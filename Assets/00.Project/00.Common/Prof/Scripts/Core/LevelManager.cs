// =============================================================================
// LevelManager
// -----------------------------------------------------------------------------
// COSA FA:
//   Coordina la vittoria e la sconfitta di un livello. Tiene una lista di
//   WinCondition (devono essere TUTTE soddisfatte per vincere) e una lista
//   di LoseCondition (basta UNA per perdere). Gestisce restart e cambio scena.
//
// COME SI USA:
//   1) Crea un GameObject "LevelManager" in scena, aggiungi questo componente.
//   2) Trascina nelle liste "Win Conditions" e "Lose Conditions" i GameObject
//      con il componente WinCondition / LoseCondition gia' configurato.
//   3) (Opzionale) Imposta "Next Scene Name" per il bottone "Avanti" della
//      EndScreen e "Player Start" per spawnare/riportare il kart all'inizio.
//
// DA SAPERE:
//   - C'e' un solo LevelManager per scena (singleton di scena).
//   - Una volta vinto/perso, le condizioni non vengono piu' valutate.
//   - PER LEZIONE FUTURA: dentro TriggerWin() puoi salvare il best time o le
//     monete con PlayerPrefs (lo faremo nelle prossime lezioni).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using ArcadeKart.Behaviors;

namespace ArcadeKart.Core
{
    public class LevelManager : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: il nome compare nella schermata di fine livello e nelle scritte di stato.
        // Usalo per dare identita' al livello: "Tutorial", "Pista 1 - Spiaggia", "Boss Run".
        [SerializeField, Tooltip("Nome leggibile del livello (es. 'Pista 1 - Spiaggia').")]
        private string levelName = "Livello senza nome";

        // PER MODIFICARE: per ora abbiamo una sola scena. Quando creerai altri livelli,
        // scrivi qui il nome ESATTO (case-sensitive) della scena successiva.
        [SerializeField, Tooltip("Nome ESATTO della scena da caricare quando vinci. Lascia vuoto per disabilitare 'Avanti'.")]
        private string nextSceneName;

        // PER MODIFICARE: aggiungi/togli le WinCondition dalla lista per definire le regole del tuo livello.
        // Servono TUTTE soddisfatte per vincere (logica AND).
        [SerializeField, Tooltip("Tutte queste devono essere true per VINCERE.")]
        private List<WinCondition> winConditions = new List<WinCondition>();

        // PER MODIFICARE: aggiungi LoseCondition multiple per offrire piu' modi di "perdere"
        // (es. timer scaduto OR caduta dalla pista). Logica OR.
        [SerializeField, Tooltip("Anche solo UNA di queste a true significa SCONFITTA.")]
        private List<LoseCondition> loseConditions = new List<LoseCondition>();

        [SerializeField, Tooltip("Pannello UI da attivare alla vittoria (opzionale).")]
        private GameObject winOverlay;

        [SerializeField, Tooltip("Pannello UI da attivare alla sconfitta (opzionale).")]
        private GameObject loseOverlay;

        [SerializeField, Tooltip("Punto di partenza/respawn del kart (opzionale).")]
        private Transform playerStart;

        #endregion

        #region Events

        public UnityEvent OnLevelStart;
        public UnityEvent OnLevelWon;
        public UnityEvent OnLevelLost;

        #endregion

        #region Public API

        /// <summary>Singleton-style accessor. Null if no LevelManager exists in scene.</summary>
        public static LevelManager Instance { get; private set; }

        /// <summary>Time.time at which the current level started counting.</summary>
        public float LevelStartTime { get; private set; }

        /// <summary>True after TriggerWin or TriggerLose has fired (level frozen).</summary>
        public bool IsLevelOver { get; private set; }

        /// <summary>Display name shown by the EndScreen.</summary>
        public string LevelName => levelName;

        /// <summary>Returns the configured next scene (may be empty).</summary>
        public string NextSceneName => nextSceneName;

        /// <summary>Resets win/lose flags and notifies listeners. Called automatically on Start.</summary>
        public void StartLevel()
        {
            IsLevelOver = false;
            LevelStartTime = Time.time;
            if (winOverlay != null) winOverlay.SetActive(false);
            if (loseOverlay != null) loseOverlay.SetActive(false);
            OnLevelStart?.Invoke();
        }

        /// <summary>Forces a victory. Safe to call multiple times (no-op after the first).</summary>
        public void TriggerWin()
        {
            if (IsLevelOver) return;
            IsLevelOver = true;
            // PER LEZIONE FUTURA: salvare best time / coin con PlayerPrefs qui.
            if (winOverlay != null) winOverlay.SetActive(true);
            OnLevelWon?.Invoke();
        }

        /// <summary>Forces a defeat. Safe to call multiple times (no-op after the first).</summary>
        public void TriggerLose()
        {
            if (IsLevelOver) return;
            IsLevelOver = true;
            if (loseOverlay != null) loseOverlay.SetActive(true);
            OnLevelLost?.Invoke();
        }

        /// <summary>Reloads the current scene from scratch.</summary>
        public void RestartLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Loads the configured Next Scene if any. Otherwise logs a warning.</summary>
        public void LoadNextLevel()
        {
            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogWarning("[LevelManager] Next Scene Name vuoto: non posso caricare il livello successivo.", this);
                return;
            }
            SceneManager.LoadScene(nextSceneName);
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Singleton di scena: se ne esiste gia' un altro, ci distruggiamo.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LevelManager] Esiste gia' un LevelManager in scena. Distruggo il duplicato su " + name + ".", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // (Opzionale) sposta il player allo start. Lo cerchiamo per tag.
            if (playerStart != null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    KartController kart = player.GetComponent<KartController>();
                    if (kart != null) kart.RespawnAt(playerStart);
                    else player.transform.SetPositionAndRotation(playerStart.position, playerStart.rotation);
                }
            }
            StartLevel();
        }

        private void Update()
        {
            if (IsLevelOver) return;

            // Sconfitta ha priorita': controlliamo prima quella.
            for (int i = 0; i < loseConditions.Count; i++)
            {
                if (loseConditions[i] != null && loseConditions[i].IsSatisfied) { TriggerLose(); return; }
            }
            // Vittoria solo se TUTTE le condizioni sono soddisfatte e ce n'e' almeno una.
            if (winConditions.Count == 0) return;
            for (int i = 0; i < winConditions.Count; i++)
            {
                if (winConditions[i] == null || !winConditions[i].IsSatisfied) return;
            }
            TriggerWin();
        }

        #endregion
    }
}
