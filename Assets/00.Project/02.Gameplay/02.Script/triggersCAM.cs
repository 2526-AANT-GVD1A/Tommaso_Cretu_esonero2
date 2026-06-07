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

            if (oneShot)
                used = true;
        }
    }
}
