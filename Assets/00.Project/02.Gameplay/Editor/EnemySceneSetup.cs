using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
using ArcadeKart.Core;
using ArcadeKart.Gameplay;

namespace ArcadeKart.EditorTools
{
    // Editor-only (cartella Editor: non finisce nel build). Duplica il kart
    // del giocatore in un kart NPC con EnemyKart, crea il territorio
    // (EnemyDetectionZone) sotto la radice del Livello 1, collega i
    // riferimenti (territoryZone -> zona, aiSteeringMode true) e salva la
    // scena. Pensato per essere eseguito una tantua in batch mode:
    //   Unity -batchmode -quit -executeMethod ArcadeKart.EditorTools.EnemySceneSetup.SetupEnemyNpc
    // L'Instantiate del kart preserva automaticamente tutti i riferimenti
    // interni del KartController (groundCheckOrigin, driftVisual, probes)
    // rimappandoli sui figli del clone, cosi' il NPC guida con la stessa
    // fisica del giocatore senza dover ri-assegnare nulla a mano.
    public static class EnemySceneSetup
    {
        private const string ScenePath = "Assets/00.Project/02.Gameplay/01.Scene/TestCAR/Test1.unity";
        private const string NpcName = "Kart_NPC1";
        private const string TerritoryName = "Territory_NPC1";

        public static void SetupEnemyNpc()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Kart del giocatore: lo prendiamo dal riferimento 'kart' del
            // LevelManager (autorevole) invece che da FindWithTag("Player"),
            // perche' nella scena ci sono PIU' oggetti taggati Player
            // (CollisioniMURI, Kart1.0, sfera) e FindWithTag ne restituirebbe
            // uno qualsiasi, magari senza gerarchia kart.
            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm == null)
            {
                Debug.LogError("[EnemySceneSetup] LevelManager non trovato.");
                return;
            }
            SerializedObject lmSO = new SerializedObject(lm);
            KartController playerKartCtrl = lmSO.FindProperty("kart").objectReferenceValue as KartController;
            if (playerKartCtrl == null)
            {
                Debug.LogError("[EnemySceneSetup] Riferimento 'kart' del LevelManager non assegnato.");
                return;
            }
            GameObject playerKart = playerKartCtrl.gameObject;

            // Radice del Livello 1 letta dal LevelManager (campo privato
            // 'livelli[0].radice') via SerializedObject: cosi' non dipendiamo
            // dal nome e reggiamo se l'utente rinomina la radice.
            SerializedProperty livelli = lmSO.FindProperty("livelli");
            if (livelli == null || livelli.arraySize == 0)
            {
                Debug.LogError("[EnemySceneSetup] Nessun livello nel LevelManager.");
                return;
            }
            GameObject levelRoot = livelli.GetArrayElementAtIndex(0).FindPropertyRelative("radice").objectReferenceValue as GameObject;
            if (levelRoot == null)
            {
                Debug.LogError("[EnemySceneSetup] Radice Livello 1 non trovata.");
                return;
            }

            // Idempotenza: rimuovi un eventuale setup precedente sotto la
            // radice del livello, cosi' lo script e' ri-eseguibile.
            RemoveExisting(levelRoot.transform, NpcName);
            RemoveExisting(levelRoot.transform, TerritoryName);

            // --- Territorio (EnemyDetectionZone) ---
            GameObject territory = new GameObject(TerritoryName, typeof(BoxCollider), typeof(EnemyDetectionZone));
            territory.transform.SetParent(levelRoot.transform, false);
            Vector3 lvlWorld = levelRoot.transform.position;
            territory.transform.position = new Vector3(lvlWorld.x, lvlWorld.y, lvlWorld.z);
            BoxCollider bc = territory.GetComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(20f, 5f, 20f);
            bc.center = Vector3.zero;
            EnemyDetectionZone zone = territory.GetComponent<EnemyDetectionZone>();

            // --- Kart NPC (copia del kart del giocatore) ---
            // Instantiate con worldPositionStays=true poi riparentizza sotto
            // la radice del livello: il clone eredita TUTTA la gerarchia e i
            // riferimenti interni del KartController vengono rimappati sui
            // figli del clone (non bisogna riassegnarli a mano).
            GameObject npc = Object.Instantiate(playerKart);
            npc.transform.SetParent(levelRoot.transform, true);
            npc.name = NpcName;
            npc.tag = "Enemy"; // altrimenti pickup/trigger del giocatore lo riconoscerebbero come Player

