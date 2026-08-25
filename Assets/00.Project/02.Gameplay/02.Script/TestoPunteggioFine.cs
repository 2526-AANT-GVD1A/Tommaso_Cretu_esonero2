using UnityEngine;
using TMPro;

namespace ArcadeKart.Gameplay
{
    // Aggiorna un testo TMP del Menu_Fine con il totale degli oggetti
    // raccolti (KartCollectedStack.TotalCollected). Si attiva insieme al
    // Menu_Fine: in OnEnable legge il contatore e lo scrive nel testo col
    // formato scelto. Lo stack NON viene azzerato qui (il reset resta a
    // LevelManager.TornaAlMenu, come per il menu d'inizio), cosi' il
    // punteggio resta visibile finche' il giocatore non torna al menu.
    public class TestoPunteggioFine : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField, Tooltip("Testo TMP da aggiornare con il punteggio.")]
        private TMP_Text testo;

        [SerializeField, Tooltip("KartCollectedStack del giocatore. Se vuoto, lo cerca sul kart col tag Player.")]
        private KartCollectedStack stack;

        [Header("Formato")]
        [SerializeField, Tooltip("Formato del testo. {0} viene sostituito col numero di oggetti raccolti.")]
        private string formato = "Oggetti raccolti: {0}";

        private void OnEnable()
        {
            if (stack == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    stack = player.GetComponentInChildren<KartCollectedStack>();
            }

            if (stack == null)
            {
                Debug.LogWarning("[TestoPunteggioFine] Nessun KartCollectedStack trovato (tag Player).", this);
                return;
            }

            if (testo != null)
                testo.text = string.Format(formato, stack.TotalCollected);
            else
                Debug.LogWarning("[TestoPunteggioFine] Nessun testo TMP assegnato.", this);
        }
    }
}
