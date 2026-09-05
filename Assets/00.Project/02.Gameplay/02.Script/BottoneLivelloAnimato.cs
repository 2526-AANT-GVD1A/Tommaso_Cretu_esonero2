using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcadeKart.Gameplay
{
    // Cosa fa il bottone al termine dell'animazione di uscita.
    public enum AzioneBottone
    {
        // Carica il livello all'indice scelto (bottoni del menu d'inizio).
        CaricaLivello = 0,
        // Torna al menu d'inizio (bottone del menu di fine livello).
        TornaAlMenu = 1
    }

    // Abbellimento del bottone del menu: si ingrandisce e si dondola
    // (oscillazione sinistra/destra) quando il mouse passa sopra e, al click,
    // saltella su e giu per un secondo prima di scivolare fuori dallo schermo
    // verso destra; solo allora carica il livello e chiude il menu (le vecchie
    // chiamate OnClick sono spostate qui per poterle ritardare dopo
    // l'animazione). Quando un bottone viene premuto, tutti gli altri bottoni
    // fratelli del menu diventano inselezionabili e lo seguono nell'uscita
    // sincronizzata dallo schermo.
    public class BottoneLivelloAnimato : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Riferimenti")]
        [SerializeField, Tooltip("LevelManager che carica il livello scelto.")]
        private LevelManager levelManager;

        [SerializeField, Tooltip("Oggetto del menu da disattivare dopo l'animazione.")]
        private GameObject oggettoMenu;

        [SerializeField, Tooltip("Indice del livello da caricare (usato solo se azione = CaricaLivello).")]
        private int indiceLivello;

        [SerializeField, Tooltip("Cosa fare dopo l'animazione: CaricaLivello (menu d'inizio) o TornaAlMenu (menu di fine).")]
        private AzioneBottone azione = AzioneBottone.CaricaLivello;

        [Header("Ingrandimento al passaggio del mouse")]
        [SerializeField, Tooltip("Fattore di ingrandimento relativo alla scala impostata nell'editor (1 = uguale, 1.12 = 12% piu' grande).")]
        private float scalaHover = 1.12f;

        [SerializeField, Tooltip("Velocita' dell'ingrandimento/riduzione.")]
        private float velocitaScala = 12f;

        [Header("Dondolio al passaggio del mouse")]
        [SerializeField, Tooltip("Ampiezza massima del dondolio sinistra/destra (in gradi).")]
        private float ampiezzaDondolio = 5f;

        [SerializeField, Tooltip("Oscillazioni complete del dondolio al secondo.")]
        private float oscillazioniDondolio = 2.5f;

        [Header("Suoni del bottone")]
        [SerializeField, Tooltip("Suono riprodotto quando il mouse entra nel bottone. Vuoto = nessun suono.")]
        private AudioClip suonoHover;

        [SerializeField, Tooltip("Suono riprodotto quando il bottone viene premuto (parte insieme all'animazione del click). Vuoto = nessun suono.")]
        private AudioClip suonoClick;

        [SerializeField, Tooltip("Volume dei suoni di hover e click."), Range(0f, 1f)]
        private float volumeSuoni = 1f;

        [Header("Animazione al click")]
        [SerializeField, Tooltip("Durata del saltello su e giu (in secondi).")]
        private float durataSaltello = 1f;

        [SerializeField, Tooltip("Ampiezza massima del saltello (in pixel).")]
        private float ampiezzaSaltello = 22f;

        [SerializeField, Tooltip("Numero di saltelli durante l'animazione.")]
        private int numeroSaltelli = 3;

        [SerializeField, Tooltip("Durata dell'uscita dallo schermo (in secondi).")]
        private float durataUscita = 0.4f;

        [SerializeField, Tooltip("Margine extra oltre il bordo destro del canvas.")]
        private float margineUscita = 80f;

        private Button bottone;
        private RectTransform rectTransform;
        private bool mouseSopra;
        private bool inAnimazione;
        private Vector2 posizioneBase;
        private Quaternion rotazioneBase;

        // Scala impostata nell'editor (catturata in Awake): hover e ripristini
        // lavorano come fattore moltiplicativo sopra questa base, cosi' un
        // bottone ingrandito con lo strumento Scala dell'editor non viene
        // riportato a scala 1 all'avvio.
        private Vector3 scalaBase;

        // Sorgente unica dei suoni di hover e click, creata sull'oggetto del
        // bottone in Awake (2D, non spazializzata). Riproducendo sempre qui
        // un rientro rapido del mouse riparte da capo e il click sostituisce
        // l'eventuale hover ancora in corso, senza sovrapposizioni.
        private AudioSource sorgenteSuoni;

        // Stato del dondolio: tempo di hover accumulato (la fase dell'onda,
        // mantenuta se il mouse esce e rientra) e peso 0..1 che fa entrare e
        // uscire l'oscillazione in dolcezza, senza scatti.
        private float tempoDondolio;
        private float pesoDondolio;

        private void Awake()
        {
            bottone = GetComponent<Button>();
            rectTransform = (RectTransform)transform;
            posizioneBase = rectTransform.anchoredPosition;
            rotazioneBase = rectTransform.localRotation;
            scalaBase = rectTransform.localScale;

            sorgenteSuoni = gameObject.AddComponent<AudioSource>();
            sorgenteSuoni.playOnAwake = false;
            sorgenteSuoni.spatialBlend = 0f;
            sorgenteSuoni.dopplerLevel = 0f;
        }

        private void OnEnable()
        {
            // Al ritorno al menu (tasto Esc) il bottone torna allo stato iniziale.
            // posizioneBase e' quella catturata in Awake (la posizione disegnata
            // nell'editor): NON va azzerata, altrimenti il bottone salta al centro
            // del menu sovrapponendosi agli altri.
            inAnimazione = false;
            mouseSopra = false;
            tempoDondolio = 0f;
            pesoDondolio = 0f;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = posizioneBase;
                rectTransform.localScale = scalaBase;
                rectTransform.localRotation = rotazioneBase;
            }
            if (bottone != null)
                bottone.interactable = true;
        }

        private void Update()
        {
            if (inAnimazione)
                return;

            // Il hover scala relativamente alla scala base disegnata
            // nell'editor: il fattore (1 a riposo, scalaHover col mouse sopra)
            // viene moltiplicato per scalaBase, cosi' la dimensione scelta in
            // editor non viene sovrascritta dall'animazione.
            float fattoreTarget = mouseSopra ? scalaHover : 1f;
            float fattoreAttuale = rectTransform.localScale.x / Mathf.Max(0.0001f, scalaBase.x);
            float fattore = Mathf.Lerp(fattoreAttuale, fattoreTarget, Time.deltaTime * velocitaScala);
            rectTransform.localScale = scalaBase * fattore;

            AggiornaDondolio();
        }

        // Oscillazione sinistra/destra attorno alla rotazione di base mentre
        // il mouse e' sopra il bottone: un'onda sinusoidale il cui peso sale
        // e scende in dolcezza, cosi' il dondolio parte, si ferma e riparte
        // senza scatti. A peso zero la rotazione torna esattamente a quella
        // di partenza catturata in Awake.
        private void AggiornaDondolio()
        {
            if (mouseSopra)
                tempoDondolio += Time.deltaTime;

            float pesoTarget = mouseSopra ? 1f : 0f;
            float velocitaPeso = mouseSopra ? velocitaScala : velocitaScala * 0.5f;
            pesoDondolio = Mathf.MoveTowards(pesoDondolio, pesoTarget, Time.deltaTime * velocitaPeso);

            if (pesoDondolio <= 0f)
            {
                rectTransform.localRotation = rotazioneBase;
                tempoDondolio = 0f;
                return;
            }

            float angolo = Mathf.Sin(tempoDondolio * oscillazioniDondolio * Mathf.PI * 2f) * ampiezzaDondolio;
            rectTransform.localRotation = rotazioneBase * Quaternion.Euler(0f, 0f, angolo * pesoDondolio);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            mouseSopra = true;
            RiproduciSuono(suonoHover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            mouseSopra = false;
        }

        // Riproduce il clip indicato sulla sorgente unica del bottone.
        // Play riparte sempre dall'inizio: suoni brevi e ravvicinati non si
        // accumulano mai uno sopra l'altro.
        private void RiproduciSuono(AudioClip clip)
        {
            if (clip == null || sorgenteSuoni == null)
                return;

            sorgenteSuoni.volume = Mathf.Clamp01(volumeSuoni);
            sorgenteSuoni.clip = clip;
            sorgenteSuoni.Play();
        }

        // Chiamato dall'OnClick del Button: prima l'animazione, poi il lavoro vero.
        public void Premi()
        {
            if (inAnimazione)
                return;

            inAnimazione = true;
            bottone.interactable = false;
            rectTransform.localScale = scalaBase;
            rectTransform.localRotation = rotazioneBase;

            RiproduciSuono(suonoClick);

            // Gli altri bottoni del menu diventano inselezionabili e, senza
            // saltellare, seguiranno questo nell'uscita sincronizzata dallo
            // schermo.
            BottoneLivelloAnimato[] fratelli = TuttiIBottoniDelMenu();
            foreach (BottoneLivelloAnimato altro in fratelli)
            {
                if (altro != this && !altro.inAnimazione)
                    altro.BloccaEPreparaUscita();
            }

            StartCoroutine(AnimazioneEUscita());
        }

        // Trova tutti i bottoni animati che condividono lo stesso contenitore
        // (i figli del Menu), cosi' il coordinamento funziona per N bottoni.
        private BottoneLivelloAnimato[] TuttiIBottoniDelMenu()
        {
            if (transform.parent == null)
                return new[] { this };

            return transform.parent.GetComponentsInChildren<BottoneLivelloAnimato>(true);
        }

        // Usato dal bottone premuto sugli altri: li blocca e li prepara a
        // scivolare fuori dallo schermo appena il premuto finisce di saltellare.
        private void BloccaEPreparaUscita()
        {
            inAnimazione = true;
            mouseSopra = false;
            if (bottone != null)
                bottone.interactable = false;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = posizioneBase;
                rectTransform.localScale = scalaBase;
                rectTransform.localRotation = rotazioneBase;
            }
        }

        // Esce dallo schermo verso destra; chiamato dal bottone premuto sugli
        // altri nello stesso frame in cui inizia la propria uscita, cosi' il
        // movimento resta sincronizzato (ognuno mantiene il proprio offset).
        private IEnumerator UscitaDalSchermo()
        {
            RectTransform canvasRect = (RectTransform)transform.root;
            float distanzaUscita = canvasRect.rect.width * 0.5f + rectTransform.rect.width * 0.5f + margineUscita;
            float tempo = 0f;
            while (tempo < durataUscita)
            {
                tempo += Time.unscaledDeltaTime;
                float normale = Mathf.Clamp01(tempo / durataUscita);
                float easing = normale * normale * normale; // partenza dolce, uscita rapida
                rectTransform.anchoredPosition = new Vector2(posizioneBase.x + distanzaUscita * easing, posizioneBase.y);
                yield return null;
            }
        }

        private IEnumerator AnimazioneEUscita()
        {
            // 1) Saltello su e giu con ampiezza che si spegne.
            float tempo = 0f;
            while (tempo < durataSaltello)
            {
                tempo += Time.unscaledDeltaTime;
                float normale = Mathf.Clamp01(tempo / durataSaltello);
                float smorzamento = 1f - normale;
                float y = Mathf.Sin(normale * numeroSaltelli * Mathf.PI * 2f) * ampiezzaSaltello * smorzamento;
                rectTransform.anchoredPosition = new Vector2(posizioneBase.x, posizioneBase.y + y);
                yield return null;
            }
            rectTransform.anchoredPosition = posizioneBase;

            // 2) Uscita dallo schermo verso destra: il premuto e tutti i fratelli
            // bloccati partono insieme, nello stesso frame.
            BottoneLivelloAnimato[] fratelli = TuttiIBottoniDelMenu();
            foreach (BottoneLivelloAnimato altro in fratelli)
            {
                if (altro != this && altro.inAnimazione)
                    altro.StartCoroutine(altro.UscitaDalSchermo());
            }
            yield return StartCoroutine(UscitaDalSchermo());

            // 3) Solo ora fa quello che doveva fare.
            if (azione == AzioneBottone.TornaAlMenu)
            {
                // Menu di fine -> menu d'inizio. Importante l'ordine: prima
                // chiude il proprio menu (il suo MenuControls.OnDisable
                // riaccende i controlli del kart), POI TornaAlMenu riapre
                // Menu_Inizio il cui MenuControls.OnEnable li rispegne. Ordine
                // inverso lascerebbe i controlli accenti col menu aperto.
                if (oggettoMenu != null)
                    oggettoMenu.SetActive(false);

                if (levelManager != null)
                    levelManager.TornaAlMenu();
                else
                    Debug.LogWarning("[BottoneLivelloAnimato] LevelManager non assegnato.", this);
            }
            else
            {
                if (levelManager != null)
                    levelManager.CaricaLivello(indiceLivello);
                else
                    Debug.LogWarning("[BottoneLivelloAnimato] LevelManager non assegnato.", this);

                if (oggettoMenu != null)
                    oggettoMenu.SetActive(false);
            }
        }
    }
}
