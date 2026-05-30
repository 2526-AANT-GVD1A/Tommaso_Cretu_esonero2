// =============================================================================
// LoseCondition
// -----------------------------------------------------------------------------
// COSA FA:
//   Definisce UNA condizione di sconfitta. Il LevelManager le valuta tutte
//   ogni frame: basta UNA soddisfatta perche' il livello sia perso.
//   Tipi: TimerExpired (tempo scaduto), FellOffWorld (caduto sotto Y),
//   HealthDepleted (vita a 0), HitDeadlyZone (toccato un trigger letale).
//
// COME SI USA:
//   1) Crea un GameObject vuoto (es. "Lose - Out of Time"), aggiungi questo
//      componente.
//   2) Scegli "Type" e compila SOLO i campi pertinenti.
//   3) Trascina nella lista "Lose Conditions" del LevelManager.
//
// DA SAPERE:
//   - "FellOffWorld" cerca il player con tag "Player".
//   - "HealthDepleted" cerca KartHealth sul player se non lo assegni.
//   - "HitDeadlyZone": collega un TriggerZone "letale" e ascolta OnTriggered.
// =============================================================================

using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Behaviors
{
    public enum LoseType { TimerExpired, FellOffWorld, HealthDepleted, HitDeadlyZone }

    public class LoseCondition : MonoBehaviour
    {
        #region Inspector

        [SerializeField, Tooltip("Tipo di condizione di sconfitta.")]
        private LoseType type = LoseType.TimerExpired;

        [Header("FellOffWorld")]
        // PER MODIFICARE: alza per essere "perdonato" prima, abbassa per dare
        // piu' margine di caduta.
        [SerializeField, Tooltip("Y minima sotto cui il player e' considerato 'caduto fuori dal mondo'.")]
        private float fallY = -20f;

        [Header("HealthDepleted")]
        [SerializeField, Tooltip("Riferimento al KartHealth del player. Lasciato vuoto, lo cerca per tag.")]
        private KartHealth kartHealthRef;

        [Header("HitDeadlyZone")]
        [SerializeField, Tooltip("TriggerZone letale: quando si attiva, perdiamo subito.")]
        private TriggerZone deadlyZone;

        #endregion

        #region Public API

        /// <summary>True if this lose condition is currently met. Evaluated by LevelManager every frame.</summary>
        public bool IsSatisfied
        {
            get
            {
                switch (type)
                {
                    case LoseType.TimerExpired:
                        return EvaluateTimerExpired();
                    case LoseType.FellOffWorld:
                        return cachedPlayer != null && cachedPlayer.position.y < fallY;
                    case LoseType.HealthDepleted:
                        return kartHealthRef != null && kartHealthRef.IsDead;
                    case LoseType.HitDeadlyZone:
                        return deadlyZoneTriggered;
                }
                return false;
            }
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Cerchiamo il player una sola volta. Funziona se in scena c'e'
            // almeno un GameObject con tag "Player".
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                cachedPlayer = p.transform;
                // Se KartHealth non e' assegnato, proviamo a prenderlo dal player.
                if (kartHealthRef == null) kartHealthRef = p.GetComponentInParent<KartHealth>();
            }

            // Per la zona letale ci sottoscriviamo all'evento UnityEvent.
            // Cosi' non dobbiamo fare polling: appena scatta, settiamo il flag.
            if (type == LoseType.HitDeadlyZone)
            {
                if (deadlyZone != null) deadlyZone.OnTriggered.AddListener(OnDeadlyZoneTriggered);
                else Debug.LogWarning("[LoseCondition] HitDeadlyZone senza TriggerZone assegnato su " + name + ".", this);
            }
        }

        private void OnDestroy()
        {
            if (deadlyZone != null) deadlyZone.OnTriggered.RemoveListener(OnDeadlyZoneTriggered);
        }

        #endregion

        #region Internal

        private Transform cachedPlayer;
        private bool deadlyZoneTriggered;

        private bool EvaluateTimerExpired()
        {
            // Un Timer e' "scaduto" quando e' in CountDown e CurrentTime <= 0.
            TimerManager t = TimerManager.Instance;
            if (t == null) return false;
            return t.Mode == TimerMode.CountDown && t.CurrentTime <= 0f;
        }

        private void OnDeadlyZoneTriggered(GameObject who)
        {
            // Ignoriamo entita' che non sono il player (paranoia: la TriggerZone
            // gia' filtra, ma cosi' siamo doppiamente sicuri).
            if (who != null && who.CompareTag("Player")) deadlyZoneTriggered = true;
        }

        #endregion
    }
}