            // Riassegna il tag "Enemy" a TUTTI i discendenti: Instantiate copia
            // i tag dei figli dal kart sorgente, e i figli del kart giocatore
            // sono taggati "Player" (sfera solida + capsula trigger "CollisioniMURI").
            // Senza questo, il NPC avrebbe collider propri taggati Player e si
            // auto-rileverebbe nel territorio (PlayerInside sempre true -> lock
            // che non si resetta -> inseguimento infinito), raccoglierebbe
            // pickup e triggererebbe camera/finelivello. GetComponentsInChildren
            // (true) include i figli inattivi.
            foreach (Transform t in npc.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && !t.CompareTag("Enemy"))
                    t.gameObject.tag = "Enemy";
            }

            // Rimuovi l'input del giocatore e aggiungi il cervello NPC.
            KartInput playerInput = npc.GetComponent<KartInput>();
            if (playerInput != null)
                Object.DestroyImmediate(playerInput);
            EnemyKart ek = npc.AddComponent<EnemyKart>();

            // Attiva la sterzata costante sul KartController del NPC anche
            // serializzata (visible in Inspector; viene comunque riconfermata
            // in runtime da EnemyKart.Awake).
            KartController npcKart = npc.GetComponent<KartController>();
            if (npcKart != null)
            {
                SerializedObject kSO = new SerializedObject(npcKart);
                SerializedProperty ai = kSO.FindProperty("aiSteeringMode");
                if (ai != null)
                    ai.boolValue = true;
                kSO.ApplyModifiedProperties();
            }

            // Collega il territorio all'EnemyKart (campo privato serializzato
            // 'territoryZone'): lo settiamo via SerializedObject perche' e'
            // private.
            SerializedObject ekSO = new SerializedObject(ek);
            SerializedProperty tz = ekSO.FindProperty("territoryZone");
            if (tz != null)
                tz.objectReferenceValue = zone;
            ekSO.ApplyModifiedProperties();

            // Posiziona il NPC al centro del territorio, appena sopra il
            // terreno (l'utente puo' spostarlo a mano nel livello reale).
            Vector3 tpos = territory.transform.position;
            npc.transform.position = new Vector3(tpos.x, tpos.y + 1.2f, tpos.z);
            npc.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EnemySceneSetup] Setup completato: NPC '" + NpcName + "' + territorio '" + TerritoryName + "' creati sotto '" + levelRoot.name + "'.");
        }

        private static void RemoveExisting(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name)
                {
                    Object.DestroyImmediate(c.gameObject);
                    i--;
                }
            }
        }

        // Cerca un discendente per nome (ricorsivo), anche piu' livelli sotto.
        private static Transform FindDescendant(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c == null) continue;
                if (c.name == name) return c;
                Transform r = FindDescendant(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // Fix standalone (ri-eseguibile): retagga a "Enemy" TUTTI i discendenti
        // di un NPC gia' creato in scena, SENZA toccare posizione/riferimenti.
        // Pensato per correggere NPC creati prima che SetupEnemyNpc retaggasse
        // i figli (i quali avevano collider taggati Player copiati dal kart
        // sorgente -> auto-rilevamento nel territorio). Eseguibile via batch:
        //   Unity -batchmode -quit -executeMethod ArcadeKart.EditorTools.EnemySceneSetup.FixNpcTags
        public static void FixNpcTags()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm == null)
            {
                Debug.LogError("[EnemySceneSetup] LevelManager non trovato.");
                return;
            }
            SerializedObject lmSO = new SerializedObject(lm);
            SerializedProperty livelli = lmSO.FindProperty("livelli");
            if (livelli == null || livelli.arraySize == 0)
            {
                Debug.LogError("[EnemySceneSetup] Nessun livello nel LevelManager.");
                return;
            }
            GameObject levelRoot = livelli.GetArrayElementAtIndex(0).FindPropertyRelative("radice").objectReferenceValue as GameObject;
            if (levelRoot == null)
            {
                Debug.LogError("[EnemySceneSetup] Radice Livello 1 non trovata.");
                return;
            }

            Transform npc = FindDescendant(levelRoot.transform, NpcName);
            if (npc == null)
            {
                Debug.LogError("[EnemySceneSetup] NPC '" + NpcName + "' non trovato sotto '" + levelRoot.name + "'. Esegui prima SetupEnemyNpc.");
                return;
            }

            // Retagga a "Enemy" ogni discendente (collider inclusi). La root e'
            // gia' "Enemy"; i figli erano "Player" (copiati da Instantiate).
            int retagged = 0;
            foreach (Transform t in npc.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.CompareTag("Enemy")) continue;
                t.gameObject.tag = "Enemy";
                retagged++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EnemySceneSetup] FixNpcTags: retaggati " + retagged + " discendenti di '" + NpcName + "' a 'Enemy'.");
        }
    }
}
