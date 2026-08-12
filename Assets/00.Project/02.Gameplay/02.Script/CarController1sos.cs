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
        [SerializeField, Tooltip("Gradi al secondo di sterzata a velocita' massima.")]
        private float turnRate = 110f;

        [SerializeField, Tooltip("Quanto si gira da fermo (0 = niente, 1 = come in corsa).")]
        [Range(0f, 1f)]
        private float turnAtRest = 0.2f;

        [SerializeField, Tooltip("Quanto la sterzata perde efficacia alle alte velocita'. 0 = sterzo sempre uguale, 1 = molto effetto carrello.")]
        [Range(0f, 1f)]
        private float shoppingCartSteerLoss = 0.65f;

        [SerializeField, Tooltip("Quanto il kart perde grip laterale in curva alle alte velocita'.")]
        [Range(0f, 1f)]
        private float shoppingCartSlip = 0.75f;

        [SerializeField, Tooltip("Quanto input di sterzo serve per iniziare a far slittare sensibilmente il kart.")]
        [Range(0f, 1f)]
        private float shoppingCartSlipSteerThreshold = 0.15f;

        [SerializeField, Tooltip("Velocita' con cui il kart ruota verso la direzione desiderata letta dalla camera.")]
        private float cameraRelativeTurnResponsiveness = 10f;

        [Header("Reorientation")]
        [SerializeField, Tooltip("Soglia angolare (gradi) per il cambio direzione istantaneo. Sopra questo angolo il kart scatta subito verso la nuova direzione preservando la spinta longitudinale; sotto usa la sterzata graduale con drift e slip 'carrello della spesa'. 360 = mai snap (comportamento originale).")]
        [Range(0f, 360f)]
        private float instantRealignAngle = 90f;

        [SerializeField, Tooltip("Quanta della spinta longitudinale	vecchia viene reindirizzata lungo il nuovo forward durante uno snap. 1 = cambio direzione pulito, nessun residuo; <1 = parte della velocita' resta nel verso di prima e decresce naturalmente con il grip (derapata post-snap stile 'carrello della spesa').")]
        [Range(0f, 1f)]
        private float instantRealignLongitudinalRetention = 0.6f;

        [SerializeField, Tooltip("Sotto questa velocita' il kart e' considerato quasi fermo.")]
        private float rotateBeforeMoveSpeedThreshold = 0.35f;

        [SerializeField, Tooltip("Se il kart e' quasi fermo, deve prima girarsi sotto questo angolo per poter accelerare.")]
        private float rotateBeforeMoveReleaseAngle = 10f;

        [SerializeField, Tooltip("Angolo oltre il quale, in corsa, il cambio direzione viene trattato come inversione forte.")]
        private float movingReorientationEnterAngle = 135f;

        [SerializeField, Tooltip("Quando l'angolo scende sotto questo valore, il kart torna a spingere nella nuova direzione.")]
        private float movingReorientationExitAngle = 22f;

        [SerializeField, Tooltip("Velocita' planare minima per attivare la reorientation mentre sei in corsa.")]
        private float movingReorientationMinSpeed = 4f;

        [SerializeField, Tooltip("Quanto viene ridotta la nuova accelerazione mentre il kart si sta riallineando in corsa. 0 = nessuna spinta nuova.")]
        [Range(0f, 1f)]
        private float movingReorientationAccelerationFactor = 0.05f;

        [SerializeField, Tooltip("Extra frenata sul forward locale mentre il kart si riallinea in corsa.")]
        private float movingReorientationBrakeStrength = 20f;

        [Header("Grip / Drift")]
        [SerializeField, Tooltip("Grip laterale normale a terra. Alto = il kart si riallinea meglio.")]
        private float groundLateralFriction = 14f;

        [SerializeField, Tooltip("Grip laterale mentre sei in aria. Basso = mantiene piu' inerzia laterale.")]
        private float airLateralFriction = 2f;

        [SerializeField, Tooltip("Grip laterale mentre tieni premuto Drift.")]
        private float driftLateralFriction = 4f;

        [SerializeField, Tooltip("Velocita' minima del kart per considerare attivo il drift.")]
        private float driftMinSpeed = 4f;

        [SerializeField, Tooltip("Input minimo di direzione per considerare attivo il drift.")]
        [Range(0f, 1f)]
        private float driftMinSteer = 0.2f;

        [SerializeField, Tooltip("Moltiplicatore di sterzata mentre tieni Drift.")]
        private float driftSteerBoost = 1.4f;

        [SerializeField, Tooltip("Transform del mesh visivo del kart.")]
        private Transform driftVisual;

        [SerializeField, Tooltip("Gradi massimi di rotazione visiva del mesh durante il drift.")]
        private float driftVisualYawDegrees = 25f;

        [SerializeField, Tooltip("Velocita' di transizione dello yaw visivo.")]
        private float driftVisualLerpSpeed = 8f;

        [Header("Active Drift")]
        [SerializeField, Tooltip("Velocita' planare minima del kart per attivare e mantenere il drift attivo (Shift + sterzo). Sotto questo valore (es. dopo un impatto col muro) il drift si interrompe, senza boost.")]
        private float activeDriftMinSpeed = 5f;

        [SerializeField, Tooltip("Input di sterzo laterale (Move.x) minimo per entrare nel drift attivo.")]
        [Range(0f, 1f)]
        private float activeDriftMinSteer = 0.35f;

        [SerializeField, Tooltip("Grip laterale durante il drift attivo (separato dal drift passivo). Basso = la velocity slitta rispetto al muso (derapata).")]
        private float activeDriftLateralFriction = 3f;

        [SerializeField, Tooltip("Frazione della speed all'ingresso tenuta come velocita' longitudinale target durante la derapata. 1 = conserva, <1 = decelera leggermente. Clamp in ogni caso al floor (activeDriftMinForwardSpeed).")]
        [Range(0f, 1f)]
        private float activeDriftForwardRetention = 0.95f;

        [SerializeField, Tooltip("HARD FLOOR (unita'/sec) della velocita' longitudinale locale durante il drift attivo. Il kart non scende mai sotto questo valore finche' resta in drift: niente perdita di boost per derapate strette / 360. Pero' un impatto col muro puo' comunque abbassare la planarSpeed sotto activeDriftMinSpeed e far uscire il drift (vedi activeDriftMinSpeed).")]
        private float activeDriftMinForwardSpeed = 8f;

        [SerializeField, Tooltip("Rate (unita'/sec^2) con cui forwardSpeed raggiunge l'hard floor (activeDriftMinForwardSpeed) durante il drift attivo. Graduale per evitare scatti quando entri in drift subito dopo un'inversione a 180 senza Shift: il muso e' gia' reindirizzato ma la velocity locale e' ~0, e il floor la porta su morbida. Abbastanza rapido da sostenere il kart in derapata stretta.")]
        private float activeDriftFloorCatchUpRate = 40f;

        [SerializeField, Tooltip("Cap hard (gradi/sec) di quanto il muso puo' ruotare verso il joystick durante il drift attivo. Previene spin istantanei: niente 360 in 0.1 sec anche se il joystick fa cerchi completi. Indipendente dalla sterzata normale (turnRate).")]
        private float activeDriftMaxTurnRate = 150f;

        [SerializeField, Tooltip("Angolo minimo (gradi) fra muso del kart e direzione del joystick per accumulare carica boost. Sotto questa soglia (es. vai dritto) NON carichi. La carica accumulata resta pero' sticky (non decade): serve a impedire 'charge for free' andando dritto.")]
        private float activeDriftChargeMinAngle = 15f;

        [SerializeField, Tooltip("Tempo (sec) di sterzata sopra soglia richiesto per caricare completamente il boost (singola fase). Una volta raggiunto, isDriftCharged diventa true e resta sticky fino al rilascio del Shift o perdita speed.")]
        private float activeDriftChargeTime = 1.0f;

        [SerializeField, Tooltip("Velocita' di accumulo della carica (unita'/sec). Con driftChargeRate=1 e activeDriftChargeTime=1, ci vuole 1 secondo di sterzata sopra soglia per caricare.")]
        private float driftChargeRate = 1f;

        [SerializeField, Tooltip("Magnitude (moltiplicatore speed) del boost in uscita al rilascio dello Shift con carica completata. Singola fase: valore fisso.")]
        private float activeDriftBoostMagnitude = 1.5f;

        [SerializeField, Tooltip("Durata (sec) del boost in uscita al rilascio dello Shift con carica completata. Singola fase: valore fisso.")]
        private float activeDriftBoostDuration = 0.8f;

        [SerializeField, Tooltip("Inclinazione visiva (yaw del mesh) durante il drift attivo, come frazione del drift passivo. 0 = nessuna inclinazione, 0.4 = lieve (40% del passivo), 1 = identica al passivo. Il muso segue il joystick, il kart resta sostanzialmente dritto.")]
        [Range(0f, 1f)]
        private float activeDriftVisualYawScale = 0.4f;

        [Header("Ground & Gravity")]
        [SerializeField, Tooltip("Gravita' custom applicata al kart.")]
        private float gravity = 30f;

        [SerializeField, Tooltip("Quanto risponde il kart in aria alla direzione desiderata (0-1).")]
        [Range(0f, 1f)]
        private float airControl = 0.3f;

        [SerializeField, Tooltip("Distanza massima dello SphereCast centrale per rilevare il terreno e la sospensione.")]
        private float groundCheckDistance = 1.2f;

        [SerializeField, Tooltip("Raggio dello SphereCast per il controllo del terreno.")]
        private float groundCheckRadius = 0.35f;

        [SerializeField, Tooltip("Punto di partenza dello SphereCast centrale.")]
        private Transform groundCheckOrigin;

        [SerializeField, Tooltip("Layer considerati come terreno. Imposta SOLO Ground.")]
        private LayerMask groundLayer;

        [SerializeField, Tooltip("Angolo massimo (gradi) della superficie considerata terreno. Oltre questo valore e' un muro: il kart non ci sale e scivola giu'.")]
        [Range(0f, 89f)]
        private float maxGroundSlopeAngle = 80f;

        [SerializeField, Tooltip("Piccolo tempo di tolleranza prima di perdere lo stato grounded.")]
        private float groundedGraceTime = 0.08f;

        [SerializeField, Tooltip("Altezza desiderata del kart dal terreno.")]
        private float rideHeight = 0.8f;

        [SerializeField, Tooltip("Forza della sospensione raycast.")]
        private float suspensionStrength = 90f;

        [SerializeField, Tooltip("Smorzamento della sospensione.")]
        private float suspensionDamping = 12f;

        [Header("Wall Avoidance")]
        [SerializeField, Tooltip("Tempo di tolleranza in cui il contatto col muro resta attivo anche se la collisione sfarfalla.")]
        private float wallContactGraceTime = 0.2f;

        [Header("Air Stability")]
        [SerializeField, Tooltip("Smorza la rotazione residua in aria per evitare spin strani al rientro.")]
        private float airAngularDamping = 2.5f;

        [SerializeField, Tooltip("Limite massimo della velocita' angolare Y in aria.")]
        private float maxAirYawAngularVelocity = 2.5f;

        [SerializeField, Tooltip("Quanto smorzare la rotazione al momento dell'atterraggio.")]
        [Range(0f, 1f)]
        private float landingAngularDampingFactor = 0.2f;

        [Header("Ground Alignment Visual")]
        [SerializeField, Tooltip("Probe anteriore sinistra per allineamento visivo al terreno.")]
        private Transform frontLeftGroundProbe;

        [SerializeField, Tooltip("Probe anteriore destra per allineamento visivo al terreno.")]
        private Transform frontRightGroundProbe;

        [SerializeField, Tooltip("Probe posteriore sinistra per allineamento visivo al terreno.")]
        private Transform rearLeftGroundProbe;

        [SerializeField, Tooltip("Probe posteriore destra per allineamento visivo al terreno.")]
        private Transform rearRightGroundProbe;

        [SerializeField, Tooltip("Distanza dei raycast usati per inclinare visivamente il kart.")]
        private float visualGroundAlignDistance = 1.4f;

        [SerializeField, Tooltip("Velocita' di allineamento del mesh alla pendenza del terreno.")]
        private float groundAlignLerpSpeed = 10f;

        [SerializeField, Tooltip("Tempo (sec) con cui il modello rincorre la rotazione del corpo. Piu' alto = oscillazioni lunghe e fluide; piu' basso = modello incollato al corpo.")]
        private float visualYawSmoothTime = 0.15f;

        [SerializeField, Tooltip("Velocita' massima (gradi/sec) con cui il muso del modello ruota verso la direzione di sterzo. Il corpo fisico puo' scattare piu' in fretta (es. inversioni): il modello oscilla a questa velocita' costante invece di seguirlo.")]
        private float visualYawMaxTurnSpeed = 400f;

        [Header("Skate Ramp Launch")]
        [SerializeField, Tooltip("Velocita' angolare massima (gradi/sec) con cui il visual del kart si riallinea alla traiettoria parabolica durante un lancio skate. Basso = il muso segue lentamente la parabola (piu' fluido, meno 'snappy'); alto = il muso si allinea subito alla velocity. Simile a visualYawMaxTurnSpeed ma applicato al lancio skate.")]
        private float skateRampVisualTurnSpeed = 180f;

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

        public float CurrentSpeed { get; private set; }
        public float MaxSpeed => maxSpeed;
        public bool IsGrounded { get; private set; }

        public bool IsDrifting =>
            input != null
            && input.Drift
            && IsGrounded
            && Mathf.Abs(CurrentSpeed) >= driftMinSpeed
            && input.Move.sqrMagnitude >= driftMinSteer * driftMinSteer;

        public bool IsDriftingActive => isDriftingActive;

        public float DriftCharge => driftCharge;

        public bool IsDriftCharged => isDriftCharged;

        public void ApplyBoost(float magnitude, float duration) =>
            StartMultiplier(Mathf.Max(1f, magnitude), duration);

        public void ApplySlow(float factor, float duration) =>
            StartMultiplier(Mathf.Clamp01(factor), duration);

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            transform.SetPositionAndRotation(position, rotation);
            visualYawDegrees = rotation.eulerAngles.y;
            visualYawVelocity = 0f;
            hasVisualYawDegrees = true;
            hasVisualWorldRotation = false;

            isDriftingActive = false;
            driftCharge = 0f;
            driftEntrySpeed = 0f;
            isDriftCharged = false;
        }

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
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            // NESSUNA interpolazione: grafica (il visual) e' figlia del rigidbody e
            // viene ruotata in world space in LateUpdate. Con Interpolate attivo,
            // durante le inversioni (corpo che ruota ~49 gradi/step) il render
            // interpola la posa del padre DOPO la nostra scrittura e trascina il
            // muso di ~30 gradi in un frame: il famoso "scatto verso il mezzo".
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;

            input = GetComponent<KartInput>();

            if (groundCheckOrigin == null)
            {
                Debug.LogWarning(
                    "[KartController] Ground Check Origin non assegnato: uso il transform principale.",
                    this
                );
                groundCheckOrigin = transform;
            }

            if (driftVisual != null)
            {
                driftVisualBaseRotation = driftVisual.localRotation;
            }

            lastValidGroundUp = Vector3.up;
            hasValidGroundUp = true;

            visualYawDegrees = transform.eulerAngles.y;
            visualYawVelocity = 0f;
            hasVisualYawDegrees = true;
        }

        private void FixedUpdate()
        {
            bool wasGroundedLastFrame = IsGrounded;

            UpdateGrounded();
            HandleLandingStabilization(wasGroundedLastFrame);
            UpdateSkateRampLaunchState();
            ApplySuspension();
            ApplyAirStabilization();
            UpdateCameraRelativeMoveDirection();
            UpdateActiveDrift();
            UpdateSteering();
            UpdateVelocity();
        }

        private void LateUpdate()
        {
            UpdateDriftVisual();
            UpdateGroundAlignmentVisual();


        }



        private void OnCollisionEnter(Collision collision)
        {
            float v = collision.relativeVelocity.magnitude;
            if (v >= impactThreshold)
                OnImpact?.Invoke(v);
        }

        private void OnCollisionStay(Collision collision)
        {
            float steepestAngle = maxGroundSlopeAngle;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 n = contact.normal;
                float angle = Vector3.Angle(n, Vector3.up);

                if (angle > steepestAngle)
                {
                    // Distingue la parete verticale di una rampa da skate
                    // (collider sul layer Ground) da un muro vero (Default):
                    // sulla rampa non applichiamo il wall-avoidance; entriamo
                    // invece in modalita' "lancio" balistica, conservando la
                    // spinta residua e lasciando agire solo la gravita'.
                    bool isGroundLayer =
                        (groundLayer.value & (1 << contact.otherCollider.gameObject.layer)) != 0;

                    if (isGroundLayer)
                    {
                        lastSkateRampContactTime = Time.time;
                        // Cattura la velocity una sola volta, al momento del
                        // primo contatto con la parete verticale. Usiamo
                        // lastSetVelocity (la velocity che avevamo impostato nel
                        // FixedUpdate precedente, PRIMA che il solver delle
                        // collisioni rimuovesse la componente dentro-il-muro):
                        // cosi' conserviamo l'orientamento reale del kart subito
                        // prima di toccare la rampa e il lancio segue quella
                        // direzione (anche in avvicinamento laterale).
                        if (!skateRampLaunch)
                        {
                            skateRampLaunch = true;
                            launchVelocity = lastSetVelocity;
                            // Congela lo yaw orizzontale al momento del distacco:
                            // durante il volo parabolico il muso non segue piu'
                            // l'input del giocatore (lancio balistico), resta
                            // fisso sulla direzione in cui abbiamo lasciato il muro.
                            launchYaw = transform.eulerAngles.y;
                            visualYawDegrees = launchYaw;
                            // Cattura il PITCH dalla velocity (NON dalla posa del
                            // visual, che potrebbe essere ancora orizzontale per
                            // via dello slerp lagging). La velocity rappresenta
                            // l'orientamento reale del kart subito prima di
                            // toccare il muro: se stava salendo la rampa slopeata
                            // a 80 gradi, frozenPitch sara' ~80 gradi (muso in su).
                            // Resta FISSO per tutto il volo fino all'atterraggio.
                            Vector3 pf = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
                            float pm = pf.magnitude;
                            frozenPitch = (pm > 0.001f)
                                ? Mathf.Atan2(launchVelocity.y, pm) * Mathf.Rad2Deg
                                : (launchVelocity.y > 0f ? 90f : 0f);
                            // Cattura la NORMALE del muro al primo contatto: sara'
                            // l'"up" del kart per tutto il volo, cosi' le 4 ruote
                            // restano "attaccate" alla parete verticale. La
                            // normale punta verso l'esterno del muro (e' il
                            // reference up come se il kart guidasse sulla parete).
                            launchWallNormal = n;
                            // NON azzeriamo hasVisualWorldRotation: la
                            // RotateTowards nel branch di lancio parte dalla
                            // posa attuale del visual (allineata al terreno
                            // slope) e transita gradualmente verso la posa di
                            // lancio al rate di skateRampVisualTurnSpeed
                            // gradi/sec. Se lo azzerassimo, il primo frame
                            // snap-erebbe subito al target = scatto visibile.
                        }
                        continue;
                    }

                    steepestAngle = angle;
                    steepWallNormal = n;
                    steepWallPoint = contact.point;
                    lastWallContactTime = Time.time;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (groundCheckOrigin != null)
            {
                Gizmos.color = (Application.isPlaying && IsGrounded) ? Color.green : Color.red;
                Vector3 from = groundCheckOrigin.position;
                Vector3 to = from + Vector3.down * groundCheckDistance;
                Gizmos.DrawLine(from, to);
                Gizmos.DrawWireSphere(from, groundCheckRadius);
                Gizmos.DrawWireSphere(to, groundCheckRadius);

                Gizmos.color = Color.cyan;
                Vector3 ridePoint = from + Vector3.down * rideHeight;
                Gizmos.DrawWireSphere(ridePoint, 0.08f);
            }

            DrawProbeGizmo(frontLeftGroundProbe);
            DrawProbeGizmo(frontRightGroundProbe);
            DrawProbeGizmo(rearLeftGroundProbe);
            DrawProbeGizmo(rearRightGroundProbe);


            if (Application.isPlaying && WallContactActive)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(steepWallPoint, steepWallPoint + steepWallNormal);
                Gizmos.DrawWireSphere(steepWallPoint, 0.06f);
            }
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

        private bool isDriftingActive;
        private float driftCharge;
        private float driftEntrySpeed;
        private bool isDriftCharged;
        private float visualYawDegrees;
        private float visualYawVelocity;
        private bool hasVisualYawDegrees;
        private Quaternion visualWorldRotation = Quaternion.identity;
        private bool hasVisualWorldRotation;
        private RaycastHit groundHit;
        private float lastGroundedTime;
        private bool hasGroundContactThisFrame;

        private float lastWallContactTime = -999f;
        private Vector3 steepWallNormal;
        private Vector3 steepWallPoint;
        private bool skateRampLaunch;
        private float lastSkateRampContactTime = -999f;
        private Vector3 launchVelocity;
        private Vector3 lastSetVelocity;
        private float launchYaw;
        private float frozenPitch;
        private Vector3 launchWallNormal = Vector3.up;
        private Vector3 lastValidGroundUp = Vector3.up;
        private bool hasValidGroundUp;
        private Vector3 desiredMoveDirection;
        private float desiredMoveAmount;
        private float lastSteerAmount;
        private float currentSignedAngleToDesired;
        private bool isReorientingFromStop;
        private bool isReorientingWhileMoving;

        private bool WallContactActive => (Time.time - lastWallContactTime) <= wallContactGraceTime;

        private void UpdateGrounded()
        {
            bool hitGround = TrySphereCastGround(
                groundCheckOrigin.position,
                groundCheckRadius,
                Vector3.down,
                groundCheckDistance,
                out groundHit
            );

            hasGroundContactThisFrame = hitGround;

            if (hitGround)
                lastGroundedTime = Time.time;

            bool grounded = hitGround || (Time.time - lastGroundedTime) <= groundedGraceTime;
            IsGrounded = grounded;

            if (grounded != wasGrounded)
            {
                wasGrounded = grounded;
                OnGroundedChanged?.Invoke(grounded);
            }
        }

        private void HandleLandingStabilization(bool wasGroundedLastFrame)
        {
            if (!wasGroundedLastFrame && IsGrounded)
            {
                Vector3 av = rb.angularVelocity;
                av.y *= landingAngularDampingFactor;
                rb.angularVelocity = av;
            }
        }

private void UpdateSkateRampLaunchState()
        {
            if (!skateRampLaunch)
                return;

            // Finche' tocchiamo la parete verticale restiamo in lancio, anche se
            // lo SphereCast verso il basso becca ancora la parte slopeata della
            // rampa sotto di noi (e' "camminabile", quindi farebbe scattare
            // IsGrounded, azzerando il lancio e riattivando sospensione/wall
            // avoidance che respingono il kart sul muro). Solo quando ci siamo
            // staccati dalla parete verticale accettiamo di nuovo il grounding.
            bool stillTouchingWall =
                (Time.time - lastSkateRampContactTime) <= wallContactGraceTime;

            if (stillTouchingWall)
                return;

            // Siamo staccati dalla parete: restiamo in lancio per tutta la
            // fase aerea (volo parabolico). Usciamo SOLO quando riatterriamo
            // su una superficie camminabile. Questo disabilita sterzo e
            // air-control per tutto il volo, come richiesto.
            if (IsGrounded && hasGroundContactThisFrame)
            {
                skateRampLaunch = false;
            }
        }

        private void ApplyAirStabilization()
        {
            // Durante il lancio skate azzeriamo la angular velocity: vogliamo un
            // volo balistico pulito, senza rotazioni residue del corpo ne'
            // intervento dell'air-control (i FreezeRotationX/Z sono attivi, ma
            // azzeriamo anche il yaw per sicurezza e pulizia visiva).
            if (skateRampLaunch)
            {
                rb.angularVelocity = Vector3.zero;
                return;
            }

            if (IsGrounded)
            {
                rb.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 av = rb.angularVelocity;
            av.x = 0f;
            av.z = 0f;
            av.y = Mathf.Clamp(av.y, -maxAirYawAngularVelocity, maxAirYawAngularVelocity);
            av.y = Mathf.MoveTowards(av.y, 0f, airAngularDamping * Time.fixedDeltaTime);
            rb.angularVelocity = av;
        }

        private void ApplySuspension()
        {
            // Durante il lancio skate disattiviamo la sospensione: lo SphereCast
            // centrale verso il basso becca ancora la parte slopeata della rampa
            // sotto la parete verticale e la molla aggiungerebbe una spinta
            // extra non dovuta (il kart schizzerebbe sul muro anche a bassa
            // velocita'). In lancio vogliamo solo inerzia + gravita'.
            if (skateRampLaunch)
                return;

            if (!hasGroundContactThisFrame)
                return;

            float compression = Mathf.Clamp(rideHeight - groundHit.distance, 0f, rideHeight);
            if (compression <= 0f)
                return;

            float springForce = compression * suspensionStrength;
            float verticalVelocity = Vector3.Dot(rb.linearVelocity, Vector3.up);
            float dampingForce = -verticalVelocity * suspensionDamping;
            float totalForce = springForce + dampingForce;

            if (totalForce <= 0f)
                return;

            rb.AddForce(Vector3.up * totalForce, ForceMode.Acceleration);
        }

        private void UpdateCameraRelativeMoveDirection()
        {
            Vector2 moveInput = input.Move;
            desiredMoveAmount = Mathf.Clamp01(moveInput.magnitude);

            if (desiredMoveAmount <= 0.001f)
            {
                desiredMoveDirection = Vector3.zero;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Vector3 fallback = new Vector3(moveInput.x, 0f, moveInput.y);
                desiredMoveDirection = fallback.normalized;
                return;
            }

            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            Vector3 move =
                camForward * moveInput.y +
                camRight * moveInput.x;

            if (move.sqrMagnitude <= 0.001f)
            {
                desiredMoveDirection = Vector3.zero;
                return;
            }

            desiredMoveDirection = move.normalized;
        }

        private void UpdateActiveDrift()
        {
            Vector3 planarVel = rb.linearVelocity;
            planarVel.y = 0f;
            float planarSpeed = planarVel.magnitude;

            if (isDriftingActive)
            {
                bool lostGround = !IsGrounded || planarSpeed < activeDriftMinSpeed;
                bool releasedDrift = input.Drift == false;

                if (lostGround || releasedDrift)
                {
                    // Uscita INTELLIGENTE:
                    //  - Rilascio Shift con carica completata -> ApplyBoost
                    //    (singola fase, magnitude/duration fissi).
                    //  - Crash (planarSpeed < activeDriftMinSpeed) o salto ->
                    //    reset senza boost. isDriftCharged decide se boostare,
                    //    NON driftCharge direttamente, cosi' il boost si ha
                    //    solo se hai sterzato abbastanza a lungo.
                    if (releasedDrift && isDriftCharged)
                    {
                        ApplyBoost(activeDriftBoostMagnitude, activeDriftBoostDuration);
                    }

                    isDriftingActive = false;
                    driftCharge = 0f;
                    driftEntrySpeed = 0f;
                    isDriftCharged = false;
                    return;
                }

                // Carica boost: accumula driftCharge SOLO se stai sterzando
                // abbastanza (|angolo muso-vs-joystick| >= soglia). Sticky:
                // se smetti di sterzare o cambi direzione, la carica accumulata
                // NON decade. Cosi' puoi caricare, poi andare dritto/cambiare
                // direzione senza perdere il boost, e rilasciarlo quando vuoi.
                // Clamp a activeDriftChargeTime: singola fase, non serve oltre.
                float angleToJoystick = Mathf.Abs(currentSignedAngleToDesired);
                if (angleToJoystick >= activeDriftChargeMinAngle)
                {
                    driftCharge = Mathf.Min(
                        driftCharge + driftChargeRate * Time.fixedDeltaTime,
                        activeDriftChargeTime
                    );
                }

                // Sticky charged: una volta raggiunta la soglia resta true
                // fino al reset (rilascio Shift / crash). Niente decay.
                if (!isDriftCharged && driftCharge >= activeDriftChargeTime)
                {
                    isDriftCharged = true;
                }
            }
            else
            {
                bool canEnter =
                    input.Drift
                    && IsGrounded
                    && planarSpeed >= activeDriftMinSpeed
                    && Mathf.Abs(input.Move.x) >= activeDriftMinSteer;

                if (canEnter)
                {
                    isDriftingActive = true;
                    driftEntrySpeed = planarSpeed;
                    driftCharge = 0f;
                    isDriftCharged = false;
                }
            }
        }

        private void UpdateSteering()
        {
            currentSignedAngleToDesired = 0f;
            lastSteerAmount = 0f;
            isReorientingFromStop = false;
            isReorientingWhileMoving = false;

            // Lancio skate: balistico, nessuno sterzo da input.
            if (skateRampLaunch)
                return;

            // ===== DRIFT ATTIVO: sterzata graduale che segue il joystick =====
            // Il muso rincorre desiredMoveDirection (joystick, camera-relative)
            // come farebbe la sterzata normale, ma con un cap hard dedicato
            // (activeDriftMaxTurnRate gradi/sec) che previene spin istantanei.
            // Niente offset oversteer, niente target "oltre la curva": la
            // derapata nasce solo dalla fisica (grip laterale abbassato +
            // velocity longitudinale mantenuta), non dal muso forzato oltre
            // la sterzo. Cosi' il kart segue il joystick in curve complete
            // (anche tornanti / 360) senza bloccarsi a 180 gradi.
            // Mathf.DeltaAngle gestisce il wrap-around a 180 senza flip.
            if (isDriftingActive)
            {
                if (desiredMoveDirection.sqrMagnitude <= 0.001f)
                    return;

                Vector3 driftCurrentFwd = transform.forward;
                driftCurrentFwd.y = 0f;
                driftCurrentFwd.Normalize();

                Vector3 driftDesiredFwd = desiredMoveDirection;
                driftDesiredFwd.y = 0f;
                driftDesiredFwd.Normalize();
                if (driftDesiredFwd.sqrMagnitude < 0.001f)
                    driftDesiredFwd = driftCurrentFwd;

                float targetYaw = Mathf.Atan2(driftDesiredFwd.x, driftDesiredFwd.z) * Mathf.Rad2Deg;
                float currentYaw = Mathf.Atan2(driftCurrentFwd.x, driftCurrentFwd.z) * Mathf.Rad2Deg;
                float driftDeltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);

                float driftMaxStep = activeDriftMaxTurnRate * Time.fixedDeltaTime;
                float driftAppliedYaw = Mathf.Clamp(driftDeltaYaw, -driftMaxStep, driftMaxStep);
                transform.Rotate(0f, driftAppliedYaw, 0f, Space.World);

                // Angolo fra muso e joystick: usato da Step 3 per la carica
                // boost (carica solo se |angolo| >= activeDriftChargeMinAngle).
                currentSignedAngleToDesired = driftDeltaYaw;
                lastSteerAmount = 1f;
                return;
            }

            if (desiredMoveDirection.sqrMagnitude <= 0.001f)
                return;

            Vector3 currentForward = transform.forward;
            currentForward.y = 0f;
            currentForward.Normalize();

            Vector3 desiredForward = desiredMoveDirection;
            desiredForward.y = 0f;
            desiredForward.Normalize();

            float signedAngle = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);
            float absAngle = Mathf.Abs(signedAngle);
            currentSignedAngleToDesired = signedAngle;

            // SNAP ISTANTANEO per grandi cambi di direzione (inversioni, tornanti
            // stretti, rotazioni ad O da fermo): ruoto subito il corpo di tutta
            // l'angolatura residua e reindirizzo la componente longitudinale della
            // velocity lungo il nuovo forward, preservando l'energia. La parte
            // laterale preesistente viene lasciata al normale smorzamento della
            // grip. Sotto la soglia si ricade nel comportamento graduale originale
            // (carrello della spesa con slip e drift).
            if (absAngle >= instantRealignAngle)
            {
                Vector3 vel = rb.linearVelocity;
                Vector3 planarVel = new Vector3(vel.x, 0f, vel.z);
                float fwdComp = Vector3.Dot(planarVel, currentForward);
                Vector3 lateralRemainder = planarVel - currentForward * fwdComp;

                transform.Rotate(0f, signedAngle, 0f, Space.World);

                Vector3 newForwardXZ = transform.forward;
                newForwardXZ.y = 0f;
                newForwardXZ.Normalize();

                float keptFwd = fwdComp * instantRealignLongitudinalRetention;
                float residualFwd = fwdComp * (1f - instantRealignLongitudinalRetention);
                Vector3 newPlanarVel =
                    newForwardXZ * keptFwd
                    + lateralRemainder
                    + currentForward * residualFwd;

                rb.linearVelocity = new Vector3(newPlanarVel.x, vel.y, newPlanarVel.z);

                currentSignedAngleToDesired = 0f;
                lastSteerAmount = 0f;
                return;
            }

            // GRADUALE: comportamento originale per angoli piccoli. Il corpo
            // ruota a turn-rate, mentre la velocity mondiale continua dritta:
            // si apre un angolo fra forward e velocity e nasce lo slittamento
            // laterale assorbito progressivamente dalla grip (carrello della
            // spesa). shoppingCartSlip riduce la grip alle alte velocita' in
            // curva e isDrifting la abbassa ulteriormente col tasto drift.
            Vector3 planarVelocity = rb.linearVelocity;
            planarVelocity.y = 0f;
            float planarSpeed = planarVelocity.magnitude;

            bool nearStopped = planarSpeed <= rotateBeforeMoveSpeedThreshold;
            bool movingFastEnough = planarSpeed >= movingReorientationMinSpeed;

            if (nearStopped && absAngle > rotateBeforeMoveReleaseAngle)
            {
                isReorientingFromStop = true;
            }
            else if (
                IsGrounded &&
                movingFastEnough &&
                absAngle >= movingReorientationEnterAngle &&
                desiredMoveAmount > 0.001f
            )
            {
                isReorientingWhileMoving = true;
            }
            else if (
                IsGrounded &&
                movingFastEnough &&
                absAngle > movingReorientationExitAngle &&
                Vector3.Dot(currentForward, desiredForward) < 0f
            )
            {
                isReorientingWhileMoving = true;
            }

            float normalizedTurnInput = Mathf.Clamp(signedAngle / 90f, -1f, 1f);

            float speedRatio = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, maxSpeed));
            float effectiveTurn = turnRate * Mathf.Lerp(turnAtRest, 1f, speedRatio);

            if (isReorientingFromStop || isReorientingWhileMoving)
            {
                effectiveTurn = Mathf.Max(effectiveTurn, turnRate * cameraRelativeTurnResponsiveness);
            }

            if (!IsGrounded)
                effectiveTurn *= airControl;

            if (input.Drift && IsGrounded)
                effectiveTurn *= driftSteerBoost;

            float steerLossMultiplier = Mathf.Lerp(1f, 1f - shoppingCartSteerLoss, speedRatio);
            effectiveTurn *= steerLossMultiplier;

            float maxStep = effectiveTurn * Time.fixedDeltaTime;
            float appliedYaw = Mathf.Clamp(signedAngle, -maxStep, maxStep);

            transform.Rotate(0f, appliedYaw, 0f, Space.World);
            lastSteerAmount = Mathf.Abs(normalizedTurnInput);
        }

        private void UpdateVelocity()
        {
            if (skateRampLaunch)
            {
                // Lancio skate: balistico puro. Conserviamo la spinta residua
                // catturata al momento del primo contatto (launchVelocity) e
                // applichiamo solo la gravita'. Nessuna accelerazione da input,
                // nessun wall-avoidance, nessuna sospensione: il kart "scala"
                // la parete verticale della rampa (layer Ground) seguendo
                // l'orientamento che aveva subito prima di toccarla e ricade
                // come uno skate.
                launchVelocity.y -= gravity * Time.fixedDeltaTime;
                rb.linearVelocity = launchVelocity;

                Vector3 localVel = transform.InverseTransformDirection(launchVelocity);
                CurrentSpeed = localVel.z;

                if (Mathf.Abs(CurrentSpeed - lastReportedSpeed) > 0.05f)
                {
                    lastReportedSpeed = CurrentSpeed;
                    OnSpeedChanged?.Invoke(CurrentSpeed);
                }
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            float lateralSpeed = localVelocity.x;
            float forwardSpeed = localVelocity.z;
            float verticalSpeed = rb.linearVelocity.y;

            float absAngle = Mathf.Abs(currentSignedAngleToDesired);
            float targetForwardSpeed = desiredMoveAmount * maxSpeed * speedMultiplier;

            if (isDriftingActive)
            {
                // DRIFT ATTIVO: target = retention*entry. Il floor e' applicato
                // dopo il MoveTowards con un catch-up graduale
                // (activeDriftFloorCatchUpRate) per evitare scatti quando si
                // entra in drift subito dopo un'inversione a 180 senza Shift:
                // il muso e' gia' reindirizzato ma forwardSpeed locale e' ~0.
                targetForwardSpeed = driftEntrySpeed * activeDriftForwardRetention;
            }
            else if (desiredMoveAmount <= 0.001f)
            {
                targetForwardSpeed = 0f;
            }
            else
            {
                if (isReorientingFromStop && absAngle > rotateBeforeMoveReleaseAngle)
                {
                    targetForwardSpeed = 0f;
                }
                else if (isReorientingWhileMoving && absAngle > movingReorientationExitAngle)
                {
                    float limitedTarget = desiredMoveAmount * maxSpeed * speedMultiplier * movingReorientationAccelerationFactor;
                    targetForwardSpeed = limitedTarget;
                }
            }

            float forwardRate =
                (Mathf.Abs(targetForwardSpeed) > Mathf.Abs(forwardSpeed))
                ? acceleration
                : deceleration;

            if (isReorientingWhileMoving && absAngle > movingReorientationExitAngle)
            {
                forwardRate = Mathf.Max(forwardRate, movingReorientationBrakeStrength);
            }

            if (input.Brake)
            {
                targetForwardSpeed = 0f;
                forwardRate = brakeStrength;
            }

            forwardSpeed = Mathf.MoveTowards(
                forwardSpeed,
                targetForwardSpeed,
                forwardRate * Time.fixedDeltaTime
            );

            // HARD FLOOR durante drift attivo con catch-up graduale. Il target e'
            // retention*entry, ma MoveTowards ci arriva a rate 'forwardRate'
            // (acceleration/deceleration). Se il muso e' ortogonale alla
            // velocity (ingresso drift o post-inversione a 180), forwardSpeed
            // locale parte da 0. Con uno scatto (Mathf.Max) il kart balzerebbe
            // subito a 8 nella nuova direzione del muso = accelerazione
            // innaturale. Con MoveTowards a rate dedicato (activeDriftFloorCatchUpRate)
            // la forwardSpeed raggiunge il floor in modo graduale (~0.2s),
            // sostenendo comunque il kart in derapata stretta (360 su se stesso)
            // senza farlo fermare. Il Brake bypassa questo if.
            if (isDriftingActive && !input.Brake)
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    Mathf.Max(forwardSpeed, activeDriftMinForwardSpeed),
                    activeDriftFloorCatchUpRate * Time.fixedDeltaTime
                );
            }

            float lateralFriction = groundLateralFriction;

            if (!IsGrounded)
            {
                lateralFriction = airLateralFriction;
            }
            else if (isDriftingActive)
            {
                lateralFriction = activeDriftLateralFriction;
            }
            else if (IsDrifting)
            {
                lateralFriction = driftLateralFriction;
            }

            float speedRatio = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, maxSpeed));

            // Il drift attivo ha la sua grip dedicata: lo slip multiplier del
            // "carrello della spesa" non deve intervenire (abbasserebbe di nuovo
            // la grip ulteriormente in modo non desiderato). Si applica solo
            // nel caso passivo.
            if (!isDriftingActive && IsGrounded && lastSteerAmount > shoppingCartSlipSteerThreshold)
            {
                float steerFactor = Mathf.InverseLerp(
                    shoppingCartSlipSteerThreshold,
                    1f,
                    lastSteerAmount
                );

                float slipMultiplier = Mathf.Lerp(
                    1f,
                    1f - shoppingCartSlip,
                    speedRatio * steerFactor
                );

                lateralFriction *= slipMultiplier;
            }

            lateralSpeed = Mathf.MoveTowards(
                lateralSpeed,
                0f,
                lateralFriction * Time.fixedDeltaTime
            );

            verticalSpeed -= gravity * Time.fixedDeltaTime;

            Vector3 finalVelocity =
                transform.right * lateralSpeed +
                transform.forward * forwardSpeed +
                Vector3.up * verticalSpeed;

            Vector3 planarFinal = finalVelocity;
            planarFinal.y = 0f;
            float planarMax = maxSpeed * speedMultiplier;

            if (planarFinal.magnitude > planarMax)
            {
                planarFinal = planarFinal.normalized * planarMax;
                finalVelocity.x = planarFinal.x;
                finalVelocity.z = planarFinal.z;
            }

            if (WallContactActive)
            {
                float intoWall = Vector3.Dot(finalVelocity, steepWallNormal);
                if (intoWall < 0f)
                    finalVelocity -= steepWallNormal * intoWall;

                if (finalVelocity.y > 0f)
                    finalVelocity.y = 0f;
            }

            rb.linearVelocity = finalVelocity;
            lastSetVelocity = finalVelocity;

            Vector3 localFinal = transform.InverseTransformDirection(finalVelocity);
            CurrentSpeed = localFinal.z;

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

            float targetYaw = 0f;

            // DRIFT ATTIVO: inclinazione visiva lieve, proporzionale allo sterzo
            // (angolo fra muso e direzione del joystick). Il muso segue il
            // joystick, quindi usiamo lo stesso indicatore del passivo ma
            // scalato da activeDriftVisualYawScale (default 0.4 = 40% del
            // passivo). Il kart resta sostanzialmente dritto, niente 'lean'
            // marcato.
            if (isDriftingActive)
            {
                if (desiredMoveDirection.sqrMagnitude > 0.001f)
                {
                    float signedAngle = Vector3.SignedAngle(transform.forward, desiredMoveDirection, Vector3.up);
                    float steer = Mathf.Clamp(signedAngle / 90f, -1f, 1f);
                    targetYaw = steer * driftVisualYawDegrees * activeDriftVisualYawScale;
                }
            }
            else if (IsDrifting && desiredMoveDirection.sqrMagnitude > 0.001f)
            {
                float signedAngle = Vector3.SignedAngle(transform.forward, desiredMoveDirection, Vector3.up);
                float steer = Mathf.Clamp(signedAngle / 90f, -1f, 1f);
                targetYaw = steer * driftVisualYawDegrees;
            }

            currentDriftYaw = Mathf.Lerp(
                currentDriftYaw,
                targetYaw,
                1f - Mathf.Exp(-driftVisualLerpSpeed * Time.deltaTime)
            );
        }

        private void UpdateGroundAlignmentVisual()
        {
            if (driftVisual == null)
                return;

            bool hasFL = TryGetGroundPoint(frontLeftGroundProbe, out Vector3 fl);
            bool hasFR = TryGetGroundPoint(frontRightGroundProbe, out Vector3 fr);
            bool hasRL = TryGetGroundPoint(rearLeftGroundProbe, out Vector3 rl);
            bool hasRR = TryGetGroundPoint(rearRightGroundProbe, out Vector3 rr);

            int hitCount = 0;
            if (hasFL) hitCount++;
            if (hasFR) hitCount++;
            if (hasRL) hitCount++;
            if (hasRR) hitCount++;

            Vector3 targetUp = hasValidGroundUp ? lastValidGroundUp : Vector3.up;

            if (hitCount == 4)
            {
                Vector3 frontMid = (fl + fr) * 0.5f;
                Vector3 rearMid = (rl + rr) * 0.5f;
                Vector3 leftMid = (fl + rl) * 0.5f;
                Vector3 rightMid = (fr + rr) * 0.5f;

                Vector3 groundForward = (frontMid - rearMid).normalized;
                Vector3 groundRight = (rightMid - leftMid).normalized;

                if (groundForward.sqrMagnitude >= 0.001f && groundRight.sqrMagnitude >= 0.001f)
                {
                    Vector3 groundUp = Vector3.Cross(groundForward, groundRight).normalized;

                    if (groundUp.y < 0f)
                        groundUp = -groundUp;

                    lastValidGroundUp = groundUp;
                    hasValidGroundUp = true;
                    targetUp = groundUp;
                }
            }


            // ===== Lancio skate: volo parabolico balistico =====
            // Il PITCH locale (rotazione X) resta FISSO per tutto il volo
            // (valore catturato al primo contatto col muro, ereditato dalla
            // rampa slopeata appena percorsa). Non segue la velocity nemmeno
            // durante la salita sul muro. Solo lo YAW e' autorizzato a cambiare
            // in discesa (il muso vira verso la direzione orizzontale della
            // velocity). L'up resta sempre Vector3.up: niente bank, niente roll.
            //
            // Il pitchedForward e' costruito in WORLD SPACE (planarDir*cos +
            // Vector3.up*sin), cosi' non dipende dalla direzione del right
            // locale: quando lo yaw si gira di ~180 in discesa (la velocity
            // planare si inverte) il muso resta inclinato VERSO L'ALTO di
            // frozenPitch gradi, come uno skate che ridiscende il vert.
            // ===== Lancio skate: volo parabolico "guidando sul muro" =====
            // Il kart resta visivamente ATTACCATO alla parete verticale per tutto
            // il volo, come se stesse guidando sulla superficie del muro:
            //  - UP = launchWallNormal (la normale del muro catturata al primo
            //    contatto, punta verso l'esterno della parete). Le "4 ruote"
            //    restano premute sul muro.
            //  - FORWARD = direzione del moto PROIETTATA sul piano del muro.
            //    In questo modo il muso traccia la parabola UM (su per il vert,
            //    oltre il top, giu in discesa) ruotando gradualmente verso la
            //    destinazione, ma senza mai staccarsi dalla parete.
            // Nessun bank/roll: l'up e' fisso (la parete), solo il forward
            // cambia per seguire la tangente della traiettoria.
            if (skateRampLaunch)
            {
                targetUp = launchWallNormal;

                bool stillTouchingWall =
                    (Time.time - lastSkateRampContactTime) <= wallContactGraceTime;
                Vector3 vel = stillTouchingWall ? launchVelocity : rb.linearVelocity;

                // Proietta la velocity sul piano del muro (perpendicolare alla
                // sua normale): questo e' il "forward" che il kart segue mentre
                // "guida" sulla superficie della parete, tracciando la parabola.
                Vector3 forward = Vector3.ProjectOnPlane(vel, targetUp);
                if (forward.sqrMagnitude < 0.001f)
                {
                    // Velocity quasi parallela alla normale del muro (caso raro):
                    // fallback al forward attuale del visual per evitare LookRotation
                    // degenere.
                    forward = driftVisual.forward;
                    // Proiettiamo anche il fallback sul piano del muro.
                    forward = Vector3.ProjectOnPlane(forward, targetUp);
                    if (forward.sqrMagnitude < 0.001f)
                        forward = Vector3.Cross(targetUp, Vector3.right);
                }
                forward.Normalize();

                Quaternion launchTarget = Quaternion.LookRotation(forward, targetUp);

                if (!hasVisualWorldRotation)
                {
                    visualWorldRotation = launchTarget;
                    hasVisualWorldRotation = true;
                }
                // Rate-limit angolare costante (gradi/sec) invece di Slerp:
                // il muso rincorre la traiettoria parabolica a velocita'
                // uniforme, come fa visualYawMaxTurnSpeed sullo sterzo. Piu'
                // fluido e meno "snappy" quando la velocity cambia di colpo
                // (es. impatto col muro dopo una curva di avvicinamento).
                float maxDegStep = skateRampVisualTurnSpeed * Time.deltaTime;
                visualWorldRotation = Quaternion.RotateTowards(
                    visualWorldRotation,
                    launchTarget,
                    maxDegStep
                );
                driftVisual.rotation = visualWorldRotation;
                return;
            }

            // ===== Comportamento normale (drift / inversioni / allineamento) =====
            // Durante le inversioni il corpo ruota a scatti (fisica voluta):
            // il muso invece punta la direzione di sterzo e ci arriva a velocita'
            // costante (visualYawMaxTurnSpeed), senza scatti ne' trascinamenti.
            // In drift resta agganciato al corpo per preservare il visual del drift.
            float targetYaw =
                !IsDrifting && desiredMoveDirection.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(desiredMoveDirection.x, desiredMoveDirection.z) * Mathf.Rad2Deg
                    : transform.eulerAngles.y;

            if (!hasVisualYawDegrees)
            {
                visualYawDegrees = targetYaw;
                hasVisualYawDegrees = true;
            }
            visualYawDegrees = Mathf.MoveTowardsAngle(
                visualYawDegrees,
                targetYaw,
                visualYawMaxTurnSpeed * Time.deltaTime
            );

            Vector3 yawForward =
                Quaternion.Euler(0f, visualYawDegrees, 0f) *
                driftVisualBaseRotation *
                Quaternion.Euler(0f, currentDriftYaw, 0f) *
                Vector3.forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(yawForward, targetUp);


            projectedForward = projectedForward.normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = driftVisual.forward;

            Quaternion targetWorldRotation = Quaternion.LookRotation(projectedForward, targetUp);

            // grafica e' figlia del corpo: ad ogni FixedUpdate il padre la trascina
            // con la propria rotazione (nelle inversioni anche ~50 gradi/step).
            // Se il slerp parte dal valore gia' trascinato, l'errore resta e si vede
            // come uno scatto. Partiamo invece da uno stato nostro: la scrittura
            // annulla il trascinamento completamente ad ogni frame.
            if (!hasVisualWorldRotation)
            {
                visualWorldRotation = targetWorldRotation;
                hasVisualWorldRotation = true;
            }
            visualWorldRotation = Quaternion.Slerp(
                visualWorldRotation,
                targetWorldRotation,
                1f - Mathf.Exp(-groundAlignLerpSpeed * Time.deltaTime)
            );
            driftVisual.rotation = visualWorldRotation;
        }

        private bool TrySphereCastGround(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            out RaycastHit bestHit
        )
        {
            bestHit = default;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                radius,
                direction,
                distance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            float bestWalkableDistance = float.MaxValue;
            float bestAnyDistance = float.MaxValue;
            bool found = false;


            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                bool walkable = slopeAngle <= maxGroundSlopeAngle;

                if (hit.distance < bestAnyDistance)
                {
                    bestAnyDistance = hit.distance;

                }

                if (!walkable)
                    continue;

                if (hit.distance < bestWalkableDistance)
                {
                    bestWalkableDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetGroundPoint(Transform probe, out Vector3 point)
        {
            point = Vector3.zero;

            if (probe == null)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                probe.position,
                Vector3.down,
                visualGroundAlignDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    point = hit.point;
                    found = true;
                }
            }

            return found;
        }

        private void DrawProbeGizmo(Transform probe)
        {
            if (probe == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                probe.position,
                probe.position + Vector3.down * visualGroundAlignDistance
            );
            Gizmos.DrawWireSphere(
                probe.position + Vector3.down * visualGroundAlignDistance,
                0.04f
            );
        }

        private void StartMultiplier(float value, float duration)
        {
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
