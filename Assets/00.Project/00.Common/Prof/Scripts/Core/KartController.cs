// =============================================================================
// KartController
// -----------------------------------------------------------------------------
// COSA FA:
//   Controller arcade per un kart: accelera, sterza, gravita' custom, drift
//   leggero (passivo) + drift "attivo" mentre tieni premuto il tasto Drift.
//   Niente WheelCollider: il controllo del terreno e' un singolo raycast
//   verso il basso, tutto il resto e' matematica semplice.
// COME SI USA:
//   1) Mettilo sul kart (tag "Player"). Richiede in automatico Rigidbody +
//      KartInput. 2) Crea un figlio vuoto sotto il pancino del kart e
//      trascinalo in "Ground Check Origin". 3) Imposta "Ground Layer".
//   4) (Opzionale) per la sgommata: aggiungi un KartSkidmarks su un figlio,
//      collega TrailRenderer/ParticleSystem e l'IsDrifting fara' il resto.
// DA SAPERE:
//   Boost e Slow seguono la regola "il piu' recente vince". In aria lo sterzo
//   risponde meno (Air Control). Mentre tieni il tasto Drift si usa
//   "driftFactorHeld" al posto di "driftFactor": il kart scivola di piu'.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Core
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(KartInput))]
    public class KartController : MonoBehaviour
    {
        #region Inspector

        [Header("Speed")]
        // PER MODIFICARE: 22 = arcade frizzante, 12 = "go-kart in cantina", 35 = roba da Wipeout.
        [SerializeField, Tooltip("Velocita' massima in unita'/secondo.")]
        private float maxSpeed = 22f;

        [SerializeField, Tooltip("Accelerazione (unita'/sec^2).")]
        private float acceleration = 14f;

        [SerializeField, Tooltip("Decelerazione quando rilasci il gas o freni.")]
        private float deceleration = 10f;

        [SerializeField, Tooltip("Velocita' massima in retromarcia.")]
        private float reverseSpeed = 6f;

        [SerializeField, Tooltip("Forza extra del freno quando il giocatore preme Brake.")]
        private float brakeStrength = 18f;

        [Header("Steering")]
        // PER MODIFICARE: gradi/secondo di sterzata a velocita' massima.
        [SerializeField, Tooltip("Gradi al secondo di sterzata a velocita' massima.")]
        private float turnRate = 110f;

        [SerializeField, Tooltip("Quanto si gira da fermo (0 = niente, 1 = come in corsa).")]
        [Range(0f, 1f)]
        private float turnAtRest = 0.2f;

        // PER MODIFICARE: 1 = no drift, 0.92 = arcade classico, 0.7 = "ghiaccio".
        [SerializeField, Tooltip("Fattore di drift: 1 = no drift, 0 = scivola completamente.")]
        [Range(0f, 1f)]
        private float driftFactor = 0.92f;

        [Header("Drift (Sgommata)")]
        // PER MODIFICARE: 0.55 = sgommata marcata, 0.3 = "su ghiaccio", 0.8 = scivolata appena percepibile.
        [
            SerializeField,
            Tooltip(
                "Fattore di drift quando tieni premuto il tasto Drift. Piu' basso = scivola di piu'."
            )
        ]
        [Range(0f, 1f)]
        private float driftFactorHeld = 0.55f;

        [
            SerializeField,
            Tooltip(
                "Velocita' minima del kart per considerare attivo il drift (sotto questa, niente sgommata)."
            )
        ]
        private float driftMinSpeed = 4f;

        // PER MODIFICARE: 0.2 = quasi qualsiasi tocco, 0.5 = solo sterzate decise.
        [
            SerializeField,
            Tooltip(
                "Sterzata minima (0-1) per attivare il drift: evita sgommate quando vai dritto."
            )
        ]
        [Range(0f, 1f)]
        private float driftMinSteer = 0.2f;

        // PER MODIFICARE: 1 = sterzo invariato, 1.4 = drift "scattante", 2 = drift quasi 90 gradi.
        [
            SerializeField,
            Tooltip(
                "Moltiplicatore di sterzata mentre tieni Drift: 1 = nessun bonus, 1.4 = il drift gira di piu'."
            )
        ]
        private float driftSteerBoost = 1.4f;

        [
            SerializeField,
            Tooltip(
                "Transform del mesh visivo del kart (figlio). Verra' ruotato in LateUpdate per il colpo d'occhio del drift. Lascia vuoto per disattivare."
            )
        ]
        private Transform driftVisual;

        // PER MODIFICARE: 0 = niente yaw visivo, 25 = drift cartoon, 45 = "Initial D".
        [
            SerializeField,
            Tooltip(
                "Gradi massimi di rotazione visiva del mesh durante il drift (solo cosmesi, non tocca la fisica)."
            )
        ]
        private float driftVisualYawDegrees = 25f;

        [
            SerializeField,
            Tooltip("Velocita' di transizione dello yaw visivo (alto = scatto, basso = pigro).")
        ]
        private float driftVisualLerpSpeed = 8f;

        [Header("Ground & Gravity")]
        [SerializeField, Tooltip("Gravita' custom applicata al kart.")]
        private float gravity = 30f;

        [SerializeField, Tooltip("Quanto risponde lo sterzo in aria (0-1).")]
        [Range(0f, 1f)]
        private float airControl = 0.3f;

        [SerializeField, Tooltip("Distanza del raycast verso il basso per rilevare il terreno.")]
        private float groundCheckDistance = 0.6f;

        [SerializeField, Tooltip("Punto di partenza del raycast (figlio vuoto del kart).")]
        private Transform groundCheckOrigin;

        [SerializeField, Tooltip("Layer considerati come 'terreno'.")]
        private LayerMask groundLayer = ~0;

        [Header("Impact")]
        [SerializeField, Tooltip("Velocita' minima di urto per invocare OnImpact.")]
        private float impactThreshold = 5f;

        #endregion

        #region Events
        public UnityEvent<bool> OnGroundedChanged;
        public UnityEvent<float> OnSpeedChanged;
        public UnityEvent<float> OnImpact;
        #endregion

        #region Public API
        /// <summary>Current forward speed in units/second (negative = reverse).</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>Configured top speed (does not include boost multiplier).</summary>
        public float MaxSpeed => maxSpeed;

        /// <summary>True if the ground raycast hit something within range this frame.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>
        /// True quando il giocatore tiene Drift + sta sterzando + e' a terra + si muove abbastanza.
        /// In rettilineo torna false: niente sgommata fantasma quando acceleri dritto.
        /// Usata da KartSkidmarks (o qualsiasi VFX) per attivare trail e fumo.
        /// </summary>
        public bool IsDrifting =>
            input != null
            && input.Drift
            && IsGrounded
            && Mathf.Abs(CurrentSpeed) >= driftMinSpeed
            && Mathf.Abs(input.Move.x) >= driftMinSteer;

        /// <summary>Applies a temporary speed multiplier (e.g. 1.5 = +50%) for the given duration.</summary>
        public void ApplyBoost(float magnitude, float duration) =>
            StartMultiplier(Mathf.Max(1f, magnitude), duration);

        /// <summary>Applies a temporary slow multiplier (e.g. 0.5 = half speed) for the given duration.</summary>
        public void ApplySlow(float factor, float duration) =>
            StartMultiplier(Mathf.Clamp01(factor), duration);

        /// <summary>Snaps the kart to a position/rotation, zeroing velocity.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Convenience: teleports to a respawn Transform.</summary>
        public void RespawnAt(Transform t)
        {
            if (t == null)
            {
                Debug.LogWarning("[KartController] RespawnAt chiamato con Transform nullo.", this);
                return;
            }
            Teleport(t.position, t.rotation);
        }
        #endregion

        #region Unity callbacks
        private void Awake()
        {
            // Configuriamo Rigidbody come si aspetta il controller (lo studente non deve ricordare i flag).
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints =
                RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            input = GetComponent<KartInput>();
            // Senza pivot dedicato per il raycast usiamo il transform del kart (fallback funzionante ma sub-ottimale).
            if (groundCheckOrigin == null)
            {
                Debug.LogWarning(
                    "[KartController] Ground Check Origin non assegnato: uso il transform principale. Crea un figlio vuoto sotto il kart.",
                    this
                );
                groundCheckOrigin = transform;
            }
            // Memorizziamo la rotazione iniziale del mesh: lo yaw del drift verra' SOMMATO a questa,
            // cosi' rispettiamo eventuali rotazioni di setup (es. modello orientato male in import).
            if (driftVisual != null)
                driftVisualBaseRotation = driftVisual.localRotation;
        }

        private void FixedUpdate()
        {
            UpdateGrounded();
            UpdateSteering();
            UpdateVelocity();
        }

        private void LateUpdate()
        {
            // Yaw "cosmetico" del mesh: ruotiamo il modello fuori asse mentre stai driftando,
            // cosi' visivamente vedi la coda uscire come nei racing arcade. La fisica resta intatta.
            UpdateDriftVisual();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Solo gli urti "forti" generano l'evento OnImpact (evita spam su ogni sfioramento).
            float v = collision.relativeVelocity.magnitude;
            if (v >= impactThreshold)
                OnImpact?.Invoke(v);
        }

        private void OnDrawGizmos()
        {
            // Visualizza il raycast del ground check in Scene view: VERDE se aggancia, ROSSO se manca.
            // Utile per capire al volo se "groundCheckOrigin" e "groundCheckDistance" sono tarati bene.
            if (groundCheckOrigin == null)
                return;
            Gizmos.color = (Application.isPlaying && IsGrounded) ? Color.green : Color.red;
            Vector3 from = groundCheckOrigin.position;
            Vector3 to = from + Vector3.down * groundCheckDistance;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(to, 0.05f);
        }
        #endregion

        #region Internal
        private Rigidbody rb;
        private KartInput input;
        private Coroutine multiplierRoutine;
        private float speedMultiplier = 1f;
        private bool wasGrounded;
        private float lastReportedSpeed;
        private Quaternion driftVisualBaseRotation = Quaternion.identity;
        private float currentDriftYaw;

        private void UpdateGrounded()
        {
            // Singolo raycast verso il basso: per terreni complicati basta alzare la distance.
            bool grounded = Physics.Raycast(
                groundCheckOrigin.position,
                Vector3.down,
                out _,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );
            IsGrounded = grounded;
            if (grounded != wasGrounded)
            {
                wasGrounded = grounded;
                OnGroundedChanged?.Invoke(grounded);
            }
        }

        private void UpdateSteering()
        {
            // A velocita' alte si gira tanto, da fermo si gira poco; in aria lo sterzo risponde di meno.
            float speedRatio = Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, maxSpeed);
            float effectiveTurn = turnRate * Mathf.Lerp(turnAtRest, 1f, Mathf.Clamp01(speedRatio));
            if (!IsGrounded)
                effectiveTurn *= airControl;
            // Se tieni il tasto Drift e sei a terra, lo sterzo gira di piu': la macchina ruota
            // piu' rapidamente del vettore velocita', che e' cio' che genera la sensazione di drift.
            if (input.Drift && IsGrounded)
                effectiveTurn *= driftSteerBoost;
            // In retromarcia invertiamo lo sterzo (come in un'auto vera).
            float steerInput = (CurrentSpeed < 0f) ? -input.Move.x : input.Move.x;
            transform.Rotate(0f, steerInput * effectiveTurn * Time.fixedDeltaTime, 0f, Space.World);
        }

        private void UpdateVelocity()
        {
            float throttle = input.Move.y;
            float maxFwd = maxSpeed * speedMultiplier;
            float targetSpeed = throttle >= 0f ? throttle * maxFwd : throttle * reverseSpeed;
            float rate =
                (Mathf.Abs(targetSpeed) > Mathf.Abs(CurrentSpeed)) ? acceleration : deceleration;
            // Freno esplicito: tiriamo la velocita' verso 0 con brakeStrength.
            if (input.Brake)
            {
                targetSpeed = 0f;
                rate = brakeStrength;
            }
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            // A terra azzeriamo la verticale (snap), in aria applichiamo gravita'.
            float verticalVel = rb.linearVelocity.y;
            if (IsGrounded && verticalVel < 0f)
                verticalVel = 0f;
            else
                verticalVel -= gravity * Time.fixedDeltaTime;

            // Drift: preserviamo una parte della vecchia velocita' laterale. (1 - factor) = quanto scivolo.
            // Se il giocatore tiene il tasto Drift usiamo "driftFactorHeld" (piu' basso = piu' sgommata).
            float activeDriftFactor = (input.Drift && IsGrounded) ? driftFactorHeld : driftFactor;
            Vector3 forwardVel = transform.forward * CurrentSpeed;
            Vector3 keptLateral =
                Vector3.Project(rb.linearVelocity, transform.right) * (1f - activeDriftFactor);
            rb.linearVelocity = forwardVel + keptLateral + Vector3.up * verticalVel;

            // Notifica solo quando la velocita' cambia in modo apprezzabile (no spam HUD).
            if (Mathf.Abs(CurrentSpeed - lastReportedSpeed) > 0.05f)
            {
                lastReportedSpeed = CurrentSpeed;
                OnSpeedChanged?.Invoke(CurrentSpeed);
            }
        }

        private void UpdateDriftVisual()
        {
            if (driftVisual == null)
                return;

            // Yaw target: pieno solo se stai driftando davvero (Drift + sterzo + grounded + velocita').
            // In retromarcia inverto il segno cosi' la coda esce dalla parte "giusta" rispetto al kart.
            float targetYaw = 0f;
            if (IsDrifting)
            {
                float steer = (CurrentSpeed < 0f) ? -input.Move.x : input.Move.x;
                targetYaw = steer * driftVisualYawDegrees;
            }

            // Lerp framerate-independent: lo yaw insegue il target con un'inerzia visibile ma snella.
            currentDriftYaw = Mathf.Lerp(
                currentDriftYaw,
                targetYaw,
                1f - Mathf.Exp(-driftVisualLerpSpeed * Time.deltaTime)
            );

            // Sommiamo lo yaw alla rotazione iniziale del mesh (cosi' un eventuale orientamento di setup non si perde).
            driftVisual.localRotation =
                driftVisualBaseRotation * Quaternion.Euler(0f, currentDriftYaw, 0f);
        }

        private void StartMultiplier(float value, float duration)
        {
            // "Il piu' recente vince": fermiamo qualsiasi multiplier attivo.
            if (multiplierRoutine != null)
                StopCoroutine(multiplierRoutine);
            multiplierRoutine = StartCoroutine(MultiplierRoutine(value, duration));
        }

        private IEnumerator MultiplierRoutine(float value, float duration)
        {
            speedMultiplier = value;
            yield return new WaitForSeconds(duration);
            speedMultiplier = 1f;
            multiplierRoutine = null;
        }
        #endregion
    }
}
