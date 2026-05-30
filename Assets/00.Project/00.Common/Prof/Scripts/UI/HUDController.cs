// =============================================================================
// HUDController
// -----------------------------------------------------------------------------
// COSA FA:
//   Aggiorna l'interfaccia di gioco (timer, contatore monete, velocimetro,
//   barra vita, messaggi pop-up) ascoltando gli eventi di TimerManager,
//   CollectibleRegistry, KartController, KartHealth.
//
// COME SI USA:
//   1) Crea un Canvas in scena, aggiungici i widget (TextMeshPro, Slider...).
//   2) Aggiungi questo componente al Canvas (o a un suo figlio).
//   3) Trascina i widget negli slot dell'Inspector. Lascia vuoti quelli che
//      non ti servono: l'HUDController non si lamenta, semplicemente non li
//      aggiorna.
//
// DA SAPERE:
//   - Si iscrive agli eventi in OnEnable, si desottoscrive in OnDisable.
//   - "Counter Format" usa {0} = raccolti, {1} = totali. Es: "Monete: {0}/{1}".
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcadeKart.Core;
using ArcadeKart.Behaviors;

namespace ArcadeKart.UI
{
    public class HUDController : MonoBehaviour
    {
        #region Inspector

        [Header("Timer")]
        [SerializeField, Tooltip("Etichetta TextMeshPro che mostra il tempo.")]
        private TextMeshProUGUI timerLabel;

        [Header("Contatore Collectible")]
        [SerializeField, Tooltip("Etichetta TextMeshPro che mostra il contatore (es. monete).")]
        private TextMeshProUGUI counterLabel;
        // PER MODIFICARE: cambia per contare un'altra categoria (es. "gem", "key").
        [SerializeField, Tooltip("Categoria di Collectible da mostrare nel contatore.")]
        private string counterCategory = "coin";
        [SerializeField, Tooltip("Formato del contatore. {0}=raccolti, {1}=totali. Es: 'Monete: {0}/{1}'.")]
        private string counterFormat = "Monete: {0}/{1}";

        [Header("Velocimetro / Vita")]
        [SerializeField, Tooltip("Slider opzionale: viene riempito da 0 a 1 in base alla velocita' attuale.")]
        private Slider speedometer;
        [SerializeField, Tooltip("Etichetta opzionale per la vita. Formato: {0}=corrente, {1}=massima.")]
        private TextMeshProUGUI healthLabel;
        [SerializeField, Tooltip("Formato della vita. Es: 'Vite: {0}/{1}'.")]
        private string healthFormat = "Vite: {0}/{1}";

        [Header("Riferimenti (auto-find se vuoti)")]
        [SerializeField, Tooltip("KartController da cui leggere la velocita'. Se vuoto, lo cerca per tag 'Player'.")]
        private KartController kart;
        [SerializeField, Tooltip("KartHealth da cui leggere la vita. Se vuoto, lo cerca per tag 'Player'.")]
        private KartHealth health;

        [Header("Messaggi pop-up")]
        [SerializeField, Tooltip("Pannello che si attiva/disattiva durante un messaggio.")]
        private GameObject messagePanel;
        [SerializeField, Tooltip("Etichetta TextMeshPro del messaggio.")]
        private TextMeshProUGUI messageText;

        #endregion

        #region Public API

        /// <summary>Shows a temporary message (e.g. "Boost!") for the given duration.</summary>
        /// <param name="text">Localized text to display.</param>
        /// <param name="duration">Seconds before the panel is hidden again.</param>
        public void ShowMessage(string text, float duration = 2f)
        {
            if (messagePanel == null || messageText == null) return;
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(MessageRoutine(text, duration));
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Auto-find difensivo: se lo studente dimentica di assegnare i
            // riferimenti, proviamo a recuperarli dal player taggato.
            if (kart == null || health == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null)
                {
                    if (kart == null) kart = p.GetComponentInParent<KartController>();
                    if (health == null) health = p.GetComponentInParent<KartHealth>();
                }
            }
            if (messagePanel != null) messagePanel.SetActive(false);
        }

        private void OnEnable()
        {
            // Ci iscriviamo agli eventi solo se i widget e i riferimenti esistono.
            // Cosi' un HUD "minimale" (solo timer) non rompe niente.
            TrySubscribeToTimer();
            CollectibleRegistry.OnCategoryChanged += HandleCategoryChanged;
            if (kart != null) kart.OnSpeedChanged.AddListener(HandleSpeedChanged);
            if (health != null) health.OnHealthChanged.AddListener(HandleHealthChanged);

            // Forziamo un primo refresh: a inizio scena gli eventi non sono ancora scattati.
            HandleCategoryChanged(counterCategory, CollectibleRegistry.CountCollected(counterCategory), CollectibleRegistry.CountTotal(counterCategory));
            if (health != null) HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        private void Start()
        {
            // Ritentativo: se in OnEnable TimerManager.Instance era ancora null
            // (race condition tra Awake di GameObject diversi, scene additive,
            // prefab instanziati, oggetti riattivati...), qui Unity garantisce
            // che tutti gli Awake siano gia' avvenuti.
            TrySubscribeToTimer();

            if (timerLabel != null && TimerManager.Instance == null)
                Debug.LogWarning("[HUDController] Timer Label assegnato ma nessun TimerManager in scena: il timer non si aggiornera'.", this);
        }

        private void OnDisable()
        {
            if (subscribedToTimer && TimerManager.Instance != null)
                TimerManager.Instance.OnTimeChanged.RemoveListener(HandleTimeChanged);
            subscribedToTimer = false;
            CollectibleRegistry.OnCategoryChanged -= HandleCategoryChanged;
            if (kart != null) kart.OnSpeedChanged.RemoveListener(HandleSpeedChanged);
            if (health != null) health.OnHealthChanged.RemoveListener(HandleHealthChanged);
        }

        private void TrySubscribeToTimer()
        {
            if (subscribedToTimer) return;
            if (TimerManager.Instance == null) return;
            TimerManager.Instance.OnTimeChanged.AddListener(HandleTimeChanged);
            subscribedToTimer = true;
            HandleTimeChanged(TimerManager.Instance.CurrentTime);
        }

        #endregion

        #region Internal

        private Coroutine messageRoutine;
        private bool subscribedToTimer;

        private void HandleTimeChanged(float t)
        {
            if (timerLabel != null) timerLabel.text = FormatTime(t);
        }

        private void HandleCategoryChanged(string cat, int collected, int total)
        {
            // Aggiorniamo il contatore solo se la categoria cambiata e' quella che ci interessa.
            if (counterLabel == null || cat != counterCategory) return;
            counterLabel.text = string.Format(counterFormat, collected, total);
        }

        private void HandleSpeedChanged(float speed)
        {
            if (speedometer == null || kart == null) return;
            // Normalizziamo la velocita' nell'intervallo 0..1 per lo slider.
            speedometer.value = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, kart.MaxSpeed));
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (healthLabel == null) return;
            healthLabel.text = string.Format(healthFormat, current, max);
        }

        private static string FormatTime(float t)
        {
            // Sotto il minuto mostriamo "SS.f", sopra mostriamo "MM:SS".
            if (t < 60f) return t.ToString("0.0") + "s";
            int minutes = Mathf.FloorToInt(t / 60f);
            int seconds = Mathf.FloorToInt(t - minutes * 60f);
            return string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        private IEnumerator MessageRoutine(string text, float duration)
        {
            messageText.text = text;
            messagePanel.SetActive(true);
            yield return new WaitForSeconds(duration);
            messagePanel.SetActive(false);
        }

        #endregion
    }
}
