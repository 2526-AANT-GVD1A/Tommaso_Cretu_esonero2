using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ArcadeKart.Core;
using ArcadeKart.Utility;

namespace ArcadeKart.Gameplay
{
    [Serializable]
    public class VoceLivello
    {
        [Tooltip("Nome del livello, mostrato come etichetta del bottone nel menu.")]
        public string nome;

        [Tooltip("Radice della mappa del livello. Viene attivata al caricamento e disattivata al ritorno al menu.")]
        public GameObject radice;
    }

    // Gestisce il caricamento dei livelli: ogni livello e' un oggetto radice
    // in scena (posizione fissa) che viene attivato/disattivato interamente.
    // Flusso: menu aperto -> il giocatore sceglie un livello -> la radice si
    // attiva; tasto Esc -> la radice si disattiva, il kart torna sul punto
    // spawn della piattaforma e il menu si riapre.
    public class LevelManager : MonoBehaviour
    {
        [Header("Livelli")]
        [SerializeField, Tooltip("Livelli disponibili nel menu. Estendibile dall'Inspector.")]
        private List<VoceLivello> livelli = new List<VoceLivello>();

        [Header("Riferimenti")]
        [SerializeField, Tooltip("KartController del giocatore. Se vuoto, cerca il primo oggetto col tag Player.")]
        private KartController kart;

        [SerializeField, Tooltip("KartCollectedStack del giocatore (torre degli oggetti raccolti). Se vuoto, lo cerca sul kart in Awake.")]
        private KartCollectedStack stack;

        [SerializeField, Tooltip("Oggetto del menu da riaprire al ritorno (tasto Esc).")]
        private GameObject oggettoMenu;

        [SerializeField, Tooltip("Oggetto del menu di FINE livello da attivare quando il kart tocca il trigger di fine.")]
        private GameObject oggettoMenuFine;

        [SerializeField, Tooltip("Transform del punto di spawn sulla piattaforma.")]
        private Transform puntoSpawn;

        [SerializeField, Tooltip("Camera a fasi da resettare al ritorno al menu.")]
        private PhasedFollowCamera cameraFasi;

        [SerializeField, Tooltip("ID della fase camera da ripristinare al ritorno al menu.")]
        private string faseCameraDefault = "Default";

        public int LivelloCorrente { get; private set; } = -1;

        public IReadOnlyList<VoceLivello> Livelli => livelli;

        // True quando il kart ha toccato il trigger di fine livello e il
        // Menu_Fine e' aperto. Serve anche a bloccare il tasto Esc finche'
        // il giocatore non decide se tornare al menu (altrimenti Esc aprirebbe
        // Menu_Inizio sotto Menu_Fine, con i due menu sovrapposti e un bug sui
        // controlli del kart).
        private bool partitaFinita;

        private void Awake()
        {
            if (kart == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    kart = player.GetComponent<KartController>();
            }

            if (stack == null && kart != null)
                stack = kart.GetComponentInChildren<KartCollectedStack>();
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame && LivelloCorrente >= 0 && !partitaFinita)
                TornaAlMenu();
        }

        public void CaricaLivello(int indice)
        {
            if (indice < 0 || indice >= livelli.Count)
            {
                Debug.LogWarning($"[LevelManager] Indice livello non valido: {indice}.", this);
                return;
            }

            if (livelli[indice].radice == null)
            {
                Debug.LogWarning($"[LevelManager] Il livello '{livelli[indice].nome}' non ha radice assegnata.", this);
                return;
            }

            if (LivelloCorrente >= 0 && LivelloCorrente != indice)
            {
                GameObject vecchia = livelli[LivelloCorrente].radice;
                if (vecchia != null)
                    vecchia.SetActive(false);
            }

            livelli[indice].radice.SetActive(true);

            // Riattiva tutti i Pickup figli della radice: quelli raccolti in
            // una sessione precedente si sono disattivati da soli con
            // SetActive(false), e riattivare il genitore NON ribalta l'active
            // dei figli (Unity). OnDisable del Pickup ha gia' ripristinato lo
            // stato interno (transform, collider, collected), quindi basta
            // riattivarli per renderli di nuovo raccoglibili.
            ReenablePickups(livelli[indice].radice);

            LivelloCorrente = indice;
        }

        // Chiamato dal TriggerFineLivello quando il kart arriva in fondo al
        // livello. Attiva il Menu_Fine: il suo MenuControls spenge i controlli
        // del kart (freeze), libera il cursore e mostra il punteggio. Il
        // livello resta caricato finche' il giocatore non preme il bottone per
        // tornare al menu (che chiama TornaAlMenu). Idempotente: se chiamato
        // piu' volte o senza un livello in corso, non fa nulla.
        public void TerminaLivello()
        {
            if (partitaFinita || LivelloCorrente < 0)
                return;

            if (oggettoMenuFine == null)
            {
                Debug.LogWarning("[LevelManager] oggettoMenuFine non assegnato: impossibile aprire il menu di fine.", this);
                return;
            }

            partitaFinita = true;
            oggettoMenuFine.SetActive(true);
        }

        // Riattiva ogni Pickup inattivo sotto la radice (inclusi i figli
        // disattivati, grazie a GetComponentsInChildren(true)).
        private static void ReenablePickups(GameObject root)
        {
            if (root == null)
                return;

            Pickup[] pickups = root.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null && !pickups[i].gameObject.activeSelf)
                    pickups[i].gameObject.SetActive(true);
            }
        }

        public void TornaAlMenu()
        {
            // La partita non e' piu' "finita" e il Menu_Fine si chiude. Va
            // fatto PRIMA di attivare Menu_Inizio (alla fine del metodo),
            // cosi' l'OnDisable di Menu_Fine (che riaccende i controlli del
            // kart) gira prima dell'OnEnable di Menu_Inizio (che li rispegne):
            // ordine inverso lascerebbe i controlli accesi col menu aperto.
            partitaFinita = false;
            if (oggettoMenuFine != null && oggettoMenuFine.activeSelf)
                oggettoMenuFine.SetActive(false);

            if (LivelloCorrente >= 0 && LivelloCorrente < livelli.Count)
            {
                GameObject corrente = livelli[LivelloCorrente].radice;
                if (corrente != null)
                    corrente.SetActive(false);
            }
            LivelloCorrente = -1;

            // Svuota la torre e azzera il contatore: i pickup respawnano a
            // ogni riattivazione del livello, quindi senza questo reset il
            // giocatore accumulerebbe punteggio all'infinito riraccogliendo
            // gli stessi oggetti. Va fatto prima di muovere il kart con
            // RespawnAt, cosi' eventuali logiche visive della torre non
            // lampeggiano durante il teleport.
            if (stack != null)
            {
                stack.ClearAll();
                stack.ResetTotal();
            }

            if (kart != null && puntoSpawn != null)
            {
                kart.RespawnAt(puntoSpawn);
            }
            else
            {
                Debug.LogWarning("[LevelManager] Kart o puntoSpawn non assegnati: il kart non torna sulla piattaforma.", this);
            }

            if (cameraFasi != null)
                cameraFasi.SetPhase(faseCameraDefault, PhasedFollowCamera.TransitionMode.Snap);

            if (oggettoMenu != null)
                oggettoMenu.SetActive(true);
        }
    }
}
