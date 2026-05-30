// =============================================================================
// KartHealth
// -----------------------------------------------------------------------------
// COSA FA:
//   Tiene traccia dei punti vita del kart e gestisce i danni. Espone eventi
//   UnityEvent (OnDamaged, OnHealthChanged, OnDeath) a cui possono agganciarsi
//   HUD, particellari, suoni o la LoseCondition di tipo HealthDepleted.
//
// COME SI USA:
//   1) Aggiungi il componente sullo stesso GameObject del kart.
//   2) Imposta "Max Health" (di default 3 vite, come un classico arcade).
//   3) Le TriggerZone con effetto "Damage" chiamano automaticamente
//      TakeDamage() su questo componente quando il kart le attraversa.
//
// DA SAPERE:
//   - Dopo un danno c'e' un breve periodo di invulnerabilita' (default 1s)
//     per evitare che lo stesso ostacolo dreni la vita in pochi frame.
//   - Quando la vita arriva a 0 invochiamo OnDeath UNA SOLA VOLTA.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Core
{
    public class KartHealth : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: cambia questo valore per rendere il kart piu' o meno
        // resistente. 1 = "one-hit", 3 = arcade classico, 10 = quasi indistruttibile.
        [SerializeField, Tooltip("Numero massimo di vite/colpi che il kart puo' incassare.")]
        private int maxHealth = 3;

        // PER MODIFICARE: tempo (in secondi) dopo un danno in cui il kart non
        // puo' subire altri danni. Utile per evitare che un singolo ostacolo
        // tolga tutta la vita in un attimo.
        [SerializeField, Tooltip("Secondi di invulnerabilita' dopo aver subito un danno.")]
        private float invulnerabilityAfterHit = 1f;

        #endregion

        #region Events

        // Evento generico cambio vita: parametri = (currentHealth, maxHealth).
        // L'HUD lo usa per aggiornare il numero a schermo o le icone "cuore".
        public UnityEvent<int, int> OnHealthChanged;

        // Evento "ho appena preso danno": utile per scuotere la camera o
        // far partire un effetto particellare.
        public UnityEvent OnDamaged;

        // Evento "sono morto": invocato UNA SOLA VOLTA quando la vita arriva a 0.
        // La LoseCondition di tipo HealthDepleted lo usa per scatenare la sconfitta.
        public UnityEvent OnDeath;

        #endregion

        #region Public API

        /// <summary>Current health points. Clamped between 0 and MaxHealth.</summary>
        public int CurrentHealth { get; private set; }

        /// <summary>Maximum health configured in the Inspector.</summary>
        public int MaxHealth => maxHealth;

        /// <summary>True when the kart has been killed at least once.</summary>
        public bool IsDead => CurrentHealth <= 0;

        /// <summary>
        /// Removes a number of health points. No-op if currently invulnerable
        /// or already dead. Triggers OnDamaged and (if it kills) OnDeath.
        /// </summary>
        /// <param name="amount">Damage points to subtract. Negative values are ignored.</param>
        public void TakeDamage(int amount)
        {
            if (amount <= 0)
                return;
            if (IsDead)
                return;
            // Durante l'invulnerabilita' ignoriamo silenziosamente il danno.
            if (Time.time < invulnerableUntil)
                return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            invulnerableUntil = Time.time + invulnerabilityAfterHit;

            OnDamaged?.Invoke();
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            // La morte avviene una sola volta: usciamo subito dopo l'evento.
            if (CurrentHealth == 0)
                OnDeath?.Invoke();
        }

        /// <summary>Adds health points up to MaxHealth. No-op if already dead.</summary>
        /// <param name="amount">Health points to restore. Negative values are ignored.</param>
        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead)
                return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        /// <summary>Instantly drops health to zero and fires OnDeath once.</summary>
        public void Kill()
        {
            if (IsDead)
                return;
            CurrentHealth = 0;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnDeath?.Invoke();
        }

        /// <summary>Restores full health. Used by LevelManager on restart.</summary>
        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            invulnerableUntil = 0f;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Partiamo sempre con la vita piena. Notifichiamo l'HUD (se gia'
            // sottoscritto) cosi' mostra subito il valore corretto.
            CurrentHealth = maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        #endregion

        #region Internal

        // Time.time fino al quale i danni vengono ignorati. 0 = nessuna
        // invulnerabilita' attiva.
        private float invulnerableUntil;

        #endregion
    }
}
