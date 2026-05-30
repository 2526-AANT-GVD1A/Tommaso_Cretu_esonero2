// =============================================================================
// WinCondition
// -----------------------------------------------------------------------------
// COSA FA:
//   Definisce UNA condizione di vittoria. Il LevelManager le valuta tutte
//   ogni frame: il livello e' vinto solo quando TUTTE sono soddisfatte.
//   Tipi disponibili: CollectN (raccogli N oggetti), ReachPoint (arriva a un
//   punto), CompleteLap (passa per tutti i checkpoint), Survive (resisti X
//   secondi).
//
// COME SI USA:
//   1) Crea un GameObject vuoto (es. "Win - Collect 8 Coins") e aggiungi
//      questo componente.
//   2) Scegli "Type" e compila SOLO i campi pertinenti.
//   3) Trascina il GameObject nella lista "Win Conditions" del LevelManager.
//
// DA SAPERE:
//   - "ReachPoint" cerca il giocatore con tag "Player" se non assegni un Transform.
//   - "CompleteLap" considera completato il giro quando TUTTI i checkpoint
//     della lista sono stati raggiunti (in qualsiasi ordine, per semplicita').
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Behaviors
{
    public enum WinType { CollectN, ReachPoint, CompleteLap, Survive }

    public class WinCondition : MonoBehaviour
    {
        #region Inspector

        // PER MODIFICARE: cambia per definire la regola di vittoria del tuo livello.
        // CollectN = raccogli N oggetti; ReachPoint = arriva a un punto;
        // CompleteLap = passa tutti i checkpoint nell'ordine; Survive = sopravvivi X secondi.
        [SerializeField, Tooltip("Tipo di condizione di vittoria.")]
        private WinType type = WinType.CollectN;

        [Header("CollectN")]
        // PER MODIFICARE: numero di oggetti da raccogliere. Tieni conto di quanti ne pianti in scena!
        [SerializeField, Tooltip("Quanti collectible servono.")]
        private int requiredCount = 1;
        // PER MODIFICARE: nome della categoria. Deve coincidere ESATTAMENTE con la "category" impostata
        // sui pickup (es. "coin", "gem", "key"). Maiuscole/minuscole contano.
        [SerializeField, Tooltip("Categoria dei collectible da contare (deve combaciare con quella sui pickup).")]
        private string requiredCategory = "coin";

        [Header("ReachPoint")]
        [SerializeField, Tooltip("Punto di arrivo (Transform). Se vuoto, lascia disponibile reachRadius dal player.")]
        private Transform targetTransform;
        // PER MODIFICARE: aumenta per accettare un arrivo "approssimato",
        // diminuisci per richiedere precisione millimetrica.
        [SerializeField, Tooltip("Distanza massima dal target per considerarlo raggiunto.")]
        private float reachRadius = 1.5f;

        [Header("CompleteLap")]
        [SerializeField, Tooltip("Lista dei checkpoint del giro. Tutti devono essere stati attraversati.")]
        private List<Checkpoint> requiredCheckpoints = new List<Checkpoint>();

        [Header("Survive")]
        // PER MODIFICARE: durata della "fase di sopravvivenza" in secondi.
        // Da usare insieme a un LoseCondition tipo HealthDepleted per un livello "resisti agli attacchi per X tempo".
        [SerializeField, Tooltip("Quanti secondi resistere dall'inizio del livello.")]
        private float survivalTime = 60f;

        #endregion

        #region Public API

        /// <summary>True if this condition is currently satisfied. Evaluated by LevelManager every frame.</summary>
        public bool IsSatisfied
        {
            get
            {
                switch (type)
                {
                    case WinType.CollectN:
                        return CollectibleRegistry.CountCollected(requiredCategory) >= requiredCount;
                    case WinType.ReachPoint:
                        return EvaluateReachPoint();
                    case WinType.CompleteLap:
                        return EvaluateCompleteLap();
                    case WinType.Survive:
                        return EvaluateSurvive();
                }
                return false;
            }
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            // Per ReachPoint cerchiamo il player UNA SOLA VOLTA, evitando
            // FindWithTag in ogni frame (costoso e non necessario).
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) cachedPlayer = p.transform;
        }

        #endregion

        #region Internal

        private Transform cachedPlayer;

        private bool EvaluateReachPoint()
        {
            if (targetTransform == null || cachedPlayer == null) return false;
            // sqrMagnitude evita la radice quadrata: piu' veloce a parita' di risultato.
            float r = reachRadius;
            return (cachedPlayer.position - targetTransform.position).sqrMagnitude <= r * r;
        }

        private bool EvaluateCompleteLap()
        {
            if (requiredCheckpoints.Count == 0) return false;
            for (int i = 0; i < requiredCheckpoints.Count; i++)
            {
                Checkpoint c = requiredCheckpoints[i];
                if (c == null || !c.Reached) return false;
            }
            return true;
        }

        private bool EvaluateSurvive()
        {
            // Senza LevelManager non sappiamo da quando "iniziare" a contare.
            if (LevelManager.Instance == null) return false;
            return (Time.time - LevelManager.Instance.LevelStartTime) >= survivalTime;
        }

        #endregion
    }
}
