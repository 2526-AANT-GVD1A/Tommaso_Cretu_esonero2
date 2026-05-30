// =============================================================================
// TriggerZone
// -----------------------------------------------------------------------------
// COSA FA:
//   Volume invisibile (o visibile, dipende da te) che applica un effetto al
//   kart quando lo attraversa. Un solo componente, tante varianti: Boost,
//   Slow, Teleport, Damage, BonusTime, MalusTime, ActivateTarget, PlaySoundOnly.
//
// COME SI USA:
//   1) Crea un GameObject con un Collider impostato come "Is Trigger".
//   2) Aggiungi questo componente. Scegli "Effect" dal menu a tendina.
//   3) Compila SOLO i campi della sezione [Header] relativa all'effetto scelto.
//      Es. per "Boost" servono Magnitude e Duration; per "Teleport" il target.
//
// DA SAPERE:
//   - Reagisce a oggetti con tag "Player" (puoi disattivare con "Require Kart").
//   - "One Shot": il trigger sparisce dopo il primo uso.
//   - "Cooldown": tempo minimo tra due attivazioni consecutive.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using ArcadeKart.Core;

namespace ArcadeKart.Behaviors
{
    public enum TriggerEffect { None, Boost, Slow, Teleport, Damage, BonusTime, MalusTime, ActivateTarget, PlaySoundOnly }

    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        #region Inspector

        [Header("Effect")]
        // PER MODIFICARE: cambia il tipo di effetto. Compila i campi sotto in base alla scelta.
        [SerializeField, Tooltip("Tipo di effetto applicato al kart.")] private TriggerEffect effect = TriggerEffect.Boost;
        // PER MODIFICARE: oneShot = true se vuoi che il pickup sparisca dopo un solo uso (tipo moneta speciale).
        // oneShot = false per zone permanenti (es. boost ricorrente sul rettilineo).
        [SerializeField, Tooltip("Se true, il trigger funziona una sola volta.")] private bool oneShot = false;
        // PER MODIFICARE: cooldown evita che il trigger si riattivi continuamente se attraversi lentamente.
        // Valori tipici: 0.5s per boost, 1-2s per zone con effetti pesanti.
        [SerializeField, Tooltip("Secondi di pausa tra attivazioni consecutive.")] private float cooldown = 0.5f;
        [SerializeField, Tooltip("Se true, reagisce solo al GameObject con tag 'Player'.")] private bool requireKart = true;

        [Header("Boost / Slow")]
        // PER MODIFICARE: 1.5 = +50% velocita'. 2.0 = doppia velocita'.
        [SerializeField, Tooltip("Moltiplicatore di velocita' del Boost (1.5 = +50%).")] private float boostMagnitude = 1.5f;
        // PER MODIFICARE: durata in secondi del boost. Valori tipici: 1-3s arcade, 5+ per boost "potenti".
        [SerializeField, Tooltip("Durata del Boost in secondi.")] private float boostDuration = 2f;
        // PER MODIFICARE: 0.5 = meta' velocita'. 0.3 = lentissimo (zona sabbiosa). 0.8 = leggero rallentamento.
        [SerializeField, Tooltip("Fattore di Slow (0.5 = meta' velocita').")]
        [Range(0f, 1f)] private float slowFactor = 0.5f;
        // PER MODIFICARE: durata in secondi del rallentamento.
        [SerializeField, Tooltip("Durata dello Slow in secondi.")] private float slowDuration = 2f;

        [Header("Teleport")]
        [SerializeField, Tooltip("Punto di destinazione del teleport.")] private Transform teleportTarget;

        [Header("Damage")]
        // PER MODIFICARE: quanti punti vita togliere. Con KartHealth default (3 vite), 1 = 3 colpi prima del game over.
        [SerializeField, Tooltip("Quanti punti vita togliere al kart.")] private int damageAmount = 1;

