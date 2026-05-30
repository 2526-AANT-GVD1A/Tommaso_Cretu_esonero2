// =============================================================================
// Checkpoint
// -----------------------------------------------------------------------------
// COSA FA:
//   Punto di passaggio obbligato lungo il tracciato. Quando il kart lo
//   attraversa registra il passaggio e (opzionalmente) salva il punto di
//   respawn. Lo usano la WinCondition CompleteLap e qualsiasi sistema che
//   voglia sapere "il giocatore e' passato di qui".
//
// COME SI USA:
//   1) Trascina il prefab "Checkpoint" in scena.
//   2) Imposta "Order" in modo crescente (0, 1, 2...).
//   3) Aggiungi questo Checkpoint alla lista "Required Checkpoints" della
//      WinCondition di tipo CompleteLap.
//
// DA SAPERE:
//   - Funziona con un Collider impostato come trigger.
//   - Reagisce solo a oggetti con tag "Player".
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Behaviors
{
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: cambia "order" per definire l'ordine di passaggio.
        // Il primo checkpoint del giro ha order = 0, il secondo = 1, ecc.
        [SerializeField, Tooltip("Ordine progressivo del checkpoint (0, 1, 2...).")]
        private int order = 0;

        [SerializeField, Tooltip("Punto e rotazione di respawn (default: questo Transform).")]
        private Transform respawnPoint;

        [SerializeField, Tooltip("Se true, attivabile solo una volta.")]
        private bool oneTimeOnly = false;

        [SerializeField, Tooltip("Suono riprodotto al passaggio.")]
        private AudioClip activateSfx;

        [SerializeField, Tooltip("Particellare instanziato al passaggio.")]
        private GameObject activateVfx;

        #endregion

        #region Events

        // Evento invocato quando il kart attraversa il checkpoint.
        // Parametro = ordine del checkpoint (utile per le WinCondition CompleteLap).
        public UnityEvent<int> OnReached;

        #endregion

        #region Public API

        /// <summary>Order index used by lap-completion logic.</summary>
        public int Order => order;

        /// <summary>True after the kart has crossed it at least once.</summary>
        public bool Reached { get; private set; }

        /// <summary>Returns the assigned respawn point or, if missing, this transform.</summary>
        public Transform RespawnPoint => respawnPoint != null ? respawnPoint : transform;

        /// <summary>Resets the reached state. Used by LevelManager on level restart.</summary>
        public void ResetReached() { Reached = false; }

        #endregion

        #region Unity callbacks

        private void OnTriggerEnter(Collider other)
        {
            // Reagiamo solo al Player. Ignoriamo collectible, hazard, ecc.
            if (!other.CompareTag("Player")) return;
            // Se il checkpoint e' "una volta sola" e gia' preso, ignoriamo.
            if (oneTimeOnly && Reached) return;

            Reached = true;

            if (activateSfx != null) AudioSource.PlayClipAtPoint(activateSfx, transform.position);
            if (activateVfx != null) Instantiate(activateVfx, transform.position, Quaternion.identity);

            // Notifichiamo chi e' in ascolto (WinCondition, HUD per messaggio, ecc.).
            OnReached?.Invoke(order);
        }

        #endregion
    }
}
