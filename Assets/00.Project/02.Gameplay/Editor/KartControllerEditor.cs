using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.EditorTools
{
    // Editor-only (cartella Editor: non finisce nel build). Riorganizza
    // l'inspector di KartController in gruppi collassabili SENZA toccare lo
    // script runtime: i campi sono ESATTAMENTE gli stessi di prima (stessi
    // nomi, stessi percorsi di serializzazione), quindi tutti i valori gia'
    // sintonizzati nelle scene restano agganciati e nessuna variabile viene
    // eliminata o sostituita. I gruppi sono solo un modo diverso di
    // VISUALIZZARE i campi: tutto viene disegnato via SerializedProperty con
    // PropertyField, cosi' tooltip, slider Range, liste e riferimenti si
    // comportano come nell'inspector di default.
    //
    // Sicurezza anti-dimenticanze: qualunque campo NON elencato nei gruppi
    // (presente oggi o aggiunto in futuro) viene comunque disegnato nella
    // sezione finale "Altro", in ordine di dichiarazione. Nessun dato puo'
    // sparire dall'inspector per un nome sbagliato o mancante (un nome
    // errato in una lista produce un HelpBox di avviso, non un buco).
    //
    // Lo stato dei foldout (chiusi di default) e' statico: resta per tutta
    // la sessione dell'editor ed e' condiviso fra tutti i KartController
    // selezionati, cosi' confrontare il kart del giocatore con il NPC resta
    // comodo. Ricompilare gli script li riporta chiusi.
    [CustomEditor(typeof(KartController))]
    [CanEditMultipleObjects]
    public class KartControllerEditor : Editor
    {
        // ===== Definizione dei gruppi =====

        // Sezione = un foldout. "campi" sono i nomi serializzati da mostrare
        // dentro la sezione (in ordine di visualizzazione); "sotto" sono le
        // sottosezioni annidate (usate per i set Corsa / Camminata). Una
        // sezione puo' avere campi, sottosezioni o entrambi.
        private class Sezione
        {
            public readonly string titolo;
            public readonly string[] campi;
            public readonly Sezione[] sotto;

            public Sezione(string titolo, string[] campi, Sezione[] sotto = null)
            {
                this.titolo = titolo;
                this.campi = campi;
                this.sotto = sotto;
            }
        }

        // Ordine dei gruppi: segue l'ordine semantico dell'inspector di
        // default (Velocita' -> Sterzata -> Reorientation -> Grip/Drift ->
        // Drift attivo -> Terreno -> Volo/muri -> Visuale -> AI -> Eventi).
        private static readonly Sezione[] Sezioni =
        {
            new Sezione(
                "Velocita'",
                new[]
                {
                    "maxSpeed", "cruiseSpeed", "boostReleaseDeceleration",
                    "driftBoostEndDecay", "acceleration", "deceleration",
                    "reverseSpeed", "brakeStrength"
                }),

            // Set per fase: stesse variabili, un valore per camminata e uno
            // per corsa, fusi a runtime con PesoCorsa (vedi KartController).
            new Sezione(
                "Sterzata",
                null,
                new Sezione[]
                {
                    new Sezione(
                        "Corsa (mouse sx tenuto)",
                        new[]
                        {
                            "turnRate", "turnAtRest", "shoppingCartSteerLoss",
                            "shoppingCartSlip", "shoppingCartSlipSteerThreshold",
                            "cameraRelativeTurnResponsiveness"
                        }),
                    new Sezione(
                        "Camminata (boost rilasciato)",
                        new[]
                        {
                            "turnRateCamminata", "turnAtRestCamminata",
                            "shoppingCartSteerLossCamminata", "shoppingCartSlipCamminata",
                            "shoppingCartSlipSteerThresholdCamminata",
                            "cameraRelativeTurnResponsivenessCamminata"
                        })
                }),

            new Sezione(
                "Cambio direzione / Reorientation",
                new[]
                {
                    "instantRealignAngle", "instantRealignLongitudinalRetention",
                    "rotateBeforeMoveSpeedThreshold", "rotateBeforeMoveReleaseAngle",
                    "movingReorientationEnterAngle", "movingReorientationExitAngle",
                    "movingReorientationMinSpeed", "movingReorientationAccelerationFactor",
                    "movingReorientationBrakeStrength"
                }),

            new Sezione(
                "Grip / Drift",
                null,
                new Sezione[]
                {
                    new Sezione(
                        "Corsa (mouse sx tenuto)",
                        new[]
                        {
                            "groundLateralFriction", "airLateralFriction",
                            "driftLateralFriction", "driftMinSpeed", "driftMinSteer",
                            "driftSteerBoost", "driftVisual", "driftVisualYawDegrees",
                            "driftVisualLerpSpeed"
                        }),
                    new Sezione(
                        "Camminata (boost rilasciato)",
                        new[]
                        {
                            "groundLateralFrictionCamminata", "airLateralFrictionCamminata",
                            "driftLateralFrictionCamminata", "driftMinSpeedCamminata",
                            "driftMinSteerCamminata", "driftSteerBoostCamminata",
                            "driftVisualYawDegreesCamminata", "driftVisualLerpSpeedCamminata"
                        })
                }),

            new Sezione(
                "Drift attivo / mini-turbo",
                new[]
                {
                    "activeDriftToggleObjects", "activeDriftMinSpeed", "activeDriftMinSteer",
                    "activeDriftLateralFriction", "activeDriftForwardRetention",
                    "activeDriftMinForwardSpeed", "activeDriftExitGraceTime",
                    "activeDriftMaxTurnRate", "activeDriftSlipStabilizeK",
                    "activeDriftChargeMinAngle", "activeDriftChargeTime", "driftChargeRate",
                    "activeDriftBoostMagnitude", "activeDriftBoostDuration",
                    "activeDriftBoostKick", "activeDriftVisualYawScale",
                    "activeDriftVisualLerpSpeed"
                }),

            new Sezione(
                "Terreno e sospensioni",
                new[]
                {
                    "gravity", "airControl", "groundCheckDistance", "groundCheckRadius",
                    "groundCheckOrigin", "groundLayer", "maxGroundSlopeAngle",
                    "groundedGraceTime", "rideHeight", "suspensionStrength",
                    "suspensionDamping"
                }),

            new Sezione(
                "Volo, muri e impatti",
                new[]
                {
                    "airAngularDamping", "maxAirYawAngularVelocity",
                    "landingAngularDampingFactor", "wallContactGraceTime",
                    "impactThreshold", "skateRampVisualTurnSpeed"
                }),

            new Sezione(
                "Visuale kart",
                new[]
                {
                    "frontLeftGroundProbe", "frontRightGroundProbe",
                    "rearLeftGroundProbe", "rearRightGroundProbe",
                    "visualGroundAlignDistance", "groundAlignLerpSpeed",
                    "visualYawSmoothTime", "visualYawMaxTurnSpeed"
                }),

            new Sezione("AI", new[] { "aiSteeringMode" }),

            new Sezione(
                "Eventi",
                new[] { "OnGroundedChanged", "OnSpeedChanged", "OnImpact" })
        };

        // Stato dei foldout per titolo. Assenza del titolo = chiuso: l'inspector
        // parte quindi TUTTO compresso, come richiesto.
        private static readonly Dictionary<string, bool> foldoutAperti =
            new Dictionary<string, bool>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Nomi dei campi coperti dai gruppi: servono alla sezione "Altro"
            // per capire cosa NON ridisegnare.
            HashSet<string> raggruppati = new HashSet<string>();

            foreach (Sezione sezione in Sezioni)
                DrawSezione(sezione, raggruppati);

            DrawAltro(raggruppati);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSezione(Sezione sezione, HashSet<string> raggruppati)
        {
            if (Foldout(sezione.titolo))
            {
                EditorGUI.indentLevel++;

                if (sezione.sotto != null)
                {
                    foreach (Sezione sotto in sezione.sotto)
                        DrawSezione(sotto, raggruppati);
                }

                if (sezione.campi != null)
                {
                    foreach (string nome in sezione.campi)
                    {
                        DrawCampo(nome);
                        raggruppati.Add(nome);
                    }
                }

                EditorGUI.indentLevel--;
            }

            // Piccolo spazio di respiro fra le sezioni di primo livello
            // (le sottosezioni Corsa/Camminata restano compatte).
            if (sezione.sotto == null)
                EditorGUILayout.Space(2f);
        }

        // Foldout bold con stato ricordato per titolo (statico = per sessione).
        private bool Foldout(string titolo)
        {
            foldoutAperti.TryGetValue(titolo, out bool aperta);
            aperta = EditorGUILayout.Foldout(aperta, titolo, true, EditorStyles.foldoutHeader);
            foldoutAperti[titolo] = aperta;
            return aperta;
        }

        private void DrawCampo(string nome)
        {
            SerializedProperty prop = serializedObject.FindProperty(nome);
            if (prop == null)
            {
                // Nome sbagliato o campo rinominato: si vede SUBITO come
                // avviso, senza perdere nulla (il campo resta comunque
                // disegnato dalla sezione "Altro" o col suo nuovo nome).
                EditorGUILayout.HelpBox(
                    "KartControllerEditor: campo non trovato '" + nome + "'",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(prop, true);
        }

        // Ultima rete di sicurezza: disegna QUALSIASI campo serializzato non
        // coperto dai gruppi (oggi: nessuno; domani: ogni variabile nuova
        // dimenticata nelle liste sopra). Se non c'e' nulla, la sezione non
        // appare proprio.
        private void DrawAltro(HashSet<string> raggruppati)
        {
            List<string> altri = new List<string>();
            SerializedProperty iter = serializedObject.GetIterator();
            if (!iter.NextVisible(true))
                return;

            do
            {
                if (iter.name == "m_Script")
                    continue;
                if (raggruppati.Contains(iter.name))
                    continue;
                altri.Add(iter.name);
            }
            while (iter.NextVisible(false));

            if (altri.Count == 0)
                return;

            string titolo = "Altro (campi non raggruppati)";
            if (Foldout(titolo))
            {
                EditorGUI.indentLevel++;
                foreach (string nome in altri)
                    DrawCampo(nome);
                EditorGUI.indentLevel--;
            }
        }
    }
}