        [Header("Time")]
        // PER MODIFICARE: secondi regalati al timer in CountDown. Valori tipici: 3-5s per pickup minori, 10s per pickup speciali.
        [SerializeField, Tooltip("Secondi da AGGIUNGERE al timer (BonusTime).")] private float timeBonus = 5f;
        // PER MODIFICARE: secondi sottratti al timer. Usalo per malus aggressivi: 5-10s.
        [SerializeField, Tooltip("Secondi da SOTTRARRE dal timer (MalusTime).")] private float timeMalus = 5f;

        [Header("Activate Target")]
        [SerializeField, Tooltip("GameObject da attivare (SetActive true).")] private GameObject targetToActivate;

        [Header("Feedback")]
        [SerializeField, Tooltip("Suono riprodotto all'attivazione.")] private AudioClip sfxClip;
        [SerializeField, Tooltip("Particellare instanziato all'attivazione.")] private GameObject vfxPrefab;

        #endregion

        #region Events
        // Evento "qualcuno ha attivato il trigger". Parametro = chi (di solito il kart).
        public UnityEvent<GameObject> OnTriggered;
        #endregion

        #region Unity callbacks

        private void Reset()
        {
            // Quando lo studente aggiunge il componente in Editor, il collider
            // viene messo automaticamente come trigger: un errore in meno.
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Filtri base: cooldown attivo o filtro tag non passato → ignoriamo.
            if (Time.time < cooldownUntil) return;
            if (requireKart && !other.CompareTag("Player")) return;

            // Cerchiamo i componenti sul kart (sul GameObject del Collider o sul padre).
            KartController kart = other.GetComponentInParent<KartController>();
            KartHealth health = other.GetComponentInParent<KartHealth>();

            ApplyEffect(kart, health);

            if (sfxClip != null) AudioSource.PlayClipAtPoint(sfxClip, transform.position);
            if (vfxPrefab != null) Instantiate(vfxPrefab, transform.position, Quaternion.identity);
            OnTriggered?.Invoke(other.gameObject);

            cooldownUntil = Time.time + cooldown;
            // OneShot: spegniamo il GameObject cosi' non si riattiva.
            if (oneShot) gameObject.SetActive(false);
        }

        #endregion

        #region Internal

        // Time.time fino al quale il trigger e' "in pausa" (cooldown).
        private float cooldownUntil;

        private void ApplyEffect(KartController kart, KartHealth health)
        {
            switch (effect)
            {
                case TriggerEffect.None: break;
                case TriggerEffect.Boost:
                    if (kart != null) kart.ApplyBoost(boostMagnitude, boostDuration);
                    else WarnMissing("KartController", "Boost");
                    break;
                case TriggerEffect.Slow:
                    if (kart != null) kart.ApplySlow(slowFactor, slowDuration);
                    else WarnMissing("KartController", "Slow");
                    break;
                case TriggerEffect.Teleport:
                    if (kart != null && teleportTarget != null) kart.Teleport(teleportTarget.position, teleportTarget.rotation);
                    else WarnMissing(teleportTarget == null ? "Teleport Target" : "KartController", "Teleport");
                    break;
                case TriggerEffect.Damage:
                    if (health != null) health.TakeDamage(damageAmount);
                    else WarnMissing("KartHealth", "Damage");
                    break;
                case TriggerEffect.BonusTime:
                    if (TimerManager.Instance != null) TimerManager.Instance.AddTime(timeBonus);
                    else WarnMissing("TimerManager", "BonusTime");
                    break;
                case TriggerEffect.MalusTime:
                    if (TimerManager.Instance != null) TimerManager.Instance.SubtractTime(timeMalus);
                    else WarnMissing("TimerManager", "MalusTime");
                    break;
                case TriggerEffect.ActivateTarget:
                    if (targetToActivate != null) targetToActivate.SetActive(true);
                    else WarnMissing("Target To Activate", "ActivateTarget");
                    break;
                case TriggerEffect.PlaySoundOnly:
                    // Il suono parte sotto, dopo lo switch. Qui non facciamo altro.
                    break;
            }
        }

        private void WarnMissing(string what, string effectName)
        {
            Debug.LogWarning("[TriggerZone] Manca '" + what + "' per l'effetto " + effectName + " su " + name + ".", this);
        }

        #endregion
    }
}
