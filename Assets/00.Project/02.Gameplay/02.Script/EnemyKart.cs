using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Gameplay
{
    // Cervello di un kart NPC nemico. Sta sulla root del kart NPC (stesso
    // GameObject di KartController) e implementa IKartInput: alimenta il
    // KartController con move/brake/drift calcolati dalla sua AI invece che
    // dall'Input System, cosi' il NPC guida con la STESSA fisica del kart
    // del giocatore (nessuna logica di movimento duplicata).
    //
    // Comportamento (vedi specifica):
    //   - Il kart del giocatore entra nel trigger di territorio
    //     (EnemyDetectionZone) -> e' "in range".
    //   - Finche' e' in range, il NPC controlla il suo cono visivo: se il
    //     giocatore entra nel cono (angolo + distanza + eventuale LOS) il
    //     NPC si "blocca" (lockedOn) e da quel momento vede COSTANTEMENTE
    //     dove si trova il giocatore, finche' resta dentro il trigger.
    //   - Quando lockedOn, il NPC insegue sterzando verso la posizione live
    //     del giocatore.
    //   - Al contatto fisico kart-kart (OnCollisionEnter con il Player), il
    //     giocatore perde gli ultimi N oggetti raccolti (RemoveLastItems).
    //   - Il NPC e' sempre CONFINATO nel territorio: sterza verso il centro
    //     quando si avvicina al bordo (soft bias). A riposo (giocatore non
    //     rilevato) vaga (wander) dentro i bounds.
    //
    // Il KartController, in modalita' AI (vedi KartController.AiSteeringMode,
    // attivata qui in Awake), sterza SEMPRE gradualmente verso la direzione
    // desiderata: niente snap di inversione ne' gating "ruota-prima-di-
    // muoverti", cosi' il NPC "sterza costantemente" come richiesto.
    [RequireComponent(typeof(KartController))]
    public class EnemyKart : MonoBehaviour, IKartInput
    {
        #region Inspector

        [Header("Territorio")]
        [SerializeField, Tooltip("Zona trigger FISSA nella scena che fa da territorio del NPC: rileva il giocatore e contiene il kart NPC. Se vuoto, il NPC non rileva ne' si contiene (wander inattivo).")]
        private EnemyDetectionZone territoryZone;

        [Header("Cono visivo")]
        [SerializeField, Tooltip("Mezzo angolo del cono visivo in gradi. 60 = cono totale di 120 gradi davanti al muso del kart.")]
        private float coneHalfAngle = 60f;

        [SerializeField, Tooltip("Distanza massima di vista del cono.")]
        private float coneDistance = 15f;

        [SerializeField, Tooltip("Se true, il rilevamento richiede line-of-sight: un muro fra NPC e giocatore blocca la vista.")]
        private bool losCheck = true;

        [SerializeField, Tooltip("Layer dei muri che bloccano la visuale (line-of-sight). Default: Muris. Lasciare 0 per non usare ostruzioni.")]
        private LayerMask losBlockMask;

        [Header("Contatto (perdita oggetti)")]
        [SerializeField, Tooltip("Oggetti persi dal giocatore (dalla cima della torre) ad ogni contatto fisico con il kart NPC.")]
        private int itemsLostPerContact = 1;

        [SerializeField, Tooltip("Tempo minimo (sec) fra due contatti consecutivi, per non svuotare la torre in un solo impatto prolungato.")]
        private float contactCooldown = 0.5f;

        [Header("Movimento")]
        [SerializeField, Tooltip("Magnitudo dell'input di guida quando a pieno throttle (1 = max speed del KartController).")]
        private float driveMagnitude = 1f;

        [SerializeField, Tooltip("Raggio di arrivo del punto wander: sotto questa distanza il NPC ne sceglie un altro.")]
        private float wanderArrivalRadius = 1.5f;

        [SerializeField, Tooltip("Margine dal bordo del territorio: sotto questa distanza il NPC inizia a sterzare verso il centro (contenimento soft).")]
        private float edgeMargin = 2f;

        #endregion

        #region IKartInput

        // Valori calcolati in Update e letti dal KartController in
        // FixedUpdate (stesso timing di KartInput: scritti in Update, letti
        // al FixedUpdate del frame successivo).
        private Vector2 move;
        private bool brake;
        private bool drift;

        public Vector2 Move => move;
        public bool Brake => brake;
        public bool Drift => drift;
        // Il NPC non usa il reset/respawn del giocatore.
        public bool ResetPressed => false;

        #endregion

        #region Internal

        private KartController kart;
        private Transform myTransform;

        // Riferimenti al giocatore (trovati via tag "Player", come fa il
        // LevelManager). Ricerca lazy in OnEnable/Update cosi' reggono
        // attivazione/disattivazione della radice del livello.
        private Transform playerTransform;
        private KartCollectedStack playerStack;

        // Stato di rilevamento.
        private bool lockedOn;
        private float contactCooldownUntil = -1f;

        // Punto wander corrente (null = da rigenerare).
        private Vector3? wanderTarget;

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            kart = GetComponent<KartController>();
            myTransform = transform;

            // Attiva la sterzata costante sul KartController: niente snap,
            // niente gating, turnRate sempre pieno. Cosi' il NPC sterza
            // sempre gradualmente verso la direzione desiderata.
            if (kart != null)
                kart.AiSteeringMode = true;

            // Default della maschera muri se l'utente non l'ha impostata.
            if (losBlockMask.value == 0)
                losBlockMask = 1 << LayerMask.NameToLayer("Muris");
        }

        private void OnEnable()
        {
            // Reset stato quando la radice del livello viene riattivata.
            lockedOn = false;
            wanderTarget = null;
            playerTransform = null;
            playerStack = null;
            contactCooldownUntil = -1f;
            move = Vector2.zero;
            brake = false;
            drift = false;
        }

        private void Update()
        {
            if (kart == null)
            {
                move = Vector2.zero;
                return;
            }

            CachePlayerIfNeeded();

            // --- Rilevamento ---
            bool playerInRange = territoryZone != null && territoryZone.PlayerInside;

            if (!playerInRange)
            {
                // Il giocatore e' uscito dal territorio: perde il lock e
                // torna a vagare.
                lockedOn = false;
            }
            else if (!lockedOn)
            {
                // In range ma non ancora bloccato: controlla il cono. Una
                // volta bloccato resta bloccato finche' e' in range (vede
                // costantemente la posizione), anche se esce dal cono.
                if (PlayerInVisionCone())
                    lockedOn = true;
            }

            // --- Scelta del target e direzione ---
            Vector3 desiredDirXZ;
            float magnitude;

            if (lockedOn && playerTransform != null)
            {
                // INSEGUIMENTO: target = posizione live del giocatore.
                Vector3 toPlayer = playerTransform.position - myTransform.position;
                toPlayer.y = 0f;
                float dist = toPlayer.magnitude;
                if (dist <= 0.0001f)
                {
                    move = Vector2.zero;
                    brake = false;
                    drift = false;
                    return;
                }
                desiredDirXZ = toPlayer / dist;
                // Full throttle: il NPC deve raggiungere e "urtare" il
                // giocatore per fargli perdere gli oggetti.
                magnitude = driveMagnitude;
            }
            else
            {
                // WANDER dentro il territorio.
                Vector3 target = EnsureWanderTarget();
                Vector3 toTarget = target - myTransform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist <= wanderArrivalRadius)
                {
                    // Arrivato: rigenera al prossimo frame e per stavolta
                    // sta fermo (Move=0).
                    wanderTarget = null;
                    move = Vector2.zero;
                    brake = false;
                    drift = false;
                    return;
                }

                desiredDirXZ = toTarget / dist;
                // Sella il throttle sulla distanza: lontano full, vicino a
                // zero, per non sforare il punto e oscillare attorno.
                float slowRange = Mathf.Max(wanderArrivalRadius * 2f, 0.5f);
                magnitude = driveMagnitude * Mathf.Clamp01((dist - wanderArrivalRadius) / slowRange);
            }

            if (magnitude <= 0.001f || desiredDirXZ.sqrMagnitude <= 0.0001f)
            {
                move = Vector2.zero;
                brake = false;
                drift = false;
                return;
            }

            // --- Contenimento soft ---
            // Se vicino al bordo del territorio, blenda la direzione con un
            // vettore verso il centro (peso crescente al calare della
            // distanza dal bordo). Cosi' il kart non esce mai dalla zona
            // senza hard-clamp bruschi.
            desiredDirXZ = ApplyContainment(desiredDirXZ);
            if (desiredDirXZ.sqrMagnitude <= 0.0001f)
            {
                move = Vector2.zero;
                brake = false;
                drift = false;
                return;
            }

            // --- Conversione world -> camera-relative ---
            // Il KartController interpreta Move in spazio camera: ritrasforma
            // Move in direzione mondo usando la base della camera. Invertendo
            // qui la base, il round-trip recupera la direzione mondo voluta.
            move = WorldDirToCameraRelative(desiredDirXZ) * magnitude;
            brake = false;
            drift = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < contactCooldownUntil)
                return;

            // Cerchiamo il Rigidbody del kart che ci ha urtato: per i kart,
            // il Rigidbody e' sulla root (tag "Player" per il giocatore,
            // "Enemy"/Untagged per gli altri NPC).
            Rigidbody otherRb = collision.collider != null ? collision.collider.attachedRigidbody : null;
            if (otherRb == null)
                return;

            if (!otherRb.transform.CompareTag("Player"))
                return;

            if (playerStack == null)
            {
                // Cerca la torre sul giocatore (e' un figlio del kart).
                playerStack = otherRb.GetComponentInChildren<KartCollectedStack>();
            }

            if (playerStack == null)
                return;

            playerStack.RemoveLastItems(itemsLostPerContact);
            contactCooldownUntil = Time.time + contactCooldown;
        }

        #endregion

        #region Detection

        // True se il giocatore e' dentro il cono visivo del NPC (angolo +
        // distanza) ed eventualmente in line-of-sight. Chiamato solo quando
        // il giocatore e' in range (dentro il territorio) e non ancora
        // lockedOn.
        private bool PlayerInVisionCone()
        {
            if (playerTransform == null)
                return false;

            Vector3 selfPos = myTransform.position;
            Vector3 toPlayer = playerTransform.position - selfPos;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist <= 0.0001f || dist > coneDistance)
                return false;

            Vector3 fwd = myTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude <= 0.0001f)
                return false;
            fwd.Normalize();

            float angle = Vector3.Angle(fwd, toPlayer / dist);
            if (angle > coneHalfAngle)
                return false;

            if (losCheck && losBlockMask.value != 0)
            {
                // Raycast orizzontale (leggermente rialzato da terra) contro
                // i soli muri (losBlockMask): se qualcosa blocca prima di
                // raggiungere il giocatore, niente LOS. Il giocatore
                // (Vehicle) non e' nella maschera, quindi non auto-blocca.
                Vector3 origin = selfPos + Vector3.up * 0.5f;
                Vector3 target = playerTransform.position + Vector3.up * 0.5f;
                Vector3 dir = target - origin;
                float rayDist = dir.magnitude;
                if (rayDist <= 0.0001f)
                    return true;
                dir /= rayDist;
                if (Physics.Raycast(origin, dir, rayDist, losBlockMask, QueryTriggerInteraction.Ignore))
                    return false;
            }

            return true;
        }

        // Riferimento lazy al giocatore via tag "Player" (come il
        // LevelManager). Cosi' il NPC non dipende da riferimenti serializzati
        // che si rompono se il kart viene rigenerato/spostato.
        private void CachePlayerIfNeeded()
        {
            if (playerTransform != null)
                return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                return;

            playerTransform = player.transform;
            playerStack = player.GetComponentInChildren<KartCollectedStack>();
        }

        #endregion

        #region Wander & Containment

        // Restituisce il punto wander corrente, rigenerandolo dentro i bounds
        // del territorio (con margine interno) se e' null o fuori zona.
        private Vector3 EnsureWanderTarget()
        {
            if (territoryZone == null)
                return myTransform.position;

            if (wanderTarget.HasValue)
            {
                Bounds b = territoryZone.WorldBounds;
                Vector3 wt = wanderTarget.Value;
                bool validBounds = b.size.sqrMagnitude > 0.0001f;
                if (validBounds && (wt.x < b.min.x || wt.x > b.max.x || wt.z < b.min.z || wt.z > b.max.z))
                    wanderTarget = null; // fuori zona (zona cambiata): rigenera
            }

            if (!wanderTarget.HasValue)
                wanderTarget = PickRandomPointInZone();

            return wanderTarget.HasValue ? wanderTarget.Value : myTransform.position;
        }

        private Vector3? PickRandomPointInZone()
        {
            if (territoryZone == null)
                return null;

            Bounds b = territoryZone.WorldBounds;
            if (b.size.sqrMagnitude <= 0.0001f)
                return null;

            // Margine interno: non pescare sul bordo, cosi' il wander punta
            // sempre dentro la zona e il contenimento ha tempo di agire
            // prima che il kart arrivi al bordo.
            float mx = Mathf.Max(edgeMargin * 0.5f, 0.5f);
            float mz = Mathf.Max(edgeMargin * 0.5f, 0.5f);
            float x = Random.Range(b.min.x + mx, b.max.x - mx);
            float z = Random.Range(b.min.z + mz, b.max.z - mz);
            return new Vector3(x, myTransform.position.y, z);
        }

        // Blenda la direzione desiderata con un vettore verso il centro del
        // territorio quando il kart e' vicino al bordo. Peso crescente da 0
        // (lontano dal bordo) a 1 (sul bordo). Soft: niente clamp duro.
        private Vector3 ApplyContainment(Vector3 desiredDir)
        {
            if (territoryZone == null)
                return desiredDir;

            Bounds b = territoryZone.WorldBounds;
            if (b.size.sqrMagnitude <= 0.0001f || edgeMargin <= 0.0001f)
                return desiredDir;

            Vector3 p = myTransform.position;
            float dxMin = Mathf.Abs(p.x - b.min.x);
            float dxMax = Mathf.Abs(b.max.x - p.x);
            float dzMin = Mathf.Abs(p.z - b.min.z);
            float dzMax = Mathf.Abs(b.max.z - p.z);
            float minEdge = Mathf.Min(dxMin, dxMax, dzMin, dzMax);

            if (minEdge >= edgeMargin)
                return desiredDir;

            // Peso 0..1: 0 sul limite della zona di margine, 1 sul bordo.
            float w = 1f - (minEdge / edgeMargin);

            Vector3 toCenter = new Vector3(b.center.x - p.x, 0f, b.center.z - p.z);
            if (toCenter.sqrMagnitude <= 0.0001f)
                return desiredDir;
            toCenter.Normalize();

            Vector3 dirNorm = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : toCenter;
            Vector3 blended = dirNorm * (1f - w) + toCenter * w;
            if (blended.sqrMagnitude <= 0.0001f)
                return desiredDir;
            return blended.normalized;
        }

        #endregion

        #region Input mapping

        // Converte una direzione mondo (XZ, normalizzata) in un Vector2 di
        // "input" camera-relativo, coerente con la conversione inversa che
        // fa KartController.UpdateCameraRelativeMoveDirection. Cosi' il
        // round-trip (NPC -> Move -> KartController) recupera la direzione
        // mondo voluta. Se Camera.main e' null, usa il fallback diretto
        // (x->x, z->y) che combacia col fallback del KartController.
        private Vector2 WorldDirToCameraRelative(Vector3 worldDirXZ)
        {
            if (worldDirXZ.sqrMagnitude <= 0.0001f)
                return Vector2.zero;

            Vector3 d = worldDirXZ;
            d.y = 0f;
            d.Normalize();

            Camera cam = Camera.main;
            if (cam == null)
                return new Vector2(d.x, d.z);

            Vector3 camF = cam.transform.forward;
            camF.y = 0f;
            Vector3 camR = cam.transform.right;
            camR.y = 0f;
            if (camF.sqrMagnitude <= 0.0001f || camR.sqrMagnitude <= 0.0001f)
                return new Vector2(d.x, d.z);

            camF.Normalize();
            camR.Normalize();

            float x = Vector3.Dot(d, camR);
            float y = Vector3.Dot(d, camF);
            return new Vector2(x, y);
        }

        #endregion
    }
}
