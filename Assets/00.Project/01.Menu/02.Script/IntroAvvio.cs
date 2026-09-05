using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ArcadeKart.Core;

namespace ArcadeKart.Menu
{
    // Intro di avvio: al primo avvio dell'applicazione copre lo schermo con un
    // velo (nero di default) e lo fa dissolvere rivelando la scena; il menu si
    // attiva all'inizio della dissolvenza, cosi' il velo lo rivela
    // progressivamente. E' unica per avvio: un flag statico (che si azzera da
    // solo al riavvio dell'app) impedisce che si ripresenti nella stessa
    // sessione, quindi il ritorno al menu (Esc, fine livello o bottone) NON
    // riproduce mai l'intro: LevelManager.TornaAlMenu continua a fare
    // semplicemente SetActive(true) sul menu.
    //
    // Estendibile dall'Inspector senza toccare il codice:
    // - logoSprite: appare centrata sul velo e svanisce insieme a esso;
    // - suonoIntro: parte con il velo coprente, su un oggetto separato
    //   dall'overlay, cosi' puo' durare anche piu' della intro stessa;
    // - audioSottoConFadeOut: se attivo, il volume GLOBALE dell'applicazione
    //   (AudioListener.volume) viene portato a 0 e rientra durante la
    //   dissolvenza. Un solo comando per TUTTO l'audio: copre qualsiasi
    //   sorgente, anche quelle create a runtime (musica BG di TriggerAudio,
    //   SFX di PlayClipAtPoint) o riscritte ogni frame, senza doverle
    //   elencare. Il jingle della intro passa anch'esso dal listener, quindi
    //   rientra insieme a tutto il resto.
    public class IntroAvvio : MonoBehaviour
    {
        // Vero dopo la prima intro della sessione: campo statico, si azzera
        // automaticamente quando l'applicazione riparte. Il reset esplicito
        // serve solo se in editor viene disattivato il domain reload.
        private static bool introGiaRiprodotta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIntroGiaRiprodotta()
        {
            introGiaRiprodotta = false;
        }

        [Header("Riferimenti")]
        [SerializeField, Tooltip("Oggetto del menu da attivare a fine intro (Menu_Inizio). Durante l'intro resta disattivato.")]
        private GameObject oggettoMenu;

        [SerializeField, Tooltip("KartController del giocatore da congelare durante la intro. Se vuoto, cerca il primo KartController attivo col tag Player.")]
        private KartController kart;

        [Header("Contenuti (facoltativi)")]
        [SerializeField, Tooltip("Logo da mostrare sopra il velo. Vuoto = solo velo colorato.")]
        private Sprite logoSprite;

        [SerializeField, Tooltip("Frazione dell'altezza dello schermo occupata dal logo.")]
        private float frazioneAltezzaLogo = 0.4f;

        [SerializeField, Tooltip("Colore del velo a schermo pieno.")]
        private Color coloreSfondo = Color.black;

        [SerializeField, Tooltip("Suono della intro (es. jingle). Parte col velo coprente ma il volume globale e' a 0: rientra durante la dissolvenza, insieme a tutto il resto.")]
        private AudioClip suonoIntro;

        [SerializeField, Tooltip("Volume del suono della intro.")]
        private float volumeSuonoIntro = 1f;

        [Header("Tempi")]
        [SerializeField, Tooltip("Secondi di velo pieno prima che inizi la dissolvenza.")]
        private float durataNero = 0.4f;

        [SerializeField, Tooltip("Durata della dissolvenza del velo (in secondi).")]
        private float durataDissolvenza = 2f;

        [Header("Audio di scena")]
        [SerializeField, Tooltip("Se attivo, il volume globale dell'applicazione (AudioListener) e' a 0 durante il velo pieno e rientra gradualmente al 100% durante la dissolvenza: copre ogni suono della scena senza configurare nulla. Se disattivo, l'audio si sente da subito sotto la intro.")]
        private bool audioSottoConFadeOut = true;

        // Overlay creato a runtime: Canvas sopra ogni altra UI, con velo e
        // (se assegnato) logo. Un solo alpha (CanvasGroup) comanda tutto.
        private GameObject overlay;
        private CanvasGroup gruppoOverlay;

