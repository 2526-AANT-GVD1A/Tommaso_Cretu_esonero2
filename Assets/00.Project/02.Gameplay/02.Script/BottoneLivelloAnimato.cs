using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcadeKart.Gameplay
{
    // Abbellimento del bottone del menu: si ingrandisce leggermente quando il
    // mouse passa sopra e, al click, saltella su e giu per un secondo prima di
    // scivolare fuori dallo schermo verso destra; solo allora carica il livello
    // e chiude il menu (le vecchie chiamate OnClick sono spostate qui per
    // poterle ritardare dopo l'animazione). Quando un bottone viene premuto,
    // tutti gli altri bottoni fratelli del menu diventano inselezionabili e lo
    // seguono nell'uscita sincronizzata dallo schermo.
    public class BottoneLivelloAnimato : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Riferimenti")]
        [SerializeField, Tooltip("LevelManager che carica il livello scelto.")]
        private LevelManager levelManager;

        [SerializeField, Tooltip("Oggetto del menu da disattivare dopo l'animazione.")]
        private GameObject oggettoMenu;

        [SerializeField, Tooltip("Indice del livello da caricare (come nella lista del LevelManager).")]
        private int indiceLivello;

        [Header("Ingrandimento al passaggio del mouse")]
        [SerializeField, Tooltip("Scala raggiunta quando il mouse e' sopra il bottone.")]
        private float scalaHover = 1.12f;

        [SerializeField, Tooltip("Velocita' dell'ingrandimento/riduzione.")]
        private float velocitaScala = 12f;

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

        private void Awake()
        {
            bottone = GetComponent<Button>();
            rectTransform = (RectTransform)transform;
            posizioneBase = rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            // Al ritorno al menu (tasto Esc) il bottone torna allo stato iniziale.
            // posizioneBase e' quella catturata in Awake (la posizione disegnata
            // nell'editor): NON va azzerata, altrimenti il bottone salta al centro
            // del menu sovrapponendosi agli altri.
            inAnimazione = false;
            mouseSopra = false;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = posizioneBase;
                rectTransform.localScale = Vector3.one;
            }
            if (bottone != null)
                bottone.interactable = true;
        }

        private void Update()
        {
            if (inAnimazione)
                return;

            float scalaTarget = mouseSopra ? scalaHover : 1f;
            float scala = Mathf.Lerp(rectTransform.localScale.x, scalaTarget, Time.deltaTime * velocitaScala);
            rectTransform.localScale = new Vector3(scala, scala, 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            mouseSopra = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            mouseSopra = false;
        }

        // Chiamato dall'OnClick del Button: prima l'animazione, poi il lavoro vero.
        public void Premi()
        {
            if (inAnimazione)
                return;

            inAnimazione = true;
            bottone.interactable = false;
            rectTransform.localScale = Vector3.one;

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
                rectTransform.localScale = Vector3.one;
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

            // 3) Solo ora fa quello che doveva fare: carica il livello e chiude il menu.
            if (levelManager != null)
                levelManager.CaricaLivello(indiceLivello);
            else
                Debug.LogWarning("[BottoneLivelloAnimato] LevelManager non assegnato.", this);

            if (oggettoMenu != null)
                oggettoMenu.SetActive(false);
        }
    }
}
