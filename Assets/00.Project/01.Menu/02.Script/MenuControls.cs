using UnityEngine;
using ArcadeKart.Core;
using ArcadeKart.Gameplay;

namespace ArcadeKart.Menu
{
    // Collega lo stato attivo dell'oggetto del menu ai controlli del kart.
    // Da mettere sull'oggetto che rappresenta il menu (es. "Menu", figlio del
    // Canvas): NON sul Canvas intero, cosi' i fratelli sotto lo stesso Canvas
    // (effetti visivi, HUD, ecc.) restano attivi quando il menu si chiude.
    // Quando l'oggetto si attiva i controlli del kart si spengono e il kart
    // si freezea (vedi KartController.SetControlsEnabled); quando si
    // disattiva i controlli riprendono. Gestisce anche il cursore del mouse:
    // libero e visibile col menu aperto, ripristinato alla chiusura.
    public class MenuControls : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField, Tooltip("KartController del giocatore. Se vuoto, cerca il primo oggetto col tag Player.")]
        private KartController kart;

        private bool savedCursorVisible;
        private CursorLockMode savedCursorLockMode;

        private void OnEnable()
        {
            savedCursorVisible = Cursor.visible;
            savedCursorLockMode = Cursor.lockState;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (kart == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    kart = player.GetComponent<KartController>();
            }

            if (kart == null)
            {
                Debug.LogWarning("[MenuControls] Nessun KartController trovato (tag Player).", this);
                return;
            }

            kart.SetControlsEnabled(false);

            // Ogni volta che il menu torna attivo la partita "ricomincia":
            // azzeriamo il contatore totale degli oggetti raccolti e
            // svuotiamo anche la torre visibile sul kart (il kart NON viene
            // distrutto, e' solo respawnato da LevelManager, quindi senza
            // questo reset la vecchia pila resterebbe in spalla al rientro).
            KartCollectedStack stack = kart.GetComponent<KartCollectedStack>();
            if (stack != null)
            {
                stack.ResetTotal();
                stack.ClearAll();
            }
        }

        private void OnDisable()
        {
            Cursor.visible = savedCursorVisible;
            Cursor.lockState = savedCursorLockMode;

            if (kart != null)
                kart.SetControlsEnabled(true);
        }
    }
}
