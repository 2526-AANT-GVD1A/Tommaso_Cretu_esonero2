using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeKart.Gameplay
{
    public class KartCollectedStack : MonoBehaviour
    {
        [Serializable]
        public class VisualEntry
        {
            [Tooltip("Tipo logico dell'oggetto. Es: coin, gem, key.")]
            public string visualType = "coin";

            [Tooltip("Prefab da mostrare nella torre.")]
            public GameObject visualPrefab;

            [Tooltip("Scala locale del prefab instanziato.")]
            public Vector3 localScale = Vector3.one;
        }

        [Header("References")]
        [SerializeField, Tooltip("Punto sotto cui creare la torre. Consigliato: un figlio di Grafica.")]
        private Transform stackRoot;

        [Header("Layout")]
        [SerializeField, Tooltip("Offset locale del primo elemento della torre.")]
        private Vector3 baseLocalOffset = new Vector3(0f, 0f, 0f);

        [SerializeField, Tooltip("Distanza verticale tra un elemento e il successivo.")]
        private float verticalSpacing = 0.35f;

        [SerializeField, Tooltip("Rotazione locale degli elementi impilati.")]
        private Vector3 localEuler = Vector3.zero;

        [Header("Limit")]
        [SerializeField, Tooltip("Numero massimo di oggetti visibili nella torre.")]
        private int maxItems = 5;

        [Header("Sicurezza Fisica")]
        [SerializeField, Tooltip("Layer su cui forzare i cloni della torre. Deve essere un layer inerte che NON collide col kart (Vehicle/ground) ne' coi suoi SphereCast/Raycast di ground check, cosi' una torre alta non puo' bloccare il kart in tunnel/passaggi stretti. Default: Oggeto (9).")]
        private int stackLayer = 9;

        [Header("Type Mapping")]
        [SerializeField, Tooltip("Associazione tipo -> prefab visivo.")]
        private List<VisualEntry> visuals = new List<VisualEntry>();

        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        public Transform StackRoot => stackRoot;
        public int ItemCount => spawnedItems.Count;

        public void AddCollectedItem(string visualType)
        {
            if (string.IsNullOrWhiteSpace(visualType))
            {
                Debug.LogWarning("[KartCollectedStack] visualType vuoto.", this);
                return;
            }

            if (stackRoot == null)
            {
                Debug.LogWarning("[KartCollectedStack] Stack Root non assegnato.", this);
                return;
            }

            VisualEntry entry = FindEntry(visualType);
            if (entry == null)
            {
                Debug.LogWarning("[KartCollectedStack] Nessuna VisualEntry per il tipo: " + visualType, this);
                return;
            }

            // visualPrefab puo' essere "null" anche se la entry esiste: capita
            // quando il campo punta a un'istanza di scena anziche' a un prefab
            // asset di progetto e quella istanza viene distrutta a runtime
            // (es. e' essa stessa un Pickup raccolto e Destroy-ato).
            if (entry.visualPrefab == null)
            {
                Debug.LogWarning("[KartCollectedStack] Prefab visivo mancante o distrutto per il tipo: " + visualType + ". Assegna nel visualPrefab un prefab ASSET di progetto, non un'istanza di scena.", this);
                return;
            }

            if (maxItems > 0 && spawnedItems.Count >= maxItems)
                RemoveOldestItem();

            GameObject item = Instantiate(entry.visualPrefab, stackRoot);
            item.name = "Stack_" + visualType + "_" + Time.frameCount;

            SanitizeVisualClone(item);

            Transform t = item.transform;
            t.localRotation = Quaternion.Euler(localEuler);
            t.localScale = entry.localScale;

            spawnedItems.Add(item);
            RefreshStackLayout();
        }

        public void ClearAll()
        {
            for (int i = spawnedItems.Count - 1; i >= 0; i--)
            {
                if (spawnedItems[i] != null)
                    Destroy(spawnedItems[i]);
            }

            spawnedItems.Clear();
        }

        private void RemoveOldestItem()
        {
            if (spawnedItems.Count == 0)
                return;

            GameObject oldest = spawnedItems[0];
            spawnedItems.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest);
        }

        private void RefreshStackLayout()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] == null)
                    continue;

                Transform t = spawnedItems[i].transform;
                t.localPosition = baseLocalOffset + Vector3.up * (verticalSpacing * i);
                t.localRotation = Quaternion.Euler(localEuler);
            }
        }

        private VisualEntry FindEntry(string visualType)
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                if (string.Equals(visuals[i].visualType, visualType, StringComparison.OrdinalIgnoreCase))
                    return visuals[i];
            }

            return null;
        }

        // I cloni della torre esistono solo per scopi visivi: non devono
        // partecipare alla fisica ne' riusare la logica di Pickup del
        // prefab sorgente (che e' pensata per l'oggetto "vivo" in scena).
        // Ripuliamo quindi ogni clone a runtime cosi' e' sicuro per
        // costruzione, indipendentemente da come e' configurato il prefab
        // assegnato nel campo visualPrefab (trigger, non-trigger, con
        // Rigidbody, con Pickup, su layer sbagliato...). Cosi' una torre
        // alta non puo' mai bloccare il kart in tunnel/passaggi stretti.
        private void SanitizeVisualClone(GameObject item)
        {
            // Layer inerte: non collide col kart ne' coi suoi ground check.
            foreach (Transform tr in item.GetComponentsInChildren<Transform>(true))
                tr.gameObject.layer = stackLayer;

            // Disattiviamo ogni collider: il clone non genera ne' contatti
            // fisici ne' eventi trigger. Include eventuali collider
            // disabilitati di default nel prefab (true li prende comunque).
            Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            // Rimuoviamo eventuali Rigidbody: senza corpo fisico il clone
            // e' grafica pura, non puo' oscillare/spingere/cadere.
            Rigidbody[] rbs = item.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null)
                    Destroy(rbs[i]);
            }

            // Rimuoviamo il componente Pickup: e' la logica di raccolta
            // dell'oggetto "vivo" e non deve girare sui cloni della torre
            // (altrimenti ogni elemento impilato tenterebbe di raccogliere
            // ancora il player e sprecerebbe callback ad ogni passaggio).
            Pickup[] pickups = item.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                    Destroy(pickups[i]);
            }
        }
    }
}