        // Volume globale (AudioListener): salvato all'avvio e ripristinato
        // esattamente a fine intro. Un solo knob per l'intera scena.
        private float volumeGlobaleOriginale = 1f;
        private bool hoAbbassatoVolumeGlobale;

        private AudioSource sorgenteSuonoIntro;

        private void Awake()
        {
            // Intro univoca per avvio: se e' gia' stata riprodotta in questa
            // sessione (es. reload della scena) non tocca nulla: il menu e'
            // gia' attivo nel file di scena e appare subito.
            if (introGiaRiprodotta)
                return;

            CostruisciOverlay();

            // Un solo comando mette a silenzio QUALSIASI suono presente e
            // futuro (sorgenti persistenti, create a runtime, reverb compresa):
            // sotto il velo pieno non si deve sentire nulla. Rientra durante
            // la dissolvenza, sincronizzato con l'alpha del velo.
            if (audioSottoConFadeOut)
            {
                volumeGlobaleOriginale = AudioListener.volume;
                AudioListener.volume = 0f;
                hoAbbassatoVolumeGlobale = true;
                Debug.Log("[IntroAvvio] Volume globale (AudioListener) a 0: tutto l'audio rientra durante la dissolvenza.", this);
            }

            // Menu nascosto prima del primo frame: nessun flash a schermo.
            if (oggettoMenu != null)
                oggettoMenu.SetActive(false);
            else
                Debug.LogWarning("[IntroAvvio] oggettoMenu non assegnato: a fine intro nessun menu verra' attivato.", this);
        }

        private void Start()
        {
            if (introGiaRiprodotta)
                return;

            // Il kart non deve rispondere durante la intro (il menu e' spento,
            // quindi MenuControls non puo' farlo). A inizio dissolvenza e' il
            // MenuControls.OnEnable del menu che si attiva a ricongelarlo e
            // liberare il cursore: identico a LevelManager.TornaAlMenu.
            RisolviKart();

            if (kart != null)
                kart.SetControlsEnabled(false);
            else
                Debug.LogWarning("[IntroAvvio] Nessun KartController trovato (tag Player): il kart non e' congelato durante la intro.", this);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            RiproduciSuonoIntro();

            StartCoroutine(SequenzaIntro());
        }

        // Safety: se l'oggetto viene distrutto prima di aver completato
        // l'intro (stop Play in editor, reload di scena), il volume globale
        // non deve restare a 0.
        private void OnDestroy()
        {
            if (hoAbbassatoVolumeGlobale)
            {
                AudioListener.volume = volumeGlobaleOriginale;
                hoAbbassatoVolumeGlobale = false;
            }
        }

        // Usa il kart assegnato dall'Inspector; in assenza cerca il primo
        // KartController attivo col tag Player. NON basta FindWithTag("Player"):
        // in scena altri oggetti portano lo stesso tag (es. Collisioni_MURI,
        // sfera) e il primo trovato non e' detto che sia il kart.
        private void RisolviKart()
        {
            if (kart != null)
                return;

            KartController[] candidati = FindObjectsByType<KartController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (KartController candidato in candidati)
            {
                if (candidato != null && candidato.gameObject.CompareTag("Player"))
                {
                    kart = candidato;
                    return;
                }
            }
        }

        // Crea il Canvas overlay (sopra ogni altra UI) con velo colorato e,
        // se assegnato, il logo. Tutto procedurale: nessun prefab da creare.
        private void CostruisciOverlay()
        {
            overlay = new GameObject("IntroAvvio_Overlay");
            Canvas canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            gruppoOverlay = overlay.AddComponent<CanvasGroup>();
            gruppoOverlay.alpha = 1f;
            gruppoOverlay.interactable = false;
            gruppoOverlay.blocksRaycasts = true; // sotto la intro non deve essere cliccabile nulla

            // Il velo riceve i click (blocca tutto cio' che sta sotto).
            Image velo = CreaImmagine(overlay.transform, "IntroAvvio_Velo", coloreSfondo);
            velo.raycastTarget = true;

            if (logoSprite != null)
            {
                Image logo = CreaImmagine(overlay.transform, "IntroAvvio_Logo", Color.white);
                logo.sprite = logoSprite;
                logo.preserveAspect = true;
                logo.raycastTarget = false;

                RectTransform rect = (RectTransform)logo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;

                float altezza = Mathf.Clamp01(frazioneAltezzaLogo) * Screen.height;
                float proporzione = logoSprite.rect.width / Mathf.Max(1f, logoSprite.rect.height);
                rect.sizeDelta = new Vector2(altezza * proporzione, altezza);
            }
        }

