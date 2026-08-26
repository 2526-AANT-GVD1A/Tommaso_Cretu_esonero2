using UnityEngine;

namespace ArcadeKart.Gameplay
{
    // Zona trigger FISSA nella scena che fa da "territorio" di un EnemyKart.
    // Ha due ruoli:
    //   1) Rileva l'ingresso/uscita del kart del giocatore (rendendolo
    //      "visibile" / in range per il cono visivo del NPC). L'EnemyKart
    //      legge PlayerInside ogni frame.
    //   2) Fornisce i suoi bounds mondiali (AABB del Collider) per la logica
    //      di contenimento: il kart NPC deve restare e guidare dentro questa
    //      zona (steerare verso il centro quando si avvicina al bordo) e
    //      pescare dentro i bounds i punti del wander.
    // Stesso stile di CameraPhaseTrigger / TriggerFineLivello: filtro per tag,
    // isTrigger automatico in Reset/Awake, reset dello stato in OnEnable
    // (la radice del livello viene riattivata ad ogni partita).
    [RequireComponent(typeof(Collider))]
    public class EnemyDetectionZone : MonoBehaviour
    {
        [Header("Filtering")]
        [SerializeField, Tooltip("Se assegnato, il trigger reagisce solo a questo tag. Consigliato: Player.")]
        private string requiredTag = "Player";

        // True mentre il kart del giocatore e' dentro la zona. Letto ogni
        // frame dall'EnemyKart per decidere se il giocatore e' "in range"
        // (candidato al rilevamento via cono visivo). Si azzera in OnEnable.
        public bool PlayerInside { get; private set; }

        // Bounds mondiali (AABB) del Collider della zona. Usati dall'EnemyKart
        // per il contenimento (steerare verso il centro vicino al bordo) e per
        // pescare i punti wander. Per Box allineati al mondo e' esatto; per
        // Box ruotate e' un'approssimazione (AABB), accettabile per un
        // territorio arcade.
        public Bounds WorldBounds => cachedCollider != null ? cachedCollider.bounds : new Bounds();

        private Collider cachedCollider;

        private void Reset()
        {
            // Quando si aggiunge il componente in Editor, il collider viene
            // messo automaticamente come trigger: un errore in meno.
            Collider c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            if (cachedCollider != null)
                cachedCollider.isTrigger = true;
        }

        private void OnEnable()
        {
            // La radice del livello viene riattivata ad ogni partita: il
            // trigger riceve OnEnable e riparte pulito (il giocatore non e'
            // piu' dentro). cachedCollider e' gia' stato preso in Awake
            // (che gira una sola volta); qui resettiamo solo lo stato.
            PlayerInside = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            PlayerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            PlayerInside = false;
        }
    }
}
