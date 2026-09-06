using UnityEngine;
using TMPro;

namespace ArcadeKart.Gameplay
{
    // Aggiorna un testo TMP del Menu_Fine con il punteggio netto degli
    // oggetti (KartCollectedStack.TotalCollected: raccolti meno quelli
    // rubati dal kart NPC). Si attiva insieme al
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
                // Preferiamo lo stack tenuto dal LevelManager: e' quello
                // risolto dal kart del giocatore e resta identico anche se
                // in scena il tag "Player" e' appiccicato su altri oggetti
                // (figli del kart, kart NPC) che renderebbero ambiguo il
                // cerca-per-tag.
                LevelManager levelManager = FindFirstObjectByType<LevelManager>();
                if (levelManager != null)
                    stack = levelManager.Stack;

                // Ultima spiaggia: cerca per tag. Puo' restituire un oggetto
                // senza KartCollectedStack: in quel caso il warning qui sotto
                // lo segnala in console.
                if (stack == null)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null)
                        stack = player.GetComponentInChildren<KartCollectedStack>();
                }
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
