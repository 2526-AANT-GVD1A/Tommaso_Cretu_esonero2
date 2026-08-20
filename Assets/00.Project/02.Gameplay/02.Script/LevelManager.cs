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

        [SerializeField, Tooltip("Oggetto del menu da riaprire al ritorno (tasto Esc).")]
        private GameObject oggettoMenu;

        [SerializeField, Tooltip("Transform del punto di spawn sulla piattaforma.")]
        private Transform puntoSpawn;

        [SerializeField, Tooltip("Camera a fasi da resettare al ritorno al menu.")]
        private PhasedFollowCamera cameraFasi;

        [SerializeField, Tooltip("ID della fase camera da ripristinare al ritorno al menu.")]
        private string faseCameraDefault = "Default";

        public int LivelloCorrente { get; private set; } = -1;

        public IReadOnlyList<VoceLivello> Livelli => livelli;

        private void Awake()
        {
            if (kart == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    kart = player.GetComponent<KartController>();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame && LivelloCorrente >= 0)
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
            LivelloCorrente = indice;
        }

        public void TornaAlMenu()
        {
            if (LivelloCorrente >= 0 && LivelloCorrente < livelli.Count)
            {
                GameObject corrente = livelli[LivelloCorrente].radice;
                if (corrente != null)
                    corrente.SetActive(false);
            }
            LivelloCorrente = -1;

            if (kart != null && puntoSpawn != null)
            {
                kart.RespawnAt(puntoSpawn);
            }
            else
            {
                Debug.LogWarning("[LevelManager] Kart o puntoSpawn non assegnati: il kart non torna sulla piattaforma.", this);
            }

            if (cameraFasi != null)
                cameraFasi.SetPhase(faseCameraDefault);

            if (oggettoMenu != null)
                oggettoMenu.SetActive(true);
        }
    }
}
