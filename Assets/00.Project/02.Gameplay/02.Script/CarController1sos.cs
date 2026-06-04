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

        [Header("Grip / Drift")]
        [
            SerializeField,
            Tooltip("Grip laterale normale a terra. Alto = il kart si riallinea meglio.")
        ]
        private float groundLateralFriction = 14f;

        [
            SerializeField,
            Tooltip("Grip laterale mentre sei in aria. Basso = mantiene piu' inerzia laterale.")
        ]
        private float airLateralFriction = 2f;

        [SerializeField, Tooltip("Grip laterale mentre tieni premuto Drift.")]
        private float driftLateralFriction = 4f;

        [SerializeField, Tooltip("Velocita' minima del kart per considerare attivo il drift.")]
        private float driftMinSpeed = 4f;

        [SerializeField, Tooltip("Sterzata minima (0-1) per attivare il drift.")]
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

        [Header("Ground & Gravity")]
        [SerializeField, Tooltip("Gravita' custom applicata al kart.")]
        private float gravity = 30f;

        [SerializeField, Tooltip("Quanto risponde lo sterzo in aria (0-1).")]
        [Range(0f, 1f)]
        private float airControl = 0.3f;

        [
            SerializeField,
            Tooltip(
                "Distanza massima dello SphereCast centrale per rilevare il terreno e la sospensione."
            )
        ]
        private float groundCheckDistance = 1.2f;

        [SerializeField, Tooltip("Raggio dello SphereCast per il controllo del terreno.")]
        private float groundCheckRadius = 0.35f;

        [SerializeField, Tooltip("Punto di partenza dello SphereCast centrale.")]
        private Transform groundCheckOrigin;

        [SerializeField, Tooltip("Layer considerati come terreno. Imposta SOLO Ground.")]
        private LayerMask groundLayer;

        [
            SerializeField,
            Tooltip(
                "Angolo massimo (gradi) della superficie considerata terreno. Oltre questo valore e' un muro: il kart non ci sale e scivola giu'."
            )
        ]
        [Range(0f, 89f)]
        private float maxGroundSlopeAngle = 45f;

        [SerializeField, Tooltip("Piccolo tempo di tolleranza prima di perdere lo stato grounded.")]
        private float groundedGraceTime = 0.08f;

        [SerializeField, Tooltip("Altezza desiderata del kart dal terreno.")]
        private float rideHeight = 0.8f;

        [SerializeField, Tooltip("Forza della sospensione raycast.")]
        private float suspensionStrength = 90f;

        [SerializeField, Tooltip("Smorzamento della sospensione.")]
        private float suspensionDamping = 12f;

        [Header("Wall Avoidance")]
        [
            SerializeField,
            Tooltip(
                "Tempo di tolleranza in cui il contatto col muro resta attivo anche se la collisione sfarfalla. Evita la salita a scatti (cricchetto)."
            )
        ]
        private float wallContactGraceTime = 0.2f;

        [Header("Air Stability")]
        [
            SerializeField,
            Tooltip("Smorza la rotazione residua in aria per evitare spin strani al rientro.")
        ]
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
            && Mathf.Abs(input.Move.x) >= driftMinSteer;

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
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints =
                RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

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
                driftVisualBaseRotation = driftVisual.localRotation;
        }

        private void FixedUpdate()
        {
            bool wasGroundedLastFrame = IsGrounded;

            UpdateGrounded();
            HandleLandingStabilization(wasGroundedLastFrame);
            ApplySuspension();
            ApplyAirStabilization();
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
            // Niente filtro per layer: l'anti-arrampicata dipende dalla geometria
            // (quanto e' ripida la superficie), non dal fatto che sia "ground". Le
            // superfici calpestabili hanno angolo <= soglia e non scattano comunque.
            // Cerca il contatto piu' ripido: se supera il limite e' un muro e
            // memorizziamo la sua normale per deviare la velocita' in UpdateVelocity.
            float steepestAngle = maxGroundSlopeAngle;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 n = collision.GetContact(i).normal;
                float angle = Vector3.Angle(n, Vector3.up);
                if (angle > steepestAngle)
                {
                    steepestAngle = angle;
                    steepWallNormal = n;
                    steepWallPoint = collision.GetContact(i).point;
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

            if (hasDebugGroundHit)
            {
                Gizmos.color = debugGroundHitWalkable ? Color.green : Color.red;
                Gizmos.DrawLine(debugGroundHitPoint, debugGroundHitPoint + debugGroundHitNormal);
                Gizmos.DrawWireSphere(debugGroundHitPoint, 0.06f);
            }

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
        private RaycastHit groundHit;
        private float lastGroundedTime;
        private bool hasGroundContactThisFrame;
        private bool hasDebugGroundHit;
        private Vector3 debugGroundHitPoint;
        private Vector3 debugGroundHitNormal;
        private bool debugGroundHitWalkable;
        private float lastWallContactTime = -999f;
        private Vector3 steepWallNormal;
        private Vector3 steepWallPoint;

        // Il contatto col muro resta "attivo" per un breve grace time dopo
        // l'ultimo OnCollisionStay, cosi' il guard non sfarfalla tra i frame.
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

        private void ApplyAirStabilization()
        {
            if (IsGrounded)
            {
                // Lo sterzo e' gestito via transform.Rotate: a terra qualsiasi
                // velocita' angolare e' residuo di una collisione e va azzerata,
                // altrimenti il kart continua a ruotare da solo ("resta in curva").
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

        private void UpdateSteering()
        {
            float speedRatio = Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, maxSpeed);
            float effectiveTurn = turnRate * Mathf.Lerp(turnAtRest, 1f, Mathf.Clamp01(speedRatio));

            if (!IsGrounded)
                effectiveTurn *= airControl;

            if (input.Drift && IsGrounded)
                effectiveTurn *= driftSteerBoost;

            float steerInput = (CurrentSpeed < 0f) ? -input.Move.x : input.Move.x;
            transform.Rotate(0f, steerInput * effectiveTurn * Time.fixedDeltaTime, 0f, Space.World);
        }

        private void UpdateVelocity()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

            float lateralSpeed = localVelocity.x;
            float forwardSpeed = localVelocity.z;
            float verticalSpeed = rb.linearVelocity.y;

            float throttle = input.Move.y;
            float maxForward = maxSpeed * speedMultiplier;
            float targetForwardSpeed =
                throttle >= 0f ? throttle * maxForward : throttle * reverseSpeed;

            float forwardRate =
                (Mathf.Abs(targetForwardSpeed) > Mathf.Abs(forwardSpeed))
                    ? acceleration
                    : deceleration;

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

            float lateralFriction = groundLateralFriction;
            if (!IsGrounded)
                lateralFriction = airLateralFriction;
            else if (IsDrifting)
                lateralFriction = driftLateralFriction;

            lateralSpeed = Mathf.MoveTowards(
                lateralSpeed,
                0f,
                lateralFriction * Time.fixedDeltaTime
            );

            verticalSpeed -= gravity * Time.fixedDeltaTime;

            Vector3 finalVelocity =
                transform.right * lateralSpeed
                + transform.forward * forwardSpeed
                + Vector3.up * verticalSpeed;

            if (WallContactActive)
            {
                // Rimuove la componente che spinge DENTRO il muro (slide lungo la
                // parete) cosi' il solver di collisione non ha piu' compenetrazione
                // da risolvere spingendo il kart verso l'alto.
                float intoWall = Vector3.Dot(finalVelocity, steepWallNormal);
                if (intoWall < 0f)
                    finalVelocity -= steepWallNormal * intoWall;

                // Il muro non puo' lanciare il kart verso l'alto.
                if (finalVelocity.y > 0f)
                    finalVelocity.y = 0f;
            }

            rb.linearVelocity = finalVelocity;

            CurrentSpeed = forwardSpeed;

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

            if (IsDrifting)
            {
                float steer = (CurrentSpeed < 0f) ? -input.Move.x : input.Move.x;
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

            if (
                !TryGetGroundPoint(frontLeftGroundProbe, out Vector3 fl)
                || !TryGetGroundPoint(frontRightGroundProbe, out Vector3 fr)
                || !TryGetGroundPoint(rearLeftGroundProbe, out Vector3 rl)
                || !TryGetGroundPoint(rearRightGroundProbe, out Vector3 rr)
            )
            {
                Quaternion targetFlat =
                    transform.rotation
                    * driftVisualBaseRotation
                    * Quaternion.Euler(0f, currentDriftYaw, 0f);

                driftVisual.rotation = Quaternion.Slerp(
                    driftVisual.rotation,
                    targetFlat,
                    1f - Mathf.Exp(-groundAlignLerpSpeed * Time.deltaTime)
                );
                return;
            }

            Vector3 frontMid = (fl + fr) * 0.5f;
            Vector3 rearMid = (rl + rr) * 0.5f;
            Vector3 leftMid = (fl + rl) * 0.5f;
            Vector3 rightMid = (fr + rr) * 0.5f;

            Vector3 groundForward = (frontMid - rearMid).normalized;
            Vector3 groundRight = (rightMid - leftMid).normalized;

            if (groundForward.sqrMagnitude < 0.001f || groundRight.sqrMagnitude < 0.001f)
                return;

            Vector3 groundUp = Vector3.Cross(groundForward, groundRight).normalized;

            if (groundUp.y < 0f)
                groundUp = -groundUp;

            Quaternion slopeRotation = Quaternion.LookRotation(groundForward, groundUp);
            Quaternion targetWorldRotation =
                slopeRotation * driftVisualBaseRotation * Quaternion.Euler(0f, currentDriftYaw, 0f);

            driftVisual.rotation = Quaternion.Slerp(
                driftVisual.rotation,
                targetWorldRotation,
                1f - Mathf.Exp(-groundAlignLerpSpeed * Time.deltaTime)
            );
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
            hasDebugGroundHit = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                bool walkable = slopeAngle <= maxGroundSlopeAngle;

                // debug: colpo piu' vicino in assoluto (qualsiasi pendenza), per il gizmo
                if (hit.distance < bestAnyDistance)
                {
                    bestAnyDistance = hit.distance;
                    debugGroundHitPoint = hit.point;
                    debugGroundHitNormal = hit.normal;
                    debugGroundHitWalkable = walkable;
                    hasDebugGroundHit = true;
                }

                if (!walkable)
                    continue; // muro troppo ripido: non e' terreno

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
            Gizmos.DrawWireSphere(probe.position + Vector3.down * visualGroundAlignDistance, 0.04f);
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
