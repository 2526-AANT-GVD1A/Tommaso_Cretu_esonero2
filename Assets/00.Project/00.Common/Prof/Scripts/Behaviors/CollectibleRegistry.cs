// =============================================================================
// CollectibleRegistry
// -----------------------------------------------------------------------------
// COSA FA:
//   Tiene il conto di quanti Collectible esistono e quanti ne sono stati
//   raccolti, divisi per categoria (es. "coin", "gem", "key"). E' un servizio
//   STATICO usato sia dai Collectible (per registrarsi) sia dall'HUD e dalle
//   WinCondition (per leggere i totali).
//
// COME SI USA:
//   Non c'e' niente da fare in scena. Quando crei un Collectible, lui stesso
//   chiama Register() in Awake. L'HUD si iscrive a OnCategoryChanged per
//   aggiornare il contatore. La WinCondition di tipo CollectN legge i totali.
//
// DA SAPERE:
//   - Questo e' l'UNICO punto della toolkit in cui usiamo uno static mutabile
//     e l'evento C# (OnCategoryChanged). Motivo: un servizio "globale" non
//     puo' essere un MonoBehaviour, quindi non possiamo usare un UnityEvent
//     da Inspector. Lo accettiamo come deroga consapevole.
//   - Il registro si auto-pulisce a ogni cambio scena (RuntimeInitialize).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcadeKart.Behaviors
{
    public static class CollectibleRegistry
    {
        #region Events

        // Evento globale: (category, collected, total). L'HUD lo ascolta per
        // aggiornare scritte tipo "Monete: 3/8".
        public static event Action<string, int, int> OnCategoryChanged;

        #endregion

        #region Public API

        /// <summary>How many items of this category have been collected so far.</summary>
        public static int CountCollected(string category)
        {
            return collectedByCategory.TryGetValue(category, out int n) ? n : 0;
        }

        /// <summary>How many items of this category exist in the scene (including collected ones).</summary>
        public static int CountTotal(string category)
        {
            return totalByCategory.TryGetValue(category, out int n) ? n : 0;
        }

        /// <summary>Called by Collectible.Awake. Increases the per-category total.</summary>
        public static void Register(Collectible c)
        {
            if (c == null) return;
            string cat = c.Category;
            totalByCategory[cat] = CountTotal(cat) + 1;
            // Non incrementiamo "collected" qui, ma notifichiamo per far
            // aggiornare l'HUD col nuovo totale (es. "0/8" appena la scena parte).
            OnCategoryChanged?.Invoke(cat, CountCollected(cat), CountTotal(cat));
        }

        /// <summary>Called by Collectible.OnDestroy. Decreases the per-category total.</summary>
        public static void Unregister(Collectible c)
        {
            if (c == null) return;
            string cat = c.Category;
            int total = Mathf.Max(0, CountTotal(cat) - 1);
            totalByCategory[cat] = total;
            OnCategoryChanged?.Invoke(cat, CountCollected(cat), total);
        }

        /// <summary>Called by Collectible.Collect. Increments the collected counter.</summary>
        public static void NotifyCollected(Collectible c)
        {
            if (c == null) return;
            string cat = c.Category;
            collectedByCategory[cat] = CountCollected(cat) + c.Value;
            OnCategoryChanged?.Invoke(cat, CountCollected(cat), CountTotal(cat));
        }

        /// <summary>Wipes all counters. Called automatically on scene load.</summary>
        public static void Clear()
        {
            totalByCategory.Clear();
            collectedByCategory.Clear();
        }

        #endregion

        #region Internal

        // Due dizionari indipendenti: evita di mischiare conteggi diversi
        // (alcuni collectible possono "sparire" una volta raccolti, altri no).
        private static readonly Dictionary<string, int> totalByCategory = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> collectedByCategory = new Dictionary<string, int>();

        // Hook che si registra al boot del gioco e a ogni caricamento di scena.
        // Pulisce il registro per evitare che i conteggi si trascinino da una
        // scena all'altra.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bootstrap()
        {
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Su Single (caricamento normale di una nuova scena) ripuliamo.
            // Su Additive lasciamo i conteggi (usato di rado in didattica).
            if (mode == LoadSceneMode.Single) Clear();
        }

        #endregion
    }
}
