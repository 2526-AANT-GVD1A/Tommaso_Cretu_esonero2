using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcadeKart.Utility
{
    public class PhasedFollowCamera : MonoBehaviour
    {
        // Come passa alla nuova fase quando viene richiamata:
        // Smooth = transizione fluida col damping, Snap = salto immediato.
        public enum TransitionMode
        {
            Smooth,
            Snap
        }

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

            [Header("Post Processing")]
            [Tooltip("Volume URP i cui effetti sono attivi in questa fase (es. bianco e nero, bloom, vignetta). Vuoto = questa fase non attiva effetti. Vengono gestiti solo i volume assegnati ad almeno una fase.")]
            public Volume postProcessVolume;

            [Tooltip("Intensita' del volume in questa fase (0 = spento, 1 = pieno).")]
            [Range(0f, 1f)]
            public float volumeWeight = 1f;

            [Tooltip("Quanto velocemente l'intensita' del volume raggiunge il valore desiderato.")]
            public float volumeDamping = 6f;
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
            return SetPhase(phaseId, TransitionMode.Smooth);
        }

        public bool SetPhase(string phaseId, TransitionMode mode)
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

            // Snap: porta subito la camera ai parametri della nuova fase,
            // bypassando il damping (che continuera' da qui in lockstep).
            if (mode == TransitionMode.Snap)
                SnapToCurrentPhase();

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

            BuildManagedVolumes();

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

            // Allinea subito i pesi dei volume alla fase iniziale: evita che
            // effetti lasciati a peso 1 in scena facciano un fade-out visibile
            // nei primi frame.
            UpdateVolumeWeights(true);
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

            Vector3 desiredPos = ComputeDesiredPosition(currentPhase);

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

            UpdateVolumeWeights(false);
        }

        #endregion

        #region Internal

        private Camera cam;
        private bool warningLogged;
        private CameraPhase currentPhase;

        // Volume referenziati da almeno una fase: sono gli unici che questo
        // componente guida. Volume estranei in scena non vengono toccati.
        private readonly List<Volume> managedVolumes = new List<Volume>();

        private void BuildManagedVolumes()
        {
            managedVolumes.Clear();

            for (int i = 0; i < phases.Count; i++)
            {
                Volume volume = phases[i].postProcessVolume;

                if (volume == null || managedVolumes.Contains(volume))
                    continue;

                managedVolumes.Add(volume);
            }
        }

        // Porta ogni volume gestito al peso della fase corrente: pieno se e'
        // il volume della fase, zero altrimenti. Con immediate=true salta il
        // damping (snap, stato iniziale).
        private void UpdateVolumeWeights(bool immediate)
        {
            if (managedVolumes.Count == 0 || currentPhase == null)
                return;

            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, currentPhase.volumeDamping) * Time.deltaTime);

            for (int i = 0; i < managedVolumes.Count; i++)
            {
                Volume volume = managedVolumes[i];

                if (volume == null)
                    continue;

                float target = currentPhase.postProcessVolume == volume
                    ? currentPhase.volumeWeight
                    : 0f;

                volume.weight = immediate ? target : Mathf.Lerp(volume.weight, target, t);
            }
        }

        private CameraPhase FindPhase(string phaseId)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                if (string.Equals(phases[i].phaseId, phaseId, StringComparison.OrdinalIgnoreCase))
                    return phases[i];
            }

            return null;
        }

        private Vector3 ComputeDesiredPosition(CameraPhase phase)
        {
            Vector3 desiredPos = phase.followTargetRotation
                ? target.TransformPoint(phase.offset)
                : target.position + phase.offset;

            if (!phase.followX) desiredPos.x = phase.fixedX;
            if (!phase.followY) desiredPos.y = phase.fixedY;
            if (!phase.followZ) desiredPos.z = phase.fixedZ;

            return desiredPos;
        }

        // Teletrasporta subito camera e FOV sui valori della fase corrente.
        // Prima la posizione, poi la rotazione: la look-at dipende dalla
        // posizione appena applicata, quindi l'ordine conta.
        private void SnapToCurrentPhase()
        {
            if (currentPhase == null || target == null)
                return;

            transform.position = ComputeDesiredPosition(currentPhase);
            transform.rotation = GetDesiredRotation(currentPhase);

            if (cam != null)
                cam.fieldOfView = currentPhase.fieldOfView;

            UpdateVolumeWeights(true);
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
