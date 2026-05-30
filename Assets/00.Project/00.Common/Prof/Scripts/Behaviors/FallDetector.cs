// =============================================================================
// FallDetector
// -----------------------------------------------------------------------------
// COSA FA:
//   Monitora l'altezza del kart e invoca un evento UnityEvent quando scende
//   sotto una soglia (Fall Y). Usalo per gestire "il kart e' caduto dalla
//   mappa" senza dover piazzare un volume trigger gigante sotto tutta la pista.
//
// COME SI USA:
//   1) Aggiungi il componente sul GameObject del kart (consigliato), oppure
//      su un GameObject vuoto "FallDetector" assegnando "Target" a mano.
//      Se Target e' vuoto, il componente cerca il GameObject con tag "Player".
//   2) Imposta "Fall Y" all'altezza sotto cui il kart e' "caduto".
//   3) Nell'Inspector collega l'evento "On Fell" a una o piu' azioni:
//      - KartController.RespawnAt(playerStart)  → riporta il kart all'inizio
//      - KartHealth.TakeDamage(1)               → togli una vita
//      - KartHealth.Kill()                      → uccidi (game over via HealthDepleted)
//      - LevelManager.TriggerLose()             → sconfitta diretta
//      - TimerManager.SubtractTime(10)          → penalita' tempo (Survival)
//      - HUDController.ShowMessage("Caduto!")   → feedback testuale
//
// DA SAPERE:
//   - Edge-triggered: l'evento scatta UNA VOLTA per caduta. Se nessuno riporta
//     il kart sopra la soglia, non si ripete. Appena risale sopra Fall Y e
//     ricade, scatta di nuovo. Cosi' non c'e' spam di eventi al frame.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Behaviors
{
    public class FallDetector : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: assegna il transform da monitorare. Se vuoto, il
        // componente cerca in Awake il GameObject con tag "Player".
        [SerializeField, Tooltip("Transform da monitorare. Se vuoto, cerca 'Player' per tag.")]
        private Transform target;

        // PER MODIFICARE: alza per essere "perdonato" prima (es. -5), abbassa
        // per dare piu' margine di caduta (es. -50).
        [SerializeField, Tooltip("Y minima sotto cui il target e' considerato 'caduto'.")]
        private float fallY = -10f;

        #endregion

        #region Events

        // Evento "il kart e' caduto fuori mappa". Scatta UNA VOLTA per caduta.
        // Collega qui respawn, danno, penalita' tempo, sconfitta, messaggi HUD...
        public UnityEvent OnFell;

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Auto-find difensivo: se lo studente dimentica di assegnare Target,
            // proviamo a recuperarlo dal player taggato.
            if (target == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) target = p.transform;
                else Debug.LogWarning("[FallDetector] Nessun Target assegnato e nessun GameObject con tag 'Player' in scena.", this);
            }
        }

        private void Update()
        {
            if (target == null) return;

            // Edge-trigger: scatta solo nella transizione "sopra → sotto".
            // Cosi' chi ascolta non viene chiamato 60 volte al secondo se il
            // kart resta fermo sotto la soglia.
            bool isBelow = target.position.y < fallY;
            if (isBelow && !wasBelow) OnFell?.Invoke();
            wasBelow = isBelow;
        }

        #endregion

        #region Internal

        // Stato del frame precedente, serve per l'edge-trigger.
        private bool wasBelow;

        #endregion
    }
}