        // Crea una Image figlia a schermo pieno (anchors 0-1); per il logo le
        // anchors vengono poi ristrette al centro dal chiamante.
        private static Image CreaImmagine(Transform genitore, string nome, Color colore)
        {
            GameObject oggetto = new GameObject(nome);
            oggetto.transform.SetParent(genitore, false);

            RectTransform rect = oggetto.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image immagine = oggetto.AddComponent<Image>();
            immagine.color = colore;
            return immagine;
        }

        // Il suono della intro vive su un oggetto figlio del gestore (non
        // dell'overlay): se il clip dura piu' della intro continua a suonare
        // finche' non finisce, poi si autodistrugge. Passa dal volume globale:
        // nel velo pieno e' muto e rientra durante la dissolvenza.
        private void RiproduciSuonoIntro()
        {
            if (suonoIntro == null)
                return;

            GameObject oggetto = new GameObject("IntroAvvio_Audio");
            oggetto.transform.SetParent(transform, false);

            sorgenteSuonoIntro = oggetto.AddComponent<AudioSource>();
            sorgenteSuonoIntro.playOnAwake = false;
            sorgenteSuonoIntro.clip = suonoIntro;
            sorgenteSuonoIntro.volume = Mathf.Clamp01(volumeSuonoIntro);
            sorgenteSuonoIntro.spatialBlend = 0f;
            sorgenteSuonoIntro.Play();

            StartCoroutine(DistruggiAlTermine(oggetto));
        }

        private IEnumerator DistruggiAlTermine(GameObject oggetto)
        {
            while (sorgenteSuonoIntro != null && sorgenteSuonoIntro.isPlaying)
                yield return null;

            if (oggetto != null)
                Destroy(oggetto);
        }

        // 1) velo pieno per durataNero (volume globale a 0); 2) attivazione
        // del menu + dissolvenza del velo (e del logo) con rientro globale
        // dell'audio sincronizzato all'alpha; 3) chiusura dell'intro.
        private IEnumerator SequenzaIntro()
        {
            float nero = Mathf.Max(0f, durataNero);
            if (nero > 0f)
                yield return new WaitForSecondsRealtime(nero);

            // Il menu si attiva A INIZIO dissolvenza: il velo ancora coprente
            // lo rivela progressivamente e la sua musica (se sotto oggettoMenu,
            // spenta finche' il menu e' inattivo) parte subito, ma il volume
            // globale e' ancora a 0: rientra insieme a tutto il resto senza
            // scatti. MenuControls.OnEnable ricongela il kart e libera il
            // cursore, come gia' avviene al ritorno al menu.
            if (oggettoMenu != null)
                oggettoMenu.SetActive(true);

            float dissolvenza = Mathf.Max(0f, durataDissolvenza);
            float tempo = 0f;
            while (tempo < dissolvenza)
            {
                tempo += Time.unscaledDeltaTime;
                float normale = Mathf.Clamp01(tempo / dissolvenza);
                float easing = normale * normale * (3f - 2f * normale); // dolce ai bordi

                gruppoOverlay.alpha = 1f - easing;
                if (audioSottoConFadeOut)
                    AudioListener.volume = volumeGlobaleOriginale * easing;

                yield return null;
            }

            gruppoOverlay.alpha = 0f;
            if (audioSottoConFadeOut)
            {
                // Ripristino esatto del volume di partenza: la intro non tocca
                // piu' nulla da qui in avanti.
                AudioListener.volume = volumeGlobaleOriginale;
                hoAbbassatoVolumeGlobale = false;
            }

            introGiaRiprodotta = true;

            if (overlay != null)
                Destroy(overlay);
        }
    }
}
