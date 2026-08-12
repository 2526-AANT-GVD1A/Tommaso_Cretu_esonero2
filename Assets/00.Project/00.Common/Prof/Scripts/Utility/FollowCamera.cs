using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeKart.Utility
{
    public class PhasedFollowCamera : MonoBehaviour
    {
        [Serializable]
        public class CameraPhase
        {
            [Tooltip("ID univoco della fase, usato dai trigger per richiamarla.")]
            public string phaseId = "Default";

            [Header("Position")]
            [Tooltip("Offset rispetto al target. Se Follow Target Rotation e' true, e' locale al target; altrimenti e' world-space.")]
            public Vector3 offset = new Vector3(0f, 4f, -7f);

            [Tooltip("Se true, l'offset ruota insieme al target.")]
            public bool followTargetRotation = true;

            [Tooltip("Se true, la camera segue la X del desiredPos. Se false usa Fixed X.")]
            public bool followX = true;

            [Tooltip("Se true, la camera segue la Y del desiredPos. Se false usa Fixed Y.")]
            public bool followY = true;

            [Tooltip("Se true, la camera segue la Z del desiredPos. Se false usa Fixed Z.")]
            public bool followZ = true;

            [Tooltip("Valore X fisso nel mondo quando Follow X e' false.")]
            public float fixedX = 0f;

            [Tooltip("Valore Y fisso nel mondo quando Follow Y e' false.")]
            public float fixedY = 5f;

            [Tooltip("Valore Z fisso nel mondo quando Follow Z e' false.")]
            public float fixedZ = 0f;

            [Header("Look")]
            [Tooltip("Punto che la camera guarda, relativo al target.")]
            public Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);

            [Tooltip("Se true, la camera ruota sempre guardando il target.")]
            public bool useLookAtTarget = true;

            [Tooltip("Rotazione fissa usata se Use Look At Target e' false.")]
            public Vector3 fixedEulerAngles = new Vector3(20f, 0f, 0f);

            [Header("Lens")]
            [Tooltip("Field of View della camera in questa fase.")]
            public float fieldOfView = 60f;

            [Header("Damping")]
            [Tooltip("Quanto velocemente la posizione raggiunge la posizione desiderata.")]
            public float followDamping = 5f;

            [Tooltip("Quanto velocemente la rotazione raggiunge la rotazione desiderata.")]
            public float lookDamping = 8f;

            [Tooltip("Quanto velocemente il FOV raggiunge il valore desiderato.")]
            public float fovDamping = 6f;
        }

        #region Inspector

        [Header("Target")]
        [SerializeField, Tooltip("Transform da seguire. Di solito il kart.")]
        private Transform target;

        [Header("Fasi")]
        [SerializeField, Tooltip("Lista delle fasi camera disponibili.")]
        private List<CameraPhase> phases = new List<CameraPhase>()
        {
            new CameraPhase()
        };

        [SerializeField, Tooltip("ID della fase iniziale attiva all'avvio.")]
        private string startingPhaseId = "Default";

        [Header("Fallback")]
        [SerializeField, Tooltip("Se true e non trova la fase iniziale, usa la prima fase della lista.")]
        private bool fallbackToFirstPhase = true;

        #endregion

        #region Public API

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            warningLogged = false;
            if (!enabled) enabled = true;
        }

        public bool SetPhase(string phaseId)
        {
            if (string.IsNullOrWhiteSpace(phaseId))
                return false;

            CameraPhase found = FindPhase(phaseId);
            if (found == null)
            {
                Debug.LogWarning("[PhasedFollowCamera] Fase camera non trovata: " + phaseId, this);
                return false;
            }

            currentPhase = found;
            return true;
        }

        public string CurrentPhaseId => currentPhase != null ? currentPhase.phaseId : string.Empty;

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            cam = GetComponent<Camera>();

            if (cam == null)
                cam = Camera.main;

            currentPhase = FindPhase(startingPhaseId);

            if (currentPhase == null && fallbackToFirstPhase && phases.Count > 0)
                currentPhase = phases[0];

            if (currentPhase == null)
            {
                Debug.LogWarning("[PhasedFollowCamera] Nessuna fase configurata.", this);
                enabled = false;
                return;
            }

            if (cam != null)
                cam.fieldOfView = currentPhase.fieldOfView;
        }

        // Follow in FixedUpdate (non in LateUpdate): il target e' un Rigidbody
        // con interpolation = None, quindi la sua transform avanza a gradini
        // nel fixed step (50 Hz). Leggendola in LateUpdate (60 Hz o variabile)
        // la desiredPos resta ferma per piu' frame render e poi salta, e il
        // damping calcolato su Time.deltaTime produce stutter periodico.
        // Muovendo la camera nello stesso fixed step del target, i due avanvano
        // in lockstep: la posizione relativa resta stabile tra un frame render
        // e l'altro e scompare lo jitter. Time.deltaTime in FixedUpdate vale
        // fixedDeltaTime, quindi le formule di smoothing restano invariate.
        private void FixedUpdate()
        {
            if (target == null)
            {
                if (!warningLogged)
                {
                    Debug.LogWarning("[PhasedFollowCamera] Target non assegnato su " + name + ". Disattivo il componente.", this);
                    warningLogged = true;
                }

                enabled = false;
                return;
            }

            if (currentPhase == null)
                return;

            Vector3 desiredPos = currentPhase.followTargetRotation
                ? target.TransformPoint(currentPhase.offset)
                : target.position + currentPhase.offset;

            if (!currentPhase.followX) desiredPos.x = currentPhase.fixedX;
            if (!currentPhase.followY) desiredPos.y = currentPhase.fixedY;
            if (!currentPhase.followZ) desiredPos.z = currentPhase.fixedZ;

            float posT = 1f - Mathf.Exp(-Mathf.Max(0.01f, currentPhase.followDamping) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, posT);

            Quaternion desiredRot = GetDesiredRotation(currentPhase);
            float rotT = 1f - Mathf.Exp(-Mathf.Max(0.01f, currentPhase.lookDamping) * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotT);

            if (cam != null)
            {
                float fovT = 1f - Mathf.Exp(-Mathf.Max(0.01f, currentPhase.fovDamping) * Time.deltaTime);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, currentPhase.fieldOfView, fovT);
            }
        }

        #endregion

        #region Internal

        private Camera cam;
        private bool warningLogged;
        private CameraPhase currentPhase;

        private CameraPhase FindPhase(string phaseId)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                if (string.Equals(phases[i].phaseId, phaseId, StringComparison.OrdinalIgnoreCase))
                    return phases[i];
            }

            return null;
        }

        private Quaternion GetDesiredRotation(CameraPhase phase)
        {
            if (!phase.useLookAtTarget)
                return Quaternion.Euler(phase.fixedEulerAngles);

            Vector3 lookAtPoint = target.position + phase.lookAtOffset;
            Vector3 dir = lookAtPoint - transform.position;

            if (dir.sqrMagnitude < 0.0001f)
                return transform.rotation;

            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        #endregion
    }
}
