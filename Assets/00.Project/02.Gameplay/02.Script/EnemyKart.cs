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
    //   - Il NPC e' sempre CONFINATO nel territorio: sterza/frena verso il
    //     centro vicino al bordo (soft layer) + hard clamp di sicurezza in
    //     FixedUpdate (paraurti: il kart non esce MAI visibilmente). A riposo
    //     (giocatore non rilevato) vaga (wander) dentro i bounds.
    //
    // Il KartController, in modalita' AI (vedi KartController.AiSteeringMode,
    // attivata qui in Awake), sterza SEMPRE gradualmente verso la direzione
    // desiderata: niente snap di inversione ne' gating "ruota-prima-di-
    // muoverti", cosi' il NPC "sterza costantemente" come richiesto.
    // DefaultExecutionOrder(100): il FixedUpdate di EnemyKart (hard clamp)
    // gira DOPO quello del KartController (ordine 0), che setta la velocity:
    // cosi' clampo posizione/velocity appena calcolate, non quelle vecchie.
    [DefaultExecutionOrder(100)]
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

        [SerializeField, Tooltip("Margine dal bordo del territorio: sotto questa distanza il NPC frena e sterza verso il centro (contenimento soft). Deve essere >= alla distanza di frenata a maxSpeed (con maxSpeed 10 e brake 18 servono ~2.8m; 4 lascia margine). L'hard clamp di sicurezza in FixedUpdate e' comunque l'ultimo argine.")]
        private float edgeMargin = 4f;

        [SerializeField, Tooltip("Tempo massimo (sec) per raggiungere un punto wander: se scade, il NPC ne pesca subito un altro. Copre target irraggiungibili (dietro un muro) e stalli contro geometria ('si incastra').")]
        private float wanderTimeout = 10f;

        [SerializeField, Tooltip("Se il kart ha un target wander ma la sua velocita' planare resta sotto stuckSpeedThreshold per piu' di stuckTimeout secondi (es. all'avvio su una superficie non-ground dove basso throttle non supera l'attrito), forza un target LONTANO riflesso (throttle pieno) per farlo ripartire.")]
        private float stuckTimeout = 2f;

        [SerializeField, Tooltip("Soglia di velocita' planare (m/s) sotto la quale il kart e' considerato 'fermo' ai fini dello stuck detection.")]
        private float stuckSpeedThreshold = 0.5f;

        [Header("Failsafe")]
        [SerializeField, Tooltip("Se il kart scende sotto questo dislivello rispetto al centro del territorio (es. cade in un buco), viene teletrasportato al centro del territorio con velocity azzerata. Previene il NPC perso sotto la mappa.")]
        private float fallFailsafeDepth = 10f;

        [Header("Tocco muro (test)")]
        [SerializeField, Tooltip("Distanza dal bordo del territorio sotto la quale si considera che il kart 'tocca il muro' (da dentro). Sotto questa soglia il NPC cambia punto wander (riflesso opposto).")]
        private float touchThreshold = 1f;

        [SerializeField, Tooltip("Tempo minimo (sec) fra due cambio-punto da tocco-muro, per evitare spam/parasita.")]
        private float wallTouchCooldown = 0.3f;

        [Header("Debug")]
        [SerializeField, Tooltip("Log diagnostico in Console: nuovi target wander, tocchi muro, stato. TEMPORANEO: rimuovere dopo diagnosi.")]
        private bool debugLog = true;

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
        // Il NPC non usa il boost (mouse del giocatore): sempre false.
        public bool Boost => false;
        // Il NPC non usa il reset/respawn del giocatore.
        public bool ResetPressed => false;

        #endregion

        #region Internal

        private KartController kart;
        private Rigidbody rb;
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
        // Time.time in cui e' stato scelto l'attuale wanderTarget. Usato per
        // il wanderTimeout: se scade senza averlo raggiunto, viene scartato.
        private float wanderTargetTime;
        // Time.time dell'ultimo tocco-muro (per il wallTouchCooldown).
        private float lastWallTouchTime = -999f;
        // Timer per il log di stato periodico (debug).
        private float nextDebugStateLog;
        // Accumulo tempo "fermo" (velocita' bassa mentre c'e' un target wander):
        // se supera stuckTimeout, forzo un target lontano per disincagliare.
        private float stuckTimer;

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            kart = GetComponent<KartController>();
            rb = GetComponent<Rigidbody>();
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

            // Reset dei flag input: il contenimento qui sotto puo' mettere
            // brake=true; se non lo facciamo, un brake=true di un frame
            // precedente resterebbe "appeso" (stale) nei frame successivi.
            brake = false;
            drift = false;

            // --- Log di stato periodico (TEMPORANEO, debug): per diagnosticare
            // "sempre verso su" - ci dice se e' grounded, se rileva il player,
            // se e' in chase. Da rimuovere dopo diagnosi.
            if (debugLog && Time.time >= nextDebugStateLog)
            {
                nextDebugStateLog = Time.time + 1f;
                bool pInside = territoryZone != null && territoryZone.PlayerInside;
                bool grounded = kart != null && kart.IsGrounded;
                Vector3 pos = myTransform.position;
                Vector3 fwd = myTransform.forward; fwd.y = 0f;
                Debug.Log($"[EnemyKart] stato: grounded={grounded} playerInside={pInside} lockedOn={lockedOn} pos={pos} fwd={fwd} vel={rb.linearVelocity}", this);
            }

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

                // Timeout: se non lo raggiungo in tempo (target irraggiungibile
                // tipo dietro un muro, oppure incastrato contro geometria), lo
                // scarto e al prossimo frame ne pesco un altro.
                if (wanderTimeout > 0f && (Time.time - wanderTargetTime) > wanderTimeout)
                {
                    wanderTarget = null;
                    move = Vector2.zero;
                    brake = false;
                    drift = false;
                    return;
                }

                // Stuck detection: se ho un target non ancora raggiunto ma il
                // kart e' fermo (velocita' planare bassa) per troppo tempo
                // (es. avvio su superficie non-ground: basso throttle non
                // supera l'attrito del contatto), forzo un target LONTANO
                // riflesso (throttle pieno) per disincagliarlo.
                Vector3 velXZ = rb.linearVelocity;
                velXZ.y = 0f;
                float planarSpeed = velXZ.magnitude;

                if (dist > wanderArrivalRadius && planarSpeed < stuckSpeedThreshold)
                    stuckTimer += Time.deltaTime;
                else
                    stuckTimer = 0f;

                if (stuckTimer > stuckTimeout)
                {
                    stuckTimer = 0f;
                    Vector3? far = ComputeReflectedTarget();
                    if (far.HasValue)
                    {
                        wanderTarget = far;
                        wanderTargetTime = Time.time;
                        target = far.Value;
                        toTarget = target - myTransform.position;
                        toTarget.y = 0f;
                        dist = toTarget.magnitude;
                        if (debugLog)
                            Debug.Log($"[EnemyKart] STUCK ({planarSpeed:F2} m/s per >{stuckTimeout}s): forzo target lontano {far.Value} (dist={dist:F2})", this);
                    }
                }

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

            // --- Contenimento (soft layer) ---
            // L'hard clamp di sicurezza e' in FixedUpdate; qui facciamo la
            // parte "soft" per movimento naturale: quando il kart punta fuori
            // o e' fuori, FRENA (brake=true, brakeStrength 18 del
            // KartController) + sterza verso il centro. Frenare e' essenziale:
            // abbassare solo il throttle forward non killa il momentum (il
            // kart scivola fuori per lo slip). Tenere un finalMag minimo (0.3)
            // anche in frenata perche' con Move nullo il KartController non
            // sterza (desiredMoveAmount <= 0.001 -> no steering): il minimo
            // serve solo a dare una direzione allo sterzo, il brake comanda la
            // velocita' verso 0.
            Vector3 finalDir = desiredDirXZ;
            float finalMag = magnitude;

            if (territoryZone != null)
            {
                Bounds b = territoryZone.WorldBounds;
                if (b.size.sqrMagnitude > 0.0001f)
                {
                    Vector3 p = myTransform.position;
                    Vector3 center = new Vector3(b.center.x, 0f, b.center.z);
                    Vector3 toCenter = center - new Vector3(p.x, 0f, p.z);
                    Vector3 toCenterN =
                        toCenter.sqrMagnitude > 0.0001f
                            ? toCenter.normalized
                            : (desiredDirXZ.sqrMagnitude > 0.0001f ? desiredDirXZ.normalized : Vector3.forward);

                    Vector3 desiredN =
                        desiredDirXZ.sqrMagnitude > 0.0001f ? desiredDirXZ.normalized : toCenterN;

                    bool outside =
                        p.x < b.min.x || p.x > b.max.x ||
                        p.z < b.min.z || p.z > b.max.z;

                    bool chasing = lockedOn && playerTransform != null;

                    if (outside)
                    {
                        // Fuori: frena e sterza verso il centro. Niente full
                        // throttle: il muso e' ancora rivolto verso l'esterno,
                        // accelerebbe ancora piu' fuori prima di girare.
                        // (L'hard clamp in FixedUpdate impedisce comunque
                        // l'uscita reale; qui recuperiamo direzione/speed.)
                        finalDir = toCenterN;
                        finalMag = 0.3f;
                        brake = true;
                    }
                    else if (edgeMargin > 0.0001f)
                    {
                        float dxMin = p.x - b.min.x;
                        float dxMax = b.max.x - p.x;
                        float dzMin = p.z - b.min.z;
                        float dzMax = b.max.z - p.z;
                        float minEdge = Mathf.Min(dxMin, dxMax, dzMin, dzMax);

                        if (minEdge < edgeMargin)
                        {
                            float t = 1f - (minEdge / edgeMargin);
                            float w = t * t; // curva aggressiva verso il bordo

                            // In INSEGUIMENTO (lockedOn): contenimento piu'
                            // morbido. Dimezzo blenda e rallentamento e NON
                            // freno pieno: il NPC deve raggiungere/urtare il
                            // giocatore anche se e' appiccicato al bordo;
                            // l'hard clamp resta la garanzia di non uscire.
                            // In WANDER: piu' deciso (frena + sterza al centro)
                            // perche' non c'e' un target da raggiungere al bordo.
                            float wUse = chasing ? w * 0.5f : w;

                            Vector3 blended = desiredN * (1f - wUse) + toCenterN * wUse;
                            if (blended.sqrMagnitude > 0.0001f)
                                finalDir = blended.normalized;

                            bool headingOut = Vector3.Dot(desiredN, toCenterN) < 0f;

                            if (chasing)
                            {
                                // Inseguimento: rallenta soltanto, niente full
                                // brake, cosi' arriva al giocatore al bordo.
                                finalMag = magnitude * (1f - wUse * 0.8f);
                            }
                            else if (headingOut)
                            {
                                // Wander che punta fuori: frena + sterza al
                                // centro per non sforare.
                                brake = true;
                                finalMag = 0.3f;
                            }
                            else
                            {
                                // Wander che punta al centro: rallenta.
                                finalMag = magnitude * (1f - w * 0.8f);
                            }
                        }
                    }
                }
            }

            if (finalMag <= 0.001f || finalDir.sqrMagnitude <= 0.0001f)
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
            move = WorldDirToCameraRelative(finalDir) * finalMag;
            // brake e' stato deciso dal contenimento (true se frenata); drift
            // mai usato dal NPC.
            drift = false;
        }

        // Hard clamp di sicurezza (paraurti): garantisce che il kart NON
        // esca mai visibilmente dal territorio. Il soft layer in Update frena
        // e sterza verso il centro, ma col slip/momentum non e' infallibile;
        // qui, in FixedUpdate (DOPO il KartController grazie a
        // DefaultExecutionOrder(100)), se la posizione XZ e' fuori dai
        // bounds la clampa al bordo e azzera SOLO la componente di velocity
        // uscente (mantiene quella tangenziale: il kart scivola lungo il
        // "muro" invisibile). Solo X/Z: la Y resta alla sospensione.
        private void FixedUpdate()
        {
            if (territoryZone == null || rb == null)
                return;

            Bounds b = territoryZone.WorldBounds;
            if (b.size.sqrMagnitude <= 0.0001f)
                return;

            Vector3 p = rb.position;

            // Failsafe anti-caduta: se il kart e' sceso molto sotto il centro
            // del territorio (es. caduto in un buco o fuori dalla mappa per un
            // territorio mal piazzato), lo teletrasportiamo al centro del
            // territorio. KartController.Teleport azzera velocity/drift/stato.
            if (fallFailsafeDepth > 0f && p.y < b.center.y - fallFailsafeDepth)
            {
                Vector3 respawn = new Vector3(b.center.x, b.center.y + 1.2f, b.center.z);
                if (kart != null)
                    kart.Teleport(respawn, Quaternion.Euler(0f, myTransform.eulerAngles.y, 0f));
                wanderTarget = null;
                return;
            }

            Vector3 v = rb.linearVelocity;
            bool changed = false;

            if (p.x < b.min.x)
            {
                p.x = b.min.x;
                if (v.x < 0f) v.x = 0f;
                changed = true;
            }
            else if (p.x > b.max.x)
            {
                p.x = b.max.x;
                if (v.x > 0f) v.x = 0f;
                changed = true;
            }

            if (p.z < b.min.z)
            {
                p.z = b.min.z;
                if (v.z < 0f) v.z = 0f;
                changed = true;
            }
            else if (p.z > b.max.z)
            {
                p.z = b.max.z;
                if (v.z > 0f) v.z = 0f;
                changed = true;
            }

            if (changed)
            {
                rb.position = p;
                rb.linearVelocity = v;
            }

            // --- Tocco muro (test, richiesta): "quando tocca il limite del
            // trigger, cambia punto" ---
            // Prima scattava solo su `changed` (posizione FUORI bounds ->
            // clampata), ma il kart che preme il muro da DENTRO non triggerava
            // mai. Ora scatta anche quando e' entro touchThreshold dal bordo
            // (tocco da dentro). Al tocco: target wander riflesso sul lato
            // OPPETO del territorio (cosi' gira visibilmente ~180 gradi) +
            // spegne il lock. Cooldown per evitare spam.
            float dxMinT = p.x - b.min.x;
            float dxMaxT = b.max.x - p.x;
            float dzMinT = p.z - b.min.z;
            float dzMaxT = b.max.z - p.z;
            float minEdgeT = Mathf.Min(dxMinT, dxMaxT, dzMinT, dzMaxT);

            bool touching = changed || (touchThreshold > 0f && minEdgeT <= touchThreshold);
            if (touching && (Time.time - lastWallTouchTime) >= wallTouchCooldown)
            {
                lastWallTouchTime = Time.time;

                // Target riflesso rispetto al centro -> lato opposto (throttle
                // pieno per il giro ~180 gradi). Riusato dallo stuck detection.
                Vector3? reflected = ComputeReflectedTarget();
                if (reflected.HasValue)
                {
                    wanderTarget = reflected;
                    wanderTargetTime = Time.time;
                    lockedOn = false;

                    if (debugLog)
                        Debug.Log($"[EnemyKart] TOCCO MURO (minEdge={minEdgeT:F2} changed={changed} pos={p}) -> riflesso a {reflected.Value}", this);
                }
            }
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
            {
                wanderTarget = PickRandomPointInZone();
                // Registra quando e' stato scelto: usato dal wanderTimeout.
                if (wanderTarget.HasValue)
                {
                    wanderTargetTime = Time.time;
                    if (debugLog)
                    {
                        Vector3 wt = wanderTarget.Value;
                        Vector3 dir = wt - myTransform.position; dir.y = 0f;
                        Debug.Log($"[EnemyKart] Nuovo target wander: {wt} (dir={dir}, dist={dir.magnitude:F2})", this);
                    }
                }
            }

            return wanderTarget.HasValue ? wanderTarget.Value : myTransform.position;
        }

        private Vector3? PickRandomPointInZone()
        {
            if (territoryZone == null)
                return null;

            Bounds b = territoryZone.WorldBounds;
            if (b.size.sqrMagnitude <= 0.0001f)
                return null;

            // Margine interno = edgeMargin + wanderArrivalRadius: i punti
            // vengono pescati FUORI dalla zona di frenata del contenimento,
            // cosi' sono sempre raggiungibili (altrimenti un target nella zona
            // di frenata non verrebbe mai raggiunto e il NPC oscillerebbe /
            // si "incollerebbe" a quella direzione). Clampato al 45% della
            // dimensione: se il box e' piccolo, restringe senza degenerare
            // (Range(a,a) = a -> centro).
            float spanX = b.max.x - b.min.x;
            float spanZ = b.max.z - b.min.z;
            float mx = Mathf.Clamp(edgeMargin + wanderArrivalRadius, 0f, spanX * 0.45f);
            float mz = Mathf.Clamp(edgeMargin + wanderArrivalRadius, 0f, spanZ * 0.45f);
            float x = Random.Range(b.min.x + mx, b.max.x - mx);
            float z = Random.Range(b.min.z + mz, b.max.z - mz);
            return new Vector3(x, myTransform.position.y, z);
        }

        // Target wander riflesso rispetto al centro del territorio (lato
        // opposto), clampato ai bounds interni. Usato sia dal tocco-muro
        // (FixedUpdate) sia dallo stuck detection (Update) per forzare un
        // cambio direzione netto (~180 gradi) e throttle pieno.
        private Vector3? ComputeReflectedTarget()
        {
            if (territoryZone == null)
                return null;

            Bounds b = territoryZone.WorldBounds;
            if (b.size.sqrMagnitude <= 0.0001f)
                return null;

            Vector3 p = myTransform.position;
            Vector3 center = new Vector3(b.center.x, p.y, b.center.z);
            Vector3 reflected = center + (center - p); // = 2*center - p

            float mx = Mathf.Clamp(edgeMargin + wanderArrivalRadius, 0f, (b.max.x - b.min.x) * 0.45f);
            float mz = Mathf.Clamp(edgeMargin + wanderArrivalRadius, 0f, (b.max.z - b.min.z) * 0.45f);
            reflected.x = Mathf.Clamp(reflected.x, b.min.x + mx, b.max.x - mx);
            reflected.z = Mathf.Clamp(reflected.z, b.min.z + mz, b.max.z - mz);
            reflected.y = p.y;
            return reflected;
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
