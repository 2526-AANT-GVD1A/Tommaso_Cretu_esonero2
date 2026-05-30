// =============================================================================
// Collectible
// -----------------------------------------------------------------------------
// COSA FA:
//   Oggetto raccoglibile (moneta, gemma, chiave...). Quando il kart lo tocca,
//   notifica il CollectibleRegistry, suona/effetta, sparisce o respawna.
//   Puo' anche fluttuare e ruotare per essere piu' visibile in scena.
//
// COME SI USA:
//   1) Aggiungi questo componente a un GameObject che ha un Collider impostato
//      come "Is Trigger".
//   2) Imposta "Category" (es. "coin", "gem", "key") e "Value" (es. 1).
//   3) Se vuoi che riappaia, imposta "Respawn After" > 0 secondi.
//
// DA SAPERE:
//   - Reagisce solo a oggetti con tag "Player".
//   - Se floatBob/rotate sono attivi, il movimento viene applicato a un figlio
//     "Visual" se esiste, altrimenti al transform stesso.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Behaviors
{
    [RequireComponent(typeof(Collider))]
    public class Collectible : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: cambia per raggruppare i pickup. "coin", "gem", "key"
        // sono nomi standard, ma puoi inventarne altri (es. "star", "fuel").
        [SerializeField, Tooltip("Categoria del collectible. La WinCondition CollectN usa questa stringa.")]
        private string category = "coin";

        [SerializeField, Tooltip("Punti/quantita' che vale questo pickup.")]
        private int value = 1;

        // PER MODIFICARE: 0 = sparisce per sempre. 5 = riappare dopo 5 secondi.
        [SerializeField, Tooltip("Secondi prima del respawn (0 = sparisce per sempre).")]
        private float respawnAfter = 0f;

        [Header("Visual")]
        [SerializeField, Tooltip("Se true, fluttua su e giu' sul posto.")]
        private bool floatBob = true;
        [SerializeField, Tooltip("Ampiezza dell'oscillazione verticale (unita').")]
        private float bobAmplitude = 0.15f;
        [SerializeField, Tooltip("Frequenza dell'oscillazione (oscillazioni/secondo).")]
        private float bobFrequency = 1.5f;
        [SerializeField, Tooltip("Se true, ruota sull'asse Y.")]
        private bool rotate = true;
        [SerializeField, Tooltip("Velocita' di rotazione in gradi/secondo.")]
        private float rotateSpeed = 90f;

        [Header("Feedback")]
        [SerializeField, Tooltip("Suono riprodotto al pickup.")]
        private AudioClip pickupSfx;
        [SerializeField, Tooltip("Particellare instanziato al pickup.")]
        private GameObject pickupVfx;

        #endregion

        #region Events

        // Evento "preso!" - parametro = chi mi ha raccolto. Usalo per dare
        // bonus, far cambiare colore al kart, ecc.
        public UnityEvent<GameObject> OnCollected;

        #endregion

        #region Public API

        /// <summary>Category string used by CollectibleRegistry and WinCondition.</summary>
        public string Category => category;

        /// <summary>Value added to the registry counter on pickup.</summary>
        public int Value => value;

        /// <summary>Manually triggers the pickup (useful from UnityEvents in the Inspector).</summary>
        public void Collect(GameObject who)
        {
            if (collected) return;
            collected = true;

            CollectibleRegistry.NotifyCollected(this);
            if (pickupSfx != null) AudioSource.PlayClipAtPoint(pickupSfx, transform.position);
            if (pickupVfx != null) Instantiate(pickupVfx, transform.position, Quaternion.identity);
            OnCollected?.Invoke(who);

            // Respawn opzionale: nascondiamo il visivo, attendiamo, ricompariamo.
            if (respawnAfter > 0f) StartCoroutine(RespawnRoutine());
            else gameObject.SetActive(false);
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Memorizziamo la posizione di partenza per l'oscillazione e per il respawn.
            startPos = transform.position;
            // Se c'e' un figlio chiamato "Visual" usiamo quello per le animazioni
            // estetiche; cosi' il collider del padre resta fermo e prevedibile.
            Transform visual = transform.Find("Visual");
            visualTransform = (visual != null) ? visual : transform;
            CollectibleRegistry.Register(this);
        }

        private void OnDestroy() { CollectibleRegistry.Unregister(this); }

        private void Update()
        {
            // Animazioni puramente visive: nessun impatto su gameplay/fisica.
            if (rotate) visualTransform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
            if (floatBob)
            {
                float y = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
                Vector3 p = visualTransform.position;
                p.y = (visualTransform == transform ? startPos.y : visualTransform.localPosition.y) + y;
                if (visualTransform == transform) visualTransform.position = p;
                else visualTransform.localPosition = new Vector3(visualTransform.localPosition.x, y, visualTransform.localPosition.z);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Reagiamo solo al Player. Ignoriamo proiettili, altri pickup, ecc.
            if (!other.CompareTag("Player")) return;
            Collect(other.gameObject);
        }

        #endregion

        #region Internal

        private Vector3 startPos;
        private Transform visualTransform;
        private bool collected;

        private IEnumerator RespawnRoutine()
        {
            // Spegniamo collider + renderer (non il GameObject!), cosi' la coroutine
            // puo' continuare a girare e farci ricomparire. Se spegnessimo il
            // GameObject la coroutine morirebbe e WaitForSeconds non si risveglierebbe.
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;

            yield return new WaitForSeconds(respawnAfter);

            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = true;
            if (col != null) col.enabled = true;
            collected = false;
        }

        #endregion
    }
}
