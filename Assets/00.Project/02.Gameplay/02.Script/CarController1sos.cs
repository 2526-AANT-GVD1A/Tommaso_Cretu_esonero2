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
            {
                driftVisualBaseRotation = driftVisual.localRotation;
                lastValidGroundUp = Vector3.up;
                hasValidGroundUp = true;
            }
        }

        private void FixedUpdate()
        {
            bool wasGroundedLastFrame = IsGrounded;

            UpdateGrounded();
            HandleLandingStabilization(wasGroundedLastFrame);
            ApplySuspension();
            ApplyAirStabilization();
            UpdateCameraRelativeMoveDirection();
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
        private Vector3 lastValidGroundUp = Vector3.up;
        private bool hasValidGroundUp;
        private Vector3 desiredMoveDirection;
        private float desiredMoveAmount;

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

        private void UpdateSteering()
        {
            if (desiredMoveDirection.sqrMagnitude <= 0.001f)
                return;

            Vector3 currentForward = transform.forward;
            currentForward.y = 0f;
            currentForward.Normalize();

            Vector3 desiredForward = desiredMoveDirection;
            desiredForward.y = 0f;
            desiredForward.Normalize();

            float signedAngle = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);
            float normalizedTurnInput = Mathf.Clamp(signedAngle / 90f, -1f, 1f);

            float speedRatio = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, maxSpeed));
            float effectiveTurn = turnRate * Mathf.Lerp(turnAtRest, 1f, speedRatio);

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
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            float lateralSpeed = localVelocity.x;
            float forwardSpeed = localVelocity.z;
            float verticalSpeed = rb.linearVelocity.y;

            float targetForwardSpeed = desiredMoveAmount * maxSpeed * speedMultiplier;

            if (desiredMoveAmount <= 0.001f)
                targetForwardSpeed = 0f;

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
            {
                lateralFriction = airLateralFriction;
            }
            else if (IsDrifting)
            {
                lateralFriction = driftLateralFriction;
            }

            float speedRatio = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, maxSpeed));

            if (IsGrounded && lastSteerAmount > shoppingCartSlipSteerThreshold)
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

            if (WallContactActive)
            {
                float intoWall = Vector3.Dot(finalVelocity, steepWallNormal);
                if (intoWall < 0f)
                    finalVelocity -= steepWallNormal * intoWall;

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

            if (IsDrifting && desiredMoveDirection.sqrMagnitude > 0.001f)
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

            Vector3 yawForward =
                transform.rotation *
                driftVisualBaseRotation *
                Quaternion.Euler(0f, currentDriftYaw, 0f) *
                Vector3.forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(yawForward, targetUp).normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = driftVisual.forward;

            Quaternion targetWorldRotation = Quaternion.LookRotation(projectedForward, targetUp);

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

                if (hit.distance < bestAnyDistance)
                {
                    bestAnyDistance = hit.distance;
                    debugGroundHitPoint = hit.point;
                    debugGroundHitNormal = hit.normal;
                    debugGroundHitWalkable = walkable;
                    hasDebugGroundHit = true;
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

        private float lastSteerAmount;

        #endregion
    }
}
