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

        [SerializeField, Tooltip("Fattore di drift: 1 = no drift, 0 = scivola completamente.")]
        [Range(0f, 1f)]
        private float driftFactor = 0.92f;

        [Header("Drift (Sgommata)")]
        [SerializeField, Tooltip("Fattore di drift quando tieni premuto il tasto Drift. Piu' basso = scivola di piu'.")]
        [Range(0f, 1f)]
        private float driftFactorHeld = 0.55f;

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

        [SerializeField, Tooltip("Distanza massima dello SphereCast centrale per rilevare il terreno e la sospensione.")]
        private float groundCheckDistance = 1.2f;

        [SerializeField, Tooltip("Raggio dello SphereCast per il controllo del terreno.")]
        private float groundCheckRadius = 0.35f;

        [SerializeField, Tooltip("Punto di partenza dello SphereCast centrale.")]
        private Transform groundCheckOrigin;

        [SerializeField, Tooltip("Layer considerati come terreno.")]
        private LayerMask groundLayer = ~0;

        [SerializeField, Tooltip("Piccolo tempo di tolleranza prima di perdere lo stato grounded.")]
        private float groundedGraceTime = 0.08f;

        [SerializeField, Tooltip("Altezza desiderata del kart dal terreno.")]
        private float rideHeight = 0.8f;

        [SerializeField, Tooltip("Forza della sospensione raycast.")]
        private float suspensionStrength = 90f;

        [SerializeField, Tooltip("Smorzamento della sospensione.")]
        private float suspensionDamping = 12f;

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
            input != null &&
            input.Drift &&
            IsGrounded &&
            Mathf.Abs(CurrentSpeed) >= driftMinSpeed &&
            Mathf.Abs(input.Move.x) >= driftMinSteer;

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
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            input = GetComponent<KartInput>();

            if (groundCheckOrigin == null)
            {
                Debug.LogWarning("[KartController] Ground Check Origin non assegnato: uso il transform principale.", this);
                groundCheckOrigin = transform;
            }

            if (driftVisual != null)
                driftVisualBaseRotation = driftVisual.localRotation;
        }

        private void FixedUpdate()
        {
            UpdateGrounded();
            ApplySuspension();
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

        private void UpdateGrounded()
        {
            bool hitGround = Physics.SphereCast(
                groundCheckOrigin.position,
                groundCheckRadius,
                Vector3.down,
                out groundHit,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
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
            float throttle = input.Move.y;
            float maxFwd = maxSpeed * speedMultiplier;
            float targetSpeed = throttle >= 0f ? throttle * maxFwd : throttle * reverseSpeed;
            float rate = (Mathf.Abs(targetSpeed) > Mathf.Abs(CurrentSpeed)) ? acceleration : deceleration;

            if (input.Brake)
            {
                targetSpeed = 0f;
                rate = brakeStrength;
            }

            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            float verticalVel = rb.linearVelocity.y;
            verticalVel -= gravity * Time.fixedDeltaTime;

            float activeDriftFactor = (input.Drift && IsGrounded) ? driftFactorHeld : driftFactor;
            Vector3 forwardVel = transform.forward * CurrentSpeed;
            Vector3 keptLateral = Vector3.Project(rb.linearVelocity, transform.right) * (1f - activeDriftFactor);

            rb.linearVelocity = forwardVel + keptLateral + Vector3.up * verticalVel;

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

            if (!TryGetGroundPoint(frontLeftGroundProbe, out Vector3 fl) ||
                !TryGetGroundPoint(frontRightGroundProbe, out Vector3 fr) ||
                !TryGetGroundPoint(rearLeftGroundProbe, out Vector3 rl) ||
                !TryGetGroundPoint(rearRightGroundProbe, out Vector3 rr))
            {
                Quaternion targetFlat =
                    transform.rotation *
                    driftVisualBaseRotation *
                    Quaternion.Euler(0f, currentDriftYaw, 0f);

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
                slopeRotation *
                driftVisualBaseRotation *
                Quaternion.Euler(0f, currentDriftYaw, 0f);

            driftVisual.rotation = Quaternion.Slerp(
                driftVisual.rotation,
                targetWorldRotation,
                1f - Mathf.Exp(-groundAlignLerpSpeed * Time.deltaTime)
            );
        }

        private bool TryGetGroundPoint(Transform probe, out Vector3 point)
        {
            point = Vector3.zero;

            if (probe == null)
                return false;

            if (Physics.Raycast(
                probe.position,
                Vector3.down,
                out RaycastHit hit,
                visualGroundAlignDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            return false;
        }

        private void DrawProbeGizmo(Transform probe)
        {
            if (probe == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(probe.position, probe.position + Vector3.down * visualGroundAlignDistance);
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
