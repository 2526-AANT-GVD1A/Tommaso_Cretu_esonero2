using UnityEngine;
using ArcadeKart.Utility;

namespace ArcadeKart.Utility
{
    public class CameraPhaseTrigger : MonoBehaviour
    {
        [Header("Phase - Ingresso")]
        [SerializeField, Tooltip("ID della fase da attivare quando il kart entra nel trigger. Vuoto = il trigger non cambia la fase camera, si limita ad attivare/disattivare gli oggetti.")]
        private string phaseId = "Default";

        [SerializeField, Tooltip("Smooth = transizione fluida col damping. Snap = salto immediato sulla nuova inquadratura.")]
        private PhasedFollowCamera.TransitionMode transitionMode = PhasedFollowCamera.TransitionMode.Smooth;

        [Header("Phase - Uscita")]
        [SerializeField, Tooltip("ID della fase da attivare quando il kart esce dal trigger. Vuoto = all'uscita non cambia la fase.")]
        private string exitPhaseId = "";

        [SerializeField, Tooltip("Smooth = transizione fluida col damping. Snap = salto immediato sulla nuova inquadratura.")]
        private PhasedFollowCamera.TransitionMode exitTransitionMode = PhasedFollowCamera.TransitionMode.Smooth;

        [Header("Filtering")]
        [SerializeField, Tooltip("Se assegnato, il trigger reagisce solo a questo tag. Consigliato: Player.")]
        private string requiredTag = "Player";

        [SerializeField, Tooltip("Se true, il trigger funziona una sola volta.")]
        private bool oneShot = false;

        [Header("References")]
        [SerializeField, Tooltip("Riferimento esplicito alla camera. Se vuoto, cerca Camera.main.")]
        private PhasedFollowCamera targetCamera;

        [Header("Oggetti da attivare/disattivare all'ingresso")]
        [SerializeField, Tooltip("Oggetti (es. elementi del Canvas) da attivare quando questo trigger viene toccato.")]
        private GameObject[] activateOnEnter = System.Array.Empty<GameObject>();

        [SerializeField, Tooltip("Oggetti (es. elementi del Canvas) da disattivare quando questo trigger viene toccato.")]
        private GameObject[] deactivateOnEnter = System.Array.Empty<GameObject>();

        [Header("Oggetti da attivare/disattivare all'uscita")]
        [SerializeField, Tooltip("Oggetti (es. elementi del Canvas) da attivare quando il kart esce da questo trigger.")]
        private GameObject[] activateOnExit = System.Array.Empty<GameObject>();

        [SerializeField, Tooltip("Oggetti (es. elementi del Canvas) da disattivare quando il kart esce da questo trigger.")]
        private GameObject[] deactivateOnExit = System.Array.Empty<GameObject>();

        private bool used;

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            if (targetCamera == null && Camera.main != null)
                targetCamera = Camera.main.GetComponent<PhasedFollowCamera>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (used && oneShot)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            // Phase Id vuoto: il trigger lavora solo sugli oggetti (modelli,
            // parti di livello, canvas) senza toccare la fase camera.
            bool hasPhase = !string.IsNullOrWhiteSpace(phaseId);

            if (hasPhase)
            {
                if (targetCamera == null)
                {
                    Debug.LogWarning("[CameraPhaseTrigger] Nessuna PhasedFollowCamera trovata.", this);
                    return;
                }

                bool changed = targetCamera.SetPhase(phaseId, transitionMode);
                if (!changed)
                    return;
            }

            ApplyToggleState();

            if (oneShot)
                used = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (used && oneShot)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            ApplyExitToggleState();

            // Phase Id vuoto: all'uscita il trigger lavora solo sugli oggetti,
            // senza toccare la fase camera.
            if (string.IsNullOrWhiteSpace(exitPhaseId))
            {
                if (oneShot)
                    used = true;
                return;
            }

            if (targetCamera == null)
            {
                Debug.LogWarning("[CameraPhaseTrigger] Nessuna PhasedFollowCamera trovata.", this);
                return;
            }

            targetCamera.SetPhase(exitPhaseId, exitTransitionMode);

            if (oneShot)
                used = true;
        }

        private void ApplyToggleState()
        {
            for (int i = 0; i < activateOnEnter.Length; i++)
            {
                if (activateOnEnter[i] != null)
                    activateOnEnter[i].SetActive(true);
            }

            for (int i = 0; i < deactivateOnEnter.Length; i++)
            {
                if (deactivateOnEnter[i] != null)
                    deactivateOnEnter[i].SetActive(false);
            }
        }

        private void ApplyExitToggleState()
        {
            for (int i = 0; i < activateOnExit.Length; i++)
            {
                if (activateOnExit[i] != null)
                    activateOnExit[i].SetActive(true);
            }

            for (int i = 0; i < deactivateOnExit.Length; i++)
            {
                if (deactivateOnExit[i] != null)
                    deactivateOnExit[i].SetActive(false);
            }
        }
    }
}
