using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeKart.Gameplay
{
    public class KartCollectedStack : MonoBehaviour
    {
        [Serializable]
        public class VisualEntry
        {
            [Tooltip("Tipo logico dell'oggetto. Es: coin, gem, key.")]
            public string visualType = "coin";

            [Tooltip("Prefab da mostrare nella torre.")]
            public GameObject visualPrefab;

            [Tooltip("Scala locale del prefab instanziato.")]
            public Vector3 localScale = Vector3.one;
        }

        [Header("References")]
        [SerializeField, Tooltip("Punto sotto cui creare la torre. Consigliato: un figlio di Grafica.")]
        private Transform stackRoot;

        [Header("Layout")]
        [SerializeField, Tooltip("Offset locale del primo elemento della torre.")]
        private Vector3 baseLocalOffset = new Vector3(0f, 0f, 0f);

        [SerializeField, Tooltip("Distanza verticale tra un elemento e il successivo.")]
        private float verticalSpacing = 0.35f;

        [SerializeField, Tooltip("Rotazione locale degli elementi impilati.")]
        private Vector3 localEuler = Vector3.zero;

        [Header("Limit")]
        [SerializeField, Tooltip("Numero massimo di oggetti visibili nella torre.")]
        private int maxItems = 5;

        [Header("Sicurezza Fisica")]
        [SerializeField, Tooltip("Layer su cui forzare i cloni della torre. Deve essere un layer inerte che NON collide col kart (Vehicle/ground) ne' coi suoi SphereCast/Raycast di ground check, cosi' una torre alta non puo' bloccare il kart in tunnel/passaggi stretti. Default: Oggeto (9).")]
        private int stackLayer = 9;

        [Header("Type Mapping")]
        [SerializeField, Tooltip("Associazione tipo -> prefab visivo.")]
        private List<VisualEntry> visuals = new List<VisualEntry>();

        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        // Stato di oscillazione (molla smorzata) per ogni elemento della torre,
        // tenuto allineato a spawnedItems: pitch = inclinazione avanti/indietro
        // (asse X locale della torre), roll = inclinazione laterale (asse Z).
        private struct ItemWobble
        {
            public float pitch;
            public float pitchVel;
            public float roll;
            public float rollVel;
        }

        private readonly List<ItemWobble> wobble = new List<ItemWobble>();

        [Header("Reattivita' Carrello")]
        [SerializeField, Tooltip("Rigidita' della molla: quanta forza richiede per inclinare la torre. Alto = torre rigida, basso = torre molle/flessibile.")]
        private float wobbleStiffness = 120f;

        [SerializeField, Tooltip("Smorzamento della molla. Piu' alto = l'oscillazione si esaurisce in fretta (niente rimbalzo); piu' basso = 'gelatina' con piu' rimbalzi. Per comportamento sotto-smorzato tenerlo sotto sqrt(4*stiffness).")]
        private float wobbleDamping = 12f;

        [SerializeField, Tooltip("Inclinazione massima in gradi per ogni elemento della torre.")]
        private float maxWobbleAngle = 35f;

        [SerializeField, Tooltip("Quanto il braccio della forza cresce con l'altezza. 0 = tutti gli elementi si inclinano uguali; >0 = gli elementi in cima oscillano di piu' (effetto carrello della spesa, torre alta scalpita in cima).")]
        private float leverPerIndex = 0.35f;

        [SerializeField, Tooltip("Moltiplicatore dell'accelerazione longitudinale sul pitch (avanti/indietro).")]
        private float longAccelToPitch = 1.6f;

        [SerializeField, Tooltip("Moltiplicatore dell'accelerazione laterale sul roll (sx/dx). Valore negativo inverte il verso di piegamento.")]
        private float latAccelToRoll = 1.1f;

        [SerializeField, Tooltip("Impulso angolare (deg/sec) applicato alla torre quando il kart prende un urto (KartController.OnImpact). Distribuito con lever arm, quindi la cima salta di piu'.")]
        private float impactImpulse = 140f;

        [SerializeField, Tooltip("Componente casuale di roll sull'urto (impulso laterale random), per dare varietà ai rimbalzi.")]
        private float impactLateralRandom = 80f;

        [SerializeField, Tooltip("Viene aggiunto alla velocita' angolare di yaw del kart per alimentare il roll in curva, anche senza grossi delta di velocita' laterale. Aiuta a sentire le sterzate.")]
        private float yawToRoll = 0.6f;

        [SerializeField, Tooltip("Smorzamento aggiuntivo quando la torre e' ferma, per evitare micro-jitter numerici.")]
        private float restDampingBoost = 1.5f;

        [SerializeField, Tooltip("Numero di elementi dal basso della torre che restano rigidi (niente oscillazione gelatina). 0 = tutti oscillano; >0 = i primi N dal fondo sono fissi, utile perche' la base di una pila reale e' stabile mentre solo la cima scalpita.")]
        private int rigidBaseCount = 1;

        public Transform StackRoot => stackRoot;
        public int ItemCount => spawnedItems.Count;

        // Cache del kart: serve per leggere velocity/accelerazione ed urti.
        private ArcadeKart.Core.KartController kart;
        private Rigidbody kartRb;
        private bool wobbleReady;
        private float prevForwardSpeed;
        private float prevRightSpeed;
        private float prevKartYaw;
        private float wobbleDesyncTimer;

        private void Awake()
        {
            kart = GetComponentInParent<ArcadeKart.Core.KartController>();
            if (kart != null)
                kartRb = kart.GetComponent<Rigidbody>();

            wobbleReady = kartRb != null;
            if (wobbleReady)
            {
                Vector3 lv = kartRb.transform.InverseTransformDirection(kartRb.linearVelocity);
                prevForwardSpeed = lv.z;
                prevRightSpeed = lv.x;
                prevKartYaw = kartRb.transform.eulerAngles.y;
            }
        }

        private void OnEnable()
        {
            if (kart != null)
                kart.OnImpact.AddListener(OnKartImpact);
        }

        private void OnDisable()
        {
            if (kart != null)
                kart.OnImpact.RemoveListener(OnKartImpact);
        }

        // KartController.OnImpact riporta la magnitudo dell'urto (m/s).
        // Lo trasformiamo in un impulso angolare distribuito con lever arm:
        // la cima della torre salta di piu' del fondo, come una pila di roba
        // che oscilla quando il carrello prende una botta.
        private void OnKartImpact(float magnitude)
        {
            if (!wobbleReady || spawnedItems.Count == 0)
                return;

            float strength = Mathf.Max(0f, magnitude);
            for (int i = 0; i < wobble.Count; i++)
            {
                float lever = 1f + leverPerIndex * i;
                // pitch verso avanti (come una frenata brusca): urto frontale.
                ItemWobble w = wobble[i];
                w.pitchVel += impactImpulse * lever * strength;
                // roll random: l'urto raramente e' perfettamente frontale,
                // aggiungiamo una componente laterale casuale.
                w.rollVel += UnityEngine.Random.Range(-1f, 1f) * impactLateralRandom * lever * strength;
                wobble[i] = w;
            }
        }

        private void Update()
        {
            // Sincronizza la lunghezza della lista wobble con spawnedItems:
            // e' una difensiva contro stati desincronizzati (oggetti distrutti
            // fuori band, add/remove che ho dimenticato di hookare, ecc.).
            wobbleDesyncTimer -= Time.deltaTime;
            if (wobbleDesyncTimer <= 0f)
            {
                SyncWobbleList();
                wobbleDesyncTimer = 1f;
            }

            if (!wobbleReady || spawnedItems.Count == 0 || kartRb == null)
                return;

            // Accel/moto del kart lette come delta della velocity locale del
            // rigidbody: fwdSpeed = forward (Z locale), rightSpeed = laterale (X).
            Vector3 localVel = kartRb.transform.InverseTransformDirection(kartRb.linearVelocity);
            float fwdSpeed = localVel.z;
            float rightSpeed = localVel.x;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            float dFwd = (fwdSpeed - prevForwardSpeed) / dt;
            float dRight = (rightSpeed - prevRightSpeed) / dt;
            prevForwardSpeed = fwdSpeed;
            prevRightSpeed = rightSpeed;

            // Yaw rate (deg/sec): usato per sentire le sterzate anche quando
            // il delta di velocita' laterale e' piccolo (curva a velocita'
            // costante: la torre sente la forza centrifuga).
            float curYaw = kartRb.transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(prevKartYaw, curYaw);
            prevKartYaw = curYaw;
            float yawVel = yawDelta / dt;

            // Per evitare spike assurdi sul primo frame o dopo un respawn
            // (dove la velocity puo' cambiare di colpo), limitiamo l'accel.
            dFwd = Mathf.Clamp(dFwd, -60f, 60f);
            dRight = Mathf.Clamp(dRight, -60f, 60f);

            // ===== Catena cinematica bottom->top =====
            // La torre si piega come un corpo unico, non come oggetti che
            // ruotano sul posto. Ogni segmento eredita l'inclinazione di
            // tutti quelli sotto di lui: accum accumula la rotazione dal
            // fondo verso la cima, e pos accumula lo spostamento del
            // "giunto" superiore di ogni segmento ruotato. Cio' fa si' che
            // la cima della torre si sposti davvero quando la pila si piega
            // (effetto canna flessibile / carrello della spesa reale).
            //
            // La base rigida (i < rigidBaseCount) ha seg = identity: resta
            // dritta e ferma a baseLocalOffset, il piegamento comincia
            // sopra l'ultimo item rigido.
            //
            // Caso statico (wobble tutto 0): accum resta identity e pos ==
            // baseLocalOffset + Vector3.up*verticalSpacing*i, identico a
            // RefreshStackLayout -> nessun pop visivo al passaggio.
            Quaternion baseRot = Quaternion.Euler(localEuler);
            Vector3 pos = baseLocalOffset;
            Quaternion accum = Quaternion.identity;
            int count = spawnedItems.Count;

            for (int i = 0; i < count; i++)
            {
                if (spawnedItems[i] == null)
                {
                    // Saltiamo l'oggetto nullo: la catena NON accumula per
                    // lui (preserva costeggiatura dei pezzi ancora vivi).
                    continue;
                }

                ItemWobble w = wobble[i];

                // Base rigida: azzera wobble e velocita' (pulizia residui).
                if (rigidBaseCount > 0 && i < rigidBaseCount)
                {
                    w.pitch = 0f;
                    w.roll = 0f;
                    w.pitchVel = 0f;
                    w.rollVel = 0f;
                    wobble[i] = w;
                }
                else
                {
                    // Lever arm differenziato: gli elementi in cima hanno
                    // braccio maggiore, oscillano piu' intensamente.
                    float lever = 1f + leverPerIndex * i;

                    // Segni:
                    //  - accel forward (dFwd > 0) -> piega INDIETRO -> pitch neg.
                    //  - frenata (dFwd < 0)      -> piega AVANTI   -> pitch pos.
                    //  - accel a destra (dRight > 0) per inerzia piega a sx -> roll neg.
                    //  - curva a destra (yawVel > 0) forza centrifuga a sx -> roll neg.
                    float pitchForce = -dFwd * longAccelToPitch * lever;
                    float rollForce = (-dRight * latAccelToRoll - yawVel * yawToRoll) * lever;

                    // Molla smorzata. Smorzamento extra a riposo per
                    // uccidere il jitter numerico quanto piu' ferma possibile.
                    bool atRest = Mathf.Abs(pitchForce) < 1f && Mathf.Abs(rollForce) < 1f;
                    float damp = wobbleDamping + (atRest ? restDampingBoost : 0f);

                    w.pitchVel += (pitchForce - wobbleStiffness * w.pitch - damp * w.pitchVel) * dt;
                    w.rollVel += (rollForce - wobbleStiffness * w.roll - damp * w.rollVel) * dt;
                    w.pitch += w.pitchVel * dt;
                    w.roll += w.rollVel * dt;

                    // Clamp angolare con annullamento velocita' in uscita:
                    // evita accumulo di energia oltre il limite visivo.
                    if (maxWobbleAngle > 0f)
                    {
                        if (w.pitch > maxWobbleAngle) { w.pitch = maxWobbleAngle; if (w.pitchVel > 0f) w.pitchVel = 0f; }
                        else if (w.pitch < -maxWobbleAngle) { w.pitch = -maxWobbleAngle; if (w.pitchVel < 0f) w.pitchVel = 0f; }
                        if (w.roll > maxWobbleAngle) { w.roll = maxWobbleAngle; if (w.rollVel > 0f) w.rollVel = 0f; }
                        else if (w.roll < -maxWobbleAngle) { w.roll = -maxWobbleAngle; if (w.rollVel < 0f) w.rollVel = 0f; }
                    }

                    wobble[i] = w;
                }

                // Rotazione di questo singolo segmento. Identity se rigido.
                Quaternion seg = Quaternion.Euler(w.pitch, 0f, w.roll);

                // Ereditiamo l'inclinazione di tutti i segmenti sotto: la
                // composizione accum*seg produce il tilt cumulativo della
                // cima rispetto alla base. Cio' che distingue una pila che
                // ruota sul posto da una che si piega come corpo e' questo
                // accumulo + lo spostamento posizionale del giunto sotto.
                accum = accum * seg;

                Transform t = spawnedItems[i].transform;
                t.localRotation = baseRot * accum;
                t.localPosition = pos;

                // Prossimo giunto: saliamo lungo la direzione "su" della
                // catena piegata. Usiamo accum (senza baseRot) perche' lo
                // spacing verticale appartiene allo spazio della torre, non
                // alla rotazione voluta dall'utente nel singolo pezzo.
                pos += accum * (Vector3.up * verticalSpacing);
            }

            // Evita che l'accelerazione derivata esploda il frame successivo
            // se l'oggetto e' stato disattivato/riattivato (Time.deltaTime 0).
            if (Time.timeScale <= 0f)
            {
                prevForwardSpeed = fwdSpeed;
                prevRightSpeed = rightSpeed;
            }
        }

        // Mantiene la lista wobble alla stessa lunghezza di spawnedItems,
        // reintegrando entry mancanti allo zero ed eliminando i ridondanti.
        private void SyncWobbleList()
        {
            while (wobble.Count < spawnedItems.Count)
                wobble.Add(default);
            while (wobble.Count > spawnedItems.Count)
                wobble.RemoveAt(wobble.Count - 1);
        }

        public void AddCollectedItem(string visualType)
        {
            if (string.IsNullOrWhiteSpace(visualType))
            {
                Debug.LogWarning("[KartCollectedStack] visualType vuoto.", this);
                return;
            }

            if (stackRoot == null)
            {
                Debug.LogWarning("[KartCollectedStack] Stack Root non assegnato.", this);
                return;
            }

            VisualEntry entry = FindEntry(visualType);
            if (entry == null)
            {
                Debug.LogWarning("[KartCollectedStack] Nessuna VisualEntry per il tipo: " + visualType, this);
                return;
            }

            // visualPrefab puo' essere "null" anche se la entry esiste: capita
            // quando il campo punta a un'istanza di scena anziche' a un prefab
            // asset di progetto e quella istanza viene distrutta a runtime
            // (es. e' essa stessa un Pickup raccolto e Destroy-ato).
            if (entry.visualPrefab == null)
            {
                Debug.LogWarning("[KartCollectedStack] Prefab visivo mancante o distrutto per il tipo: " + visualType + ". Assegna nel visualPrefab un prefab ASSET di progetto, non un'istanza di scena.", this);
                return;
            }

            if (maxItems > 0 && spawnedItems.Count >= maxItems)
                RemoveOldestItem();

            GameObject item = Instantiate(entry.visualPrefab, stackRoot);
            item.name = "Stack_" + visualType + "_" + Time.frameCount;

            SanitizeVisualClone(item);

            Transform t = item.transform;
            t.localRotation = Quaternion.Euler(localEuler);
            t.localScale = entry.localScale;

            spawnedItems.Add(item);
            wobble.Add(default);
            RefreshStackLayout();
        }

        public void ClearAll()
        {
            for (int i = spawnedItems.Count - 1; i >= 0; i--)
            {
                if (spawnedItems[i] != null)
                    Destroy(spawnedItems[i]);
            }

            spawnedItems.Clear();
            wobble.Clear();
        }

        private void RemoveOldestItem()
        {
            if (spawnedItems.Count == 0)
                return;

            GameObject oldest = spawnedItems[0];
            spawnedItems.RemoveAt(0);
            if (wobble.Count > 0)
                wobble.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest);
        }

        private void RefreshStackLayout()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] == null)
                    continue;

                Transform t = spawnedItems[i].transform;
                t.localPosition = baseLocalOffset + Vector3.up * (verticalSpacing * i);
                t.localRotation = Quaternion.Euler(localEuler);
            }
        }

        private VisualEntry FindEntry(string visualType)
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                if (string.Equals(visuals[i].visualType, visualType, StringComparison.OrdinalIgnoreCase))
                    return visuals[i];
            }

            return null;
        }

        // I cloni della torre esistono solo per scopi visivi: non devono
        // partecipare alla fisica ne' riusare la logica di Pickup del
        // prefab sorgente (che e' pensata per l'oggetto "vivo" in scena).
        // Ripuliamo quindi ogni clone a runtime cosi' e' sicuro per
        // costruzione, indipendentemente da come e' configurato il prefab
        // assegnato nel campo visualPrefab (trigger, non-trigger, con
        // Rigidbody, con Pickup, su layer sbagliato...). Cosi' una torre
        // alta non puo' mai bloccare il kart in tunnel/passaggi stretti.
        private void SanitizeVisualClone(GameObject item)
        {
            // Layer inerte: non collide col kart ne' coi suoi ground check.
            foreach (Transform tr in item.GetComponentsInChildren<Transform>(true))
                tr.gameObject.layer = stackLayer;

            // Disattiviamo ogni collider: il clone non genera ne' contatti
            // fisici ne' eventi trigger. Include eventuali collider
            // disabilitati di default nel prefab (true li prende comunque).
            Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            // Rimuoviamo eventuali Rigidbody: senza corpo fisico il clone
            // e' grafica pura, non puo' oscillare/spingere/cadere.
            Rigidbody[] rbs = item.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null)
                    Destroy(rbs[i]);
            }

            // Rimuoviamo il componente Pickup: e' la logica di raccolta
            // dell'oggetto "vivo" e non deve girare sui cloni della torre
            // (altrimenti ogni elemento impilato tenterebbe di raccogliere
            // ancora il player e sprecerebbe callback ad ogni passaggio).
            Pickup[] pickups = item.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                    Destroy(pickups[i]);
            }
        }
    }
}
