using UnityEngine;

namespace ArcadeKart.Gameplay
{
    // Trigger di fine livello: quando il kart lo attraversa, comunica al
    // LevelManager che la partita e' finita (il LevelManager attiva il
    // Menu_Fine, il cui MenuControls spenge i controlli del kart e libera
    // il cursore). Stesso stile del CameraPhaseTrigger: filtro per tag,
    // funzionamento una tantua (oneShot). Il flag "used" si azzera in
    // OnEnable perche' il trigger vive sotto la radice del livello, che
    // viene disattivata/riattivata ad ogni partita: cosi' funziona anche
    // alla seconda volta che si gioca lo stesso livello.
    public class TriggerFineLivello : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField, Tooltip("LevelManager da avvisare. Se vuoto, lo cerca in scena in Awake.")]
        private LevelManager levelManager;

        [Header("Filtering")]
        [SerializeField, Tooltip("Se assegnato, il trigger reagisce solo a questo tag. Consigliato: Player.")]
        private string requiredTag = "Player";

        [SerializeField, Tooltip("Se true, il trigger funziona una sola volta per partita.")]
        private bool oneShot = true;

        private bool used;

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        private void Awake()
        {
            if (levelManager == null)
                levelManager = FindFirstObjectByType<LevelManager>();
        }

        private void OnEnable()
        // La radice del livello viene riattivata ad ogni partita: il trigger
        // riceve OnEnable e puo' servire di nuovo.
        {
            used = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (used && oneShot)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            if (levelManager == null)
            {
                Debug.LogWarning("[TriggerFineLivello] Nessun LevelManager assegnato/trovato.", this);
                return;
            }

            levelManager.TerminaLivello();

            if (oneShot)
                used = true;
        }
    }
}
