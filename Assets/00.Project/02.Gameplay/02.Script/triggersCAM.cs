using UnityEngine;
using ArcadeKart.Utility;

namespace ArcadeKart.Utility
{
    public class CameraPhaseTrigger : MonoBehaviour
    {
        [Header("Phase")]
        [SerializeField, Tooltip("ID della fase da attivare quando il kart entra nel trigger.")]
        private string phaseId = "Default";

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

            if (targetCamera == null)
            {
                Debug.LogWarning("[CameraPhaseTrigger] Nessuna PhasedFollowCamera trovata.", this);
                return;
            }

            bool changed = targetCamera.SetPhase(phaseId);
            if (!changed)
                return;

            ApplyToggleState();

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
    }
}
