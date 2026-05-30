// =============================================================================
// TimerManager
// -----------------------------------------------------------------------------
// COSA FA:
//   Gestisce il tempo del livello: puo' essere un cronometro che sale
//   (CountUp) o un timer che scende (CountDown). Espone eventi per HUD e
//   condizioni di vittoria/sconfitta.
//
// COME SI USA:
//   1) Crea un GameObject vuoto in scena, chiamalo "TimerManager" e aggiungi
//      questo componente.
//   2) Nell'Inspector scegli "Mode" (CountUp o CountDown) e "Start Time".
//   3) Se "Auto Start" e' true il timer parte appena entri in Play; altrimenti
//      chiama StartTimer() da un altro componente.
//
// DA SAPERE:
//   - C'e' un solo TimerManager per scena (singleton di scena, NO
//     DontDestroyOnLoad). Se ne metti due, il secondo si distrugge da solo.
//   - In CountDown, OnTimeUp viene invocato UNA SOLA VOLTA quando il tempo
//     arriva a 0.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Core
{
    public enum TimerMode { CountUp, CountDown }

    public class TimerManager : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: CountUp = cronometro che sale (tipo "tempo sul giro").
        // CountDown = countdown classico arcade ("hai 60 secondi per finire!").
        [SerializeField, Tooltip("Modalita' del timer: cronometro (CountUp) o countdown.")]
        private TimerMode mode = TimerMode.CountUp;

        // PER MODIFICARE: tempo iniziale in secondi.
        // Per CountUp lasciare 0. Per CountDown impostare la durata (es. 90).
        [SerializeField, Tooltip("Tempo iniziale in secondi (0 per CountUp, X per CountDown).")]
        private float startTime = 0f;

        [SerializeField, Tooltip("Se true, il timer parte automaticamente all'avvio della scena.")]
        private bool autoStart = true;

        [SerializeField, Tooltip("In CountDown, se true il timer si ferma a 0 invece di andare in negativo.")]
        private bool pauseOnEnd = true;

        #endregion

        #region Events

        // Invocato OGNI FRAME mentre il timer e' attivo. Parametro = secondi correnti.
        // L'HUD lo usa per aggiornare il testo a schermo.
        public UnityEvent<float> OnTimeChanged;

        // Invocato UNA SOLA VOLTA quando un CountDown raggiunge 0.
        // La LoseCondition di tipo TimerExpired e' un buon ascoltatore.
        public UnityEvent OnTimeUp;

        #endregion

        #region Public API

        /// <summary>Singleton-style accessor. Null if no TimerManager exists in scene.</summary>
        public static TimerManager Instance { get; private set; }

        /// <summary>Current time in seconds. Counts up or down based on Mode.</summary>
        public float CurrentTime { get; private set; }

        /// <summary>True while the timer is actively counting.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Active mode (CountUp or CountDown).</summary>
        public TimerMode Mode => mode;

        /// <summary>Starts (or restarts) the timer from the configured Start Time.</summary>
        public void StartTimer()
        {
            CurrentTime = startTime;
            timeUpFired = false;
            IsRunning = true;
            OnTimeChanged?.Invoke(CurrentTime);
        }

        /// <summary>Pauses without resetting current time.</summary>
        public void PauseTimer() { IsRunning = false; }

        /// <summary>Resumes from where it was paused.</summary>
        public void ResumeTimer() { IsRunning = true; }

        /// <summary>Stops the timer and resets it to Start Time.</summary>
        public void ResetTimer()
        {
            IsRunning = false;
            CurrentTime = startTime;
            timeUpFired = false;
            OnTimeChanged?.Invoke(CurrentTime);
        }

        /// <summary>Adds seconds to the current time (works in both modes).</summary>
        /// <param name="seconds">Positive number of seconds to add.</param>
        public void AddTime(float seconds)
        {
            CurrentTime += seconds;
            OnTimeChanged?.Invoke(CurrentTime);
        }

        /// <summary>Subtracts seconds from the current time. Clamped at 0 if pauseOnEnd is true.</summary>
        /// <param name="seconds">Positive number of seconds to subtract.</param>
        public void SubtractTime(float seconds)
        {
            CurrentTime -= seconds;
            if (pauseOnEnd && CurrentTime < 0f) CurrentTime = 0f;
            OnTimeChanged?.Invoke(CurrentTime);
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Singleton di scena difensivo: se ne esiste gia' un altro, ci
            // distruggiamo. Cosi' lo studente non rompe niente per sbaglio.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TimerManager] Esiste gia' un TimerManager in scena. Distruggo il duplicato su " + name + ".", this);
                Destroy(this);
                return;
            }
            Instance = this;
            CurrentTime = startTime;
        }

        private void OnDestroy()
        {
            // Liberiamo lo slot Instance solo se eravamo davvero noi.
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (autoStart) StartTimer();
        }

        private void Update()
        {
            if (!IsRunning) return;

            // CountUp accumula tempo, CountDown lo sottrae.
            if (mode == TimerMode.CountUp) CurrentTime += Time.deltaTime;
            else CurrentTime -= Time.deltaTime;

            // In CountDown, se siamo arrivati a 0 spariamo OnTimeUp UNA volta sola
            // e (se richiesto) fermiamo il timer evitando valori negativi.
            if (mode == TimerMode.CountDown && CurrentTime <= 0f && !timeUpFired)
            {
                if (pauseOnEnd) { CurrentTime = 0f; IsRunning = false; }
                timeUpFired = true;
                OnTimeChanged?.Invoke(CurrentTime);
                OnTimeUp?.Invoke();
                return;
            }

            OnTimeChanged?.Invoke(CurrentTime);
        }

        #endregion

        #region Internal

        // Flag che impedisce di invocare OnTimeUp piu' di una volta.
        private bool timeUpFired;

        #endregion
    }
}
