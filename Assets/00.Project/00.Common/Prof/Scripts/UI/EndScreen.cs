// =============================================================================
// EndScreen
// -----------------------------------------------------------------------------
// COSA FA:
//   Pannello di fine livello (Vittoria o Sconfitta). Mostra titolo, tempo
//   finale e monete raccolte. Espone tre metodi (Riprova / Avanti / Menu)
//   da collegare ai bottoni dell'Inspector.
//
// COME SI USA:
//   1) Crea un Canvas in scena. Mettici un GameObject "EndScreen" SEMPRE
//      ATTIVO con questo componente sopra.
//   2) Sotto "EndScreen" crea un GameObject "Panel" (l'effettiva UI di fine
//      livello), trascinalo nello slot "Panel" e LASCIALO disattivato in
//      Inspector: lo accenderemo noi al momento giusto.
//   3) Sui bottoni della UI, nell'evento OnClick, chiama OnRetryClicked /
//      OnNextClicked / OnMenuClicked di questo componente.
//
// DA SAPERE:
//   - Si auto-iscrive a LevelManager.OnLevelWon/OnLevelLost: appena il livello
//     finisce, il pannello si accende da solo con i dati corretti.
//   - Il bottone "Avanti" si disabilita se il LevelManager non ha
//     "Next Scene Name" configurato.
//   - PER LEZIONE FUTURA: dentro Show() puoi salvare con PlayerPrefs il best time.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ArcadeKart.Core;
using ArcadeKart.Behaviors;

namespace ArcadeKart.UI
{
    public class EndScreen : MonoBehaviour
    {
        #region Inspector

        [Header("Pannello")]
        [SerializeField, Tooltip("GameObject del pannello da mostrare/nascondere. DEVE essere un figlio inizialmente disattivo.")]
        private GameObject panel;

        [Header("Widget")]
        [SerializeField, Tooltip("Etichetta con il titolo (Vittoria / Sconfitta).")]
        private TextMeshProUGUI titleLabel;
        [SerializeField, Tooltip("Etichetta con il tempo finale.")]
        private TextMeshProUGUI timeLabel;
        [SerializeField, Tooltip("Etichetta con le monete raccolte.")]
        private TextMeshProUGUI coinsLabel;
        [SerializeField, Tooltip("Bottone 'Riprova'.")]
        private Button retryButton;
        [SerializeField, Tooltip("Bottone 'Avanti' (livello successivo).")]
        private Button nextButton;
        [SerializeField, Tooltip("Bottone 'Menu' (torna al menu principale).")]
        private Button menuButton;

        [Header("Configurazione")]
        // PER MODIFICARE: se nel tuo gioco i collectible si chiamano diversamente (es. "gem", "key"),
        // cambia qui per mostrare il riepilogo corretto.
        [SerializeField, Tooltip("Categoria di Collectible da mostrare nel riepilogo.")]
        private string coinsCategory = "coin";
        // PER MODIFICARE: per ora il menu principale non esiste (lezione futura).
        // Lascia il valore di default oppure metti il nome della scena se la crei tu.
        [SerializeField, Tooltip("Nome ESATTO della scena del menu principale.")]
        private string mainMenuSceneName = "01_MainMenu";
        // PER MODIFICARE: personalizza i testi della schermata di fine livello per dare carattere
        // al tuo gioco. Es. "VITTORIA!", "Hai battuto il record!", "Game Over", "Riprova...".
        [SerializeField, Tooltip("Testo del titolo in caso di vittoria.")]
        private string winTitle = "Hai Vinto!";
        [SerializeField, Tooltip("Testo del titolo in caso di sconfitta.")]
        private string loseTitle = "Hai Perso";

        [Header("Riferimenti (auto-find se vuoti)")]
        [SerializeField, Tooltip("LevelManager da cui prendere stato e prossima scena.")]
        private LevelManager levelManager;

        #endregion

        #region Public API

        /// <summary>Shows the panel populated for either win or lose state.</summary>
        public void Show(bool isWin)
        {
            if (titleLabel != null) titleLabel.text = isWin ? winTitle : loseTitle;
            if (timeLabel != null && TimerManager.Instance != null)
                timeLabel.text = "Tempo: " + TimerManager.Instance.CurrentTime.ToString("0.0") + "s";
            if (coinsLabel != null)
                coinsLabel.text = "Monete: " + CollectibleRegistry.CountCollected(coinsCategory) + "/" + CollectibleRegistry.CountTotal(coinsCategory);

            // PER LEZIONE FUTURA: salvare best time / coin con PlayerPrefs qui.

            // "Avanti" ha senso solo dopo una vittoria E se sappiamo dove andare.
            if (nextButton != null)
                nextButton.interactable = isWin && levelManager != null && !string.IsNullOrEmpty(levelManager.NextSceneName);

            if (panel != null) panel.SetActive(true);
            else Debug.LogWarning("[EndScreen] 'Panel' non assegnato: non posso mostrare niente.", this);
        }

        /// <summary>Hides the panel.</summary>
        public void Hide() { if (panel != null) panel.SetActive(false); }

        /// <summary>Wired to the Retry button. Reloads the current scene.</summary>
        public void OnRetryClicked()
        {
            if (levelManager != null) levelManager.RestartLevel();
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Wired to the Next button. Loads the configured next scene.</summary>
        public void OnNextClicked()
        {
            if (levelManager != null) levelManager.LoadNextLevel();
            else Debug.LogWarning("[EndScreen] LevelManager non disponibile: non posso andare avanti.", this);
        }

        /// <summary>Wired to the Menu button. Loads the main menu scene by name.</summary>
        public void OnMenuClicked()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                Debug.LogWarning("[EndScreen] Main Menu Scene Name non impostato.", this);
                return;
            }
            SceneManager.LoadScene(mainMenuSceneName);
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Cerchiamo il LevelManager se non assegnato in Inspector.
            if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
            // Il pannello deve partire spento. Lo facciamo qui per sicurezza,
            // ma puoi anche lasciarlo gia' disattivato in Inspector.
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            // Auto-iscrizione: il LevelManager ci avvisa di vittoria/sconfitta.
            // Funziona perche' QUESTO GameObject e' sempre attivo (mentre il
            // 'panel' separato puo' partire disattivato).
            if (levelManager != null)
            {
                levelManager.OnLevelWon.AddListener(ShowWin);
                levelManager.OnLevelLost.AddListener(ShowLose);
            }
        }

        private void OnDisable()
        {
            if (levelManager != null)
            {
                levelManager.OnLevelWon.RemoveListener(ShowWin);
                levelManager.OnLevelLost.RemoveListener(ShowLose);
            }
        }

        #endregion

        #region Internal

        // Wrapper senza parametri per gli eventi UnityEvent del LevelManager.
        private void ShowWin() { Show(true); }
        private void ShowLose() { Show(false); }

        #endregion
    }
}
