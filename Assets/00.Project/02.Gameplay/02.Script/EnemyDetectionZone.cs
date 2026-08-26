using System.Collections.Generic;
using UnityEngine;

namespace ArcadeKart.Gameplay
{
    // Zona trigger FISSA nella scena che fa da "territorio" di un EnemyKart.
    // Ha due ruoli:
    //   1) Rileva l'ingresso/uscita del kart del giocatore (rendendolo
    //      "visibile" / in range per il cono visivo del NPC). L'EnemyKart
    //      legge PlayerInside ogni frame.
    //   2) Fornisce i suoi bounds mondali (AABB del Collider) per la logica
    //      di contenimento: il kart NPC deve restare e guidare dentro questa
    //      zona (sterzare/frenare verso il centro vicino al bordo + hard
    //      clamp in FixedUpdate) e pescare dentro i bounds i punti wander.
    //
    // PlayerInside e' un CONTEGGIO di collider (HashSet) e non un bool: il
    // kart del giocatore ha piu' di un collider taggato Player (sfera solida
    // + capsula trigger). Con un bool, l'uscita di uno solo dei collider
    // mentre l'altro e' ancora dentro (giocatore a cavallo del bordo)
    // spegneva il flag e faceva sfarfallare il lock del NPC. Col conteggio,
    // PlayerInside resta true finche' c'e' almeno un collider del giocatore
    // dentro. Inoltre puliamo i null (collider distrutti) in Update per
    // sicurezza.
    [RequireComponent(typeof(Collider))]
    public class EnemyDetectionZone : MonoBehaviour
    {
        [Header("Filtering")]
        [SerializeField, Tooltip("Se assegnato, il trigger reagisce solo a questo tag. Consigliato: Player.")]
        private string requiredTag = "Player";

        // True mentre almeno un collider del giocatore e' dentro la zona.
        // Letto ogni frame dall'EnemyKart. Si azzera in OnEnable.
        public bool PlayerInside => insideColliders.Count > 0;

        // Bounds mondiali (AABB) del Collider della zona. Usati dall'EnemyKart
        // per il contenimento e per pescare i punti wander. Per Box
        // allineati al mondo e' esatto; per Box ruotate e' un'approssimazione
        // (AABB), accettabile per un territorio arcade.
        public Bounds WorldBounds => cachedCollider != null ? cachedCollider.bounds : new Bounds();

        private Collider cachedCollider;
        private readonly HashSet<Collider> insideColliders = new HashSet<Collider>();

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
            // trigger riceve OnEnable e riparte pulito (niente collider
            // "appesi" da una sessione precedente).
            insideColliders.Clear();
        }

        private void Update()
        {
            // Pulizia difensiva: se un collider del giocatore e' stato
            // distrutto/disattivato senza passare da OnTriggerExit (es. kart
            // respawnato/teletrasportato), togliamolo dal conteggio.
            if (insideColliders.Count == 0)
                return;

            insideColliders.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            insideColliders.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            insideColliders.Remove(other);
        }
    }
}
