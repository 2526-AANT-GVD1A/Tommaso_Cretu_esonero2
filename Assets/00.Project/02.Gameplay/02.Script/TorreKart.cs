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
            if (entry == null || entry.visualPrefab == null)
            {
                Debug.LogWarning("[KartCollectedStack] Nessun prefab configurato per il tipo: " + visualType, this);
                return;
            }

            if (maxItems > 0 && spawnedItems.Count >= maxItems)
                RemoveOldestItem();

            GameObject item = Instantiate(entry.visualPrefab, stackRoot);
            item.name = "Stack_" + visualType + "_" + Time.frameCount;

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
    }
}
