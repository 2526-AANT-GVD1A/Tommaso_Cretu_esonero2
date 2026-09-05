using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcadeKart.Core;

namespace ArcadeKart.Menu
{
    // Intro di avvio: al primo avvio dell'applicazione copre lo schermo con un
    // velo (nero di default) e lo fa dissolvere rivelando la scena, poi
    // attiva il menu. E' unica per avvio: un flag statico (che si azzera da
    // solo al riavvio dell'app) impedisce che si ripresenti nella stessa
    // sessione, quindi il ritorno al menu (Esc, fine livello o bottone) NON
    // riproduce mai l'intro: LevelManager.TornaAlMenu continua a fare
    // semplicemente SetActive(true) sul menu.
    //
    // Estendibile dall'Inspector senza toccare il codice:
    // - logoSprite: appare centrata sul velo e svanisce insieme a esso;
    // - suonoIntro: parte col velo coprente, su un oggetto separato dall'
    //   overlay, cosi' puo' durare anche piu' della intro stessa;
    // - audioSottoConFadeOut: se attivo, i suoni di scena/menu presenti
    //   all'avvio vengono silenziati sotto la intro e rientrano in
    //   dissolvenza insieme al velo; se disattivo si sentono da subito.
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

        [SerializeField, Tooltip("Suono della intro (es. jingle). Parte con il velo gia' coprente.")]
        private AudioClip suonoIntro;

        [SerializeField, Tooltip("Volume del suono della intro.")]
        private float volumeSuonoIntro = 1f;

        [Header("Tempi")]
        [SerializeField, Tooltip("Secondi di velo pieno prima che inizi la dissolvenza.")]
        private float durataNero = 0.4f;

        [SerializeField, Tooltip("Durata della dissolvenza del velo (in secondi).")]
        private float durataDissolvenza = 2f;

        [Header("Audio di scena")]
        [SerializeField, Tooltip("Se attivo, i suoni di scena/menu presenti all'avvio sono silenziati sotto la intro e rientrano in dissolvenza insieme al velo. Se disattivo, si sentono da subito sotto la intro.")]
        private bool audioSottoConFadeOut = true;

        // Overlay creato a runtime: Canvas sopra ogni altra UI, con velo e
        // (se assegnato) logo. Un solo alpha (CanvasGroup) comanda tutto.
        private GameObject overlay;
        private CanvasGroup gruppoOverlay;

        // Sorgenti di scena silenziate durante l'intro e i loro volumi
        // originali (parallel Index: stessa lunghezza e stesso ordine).
        private readonly List<AudioSource> sorgentiSilenziate = new List<AudioSource>();
        private readonly List<float> volumiOriginali = new List<float>();

        private AudioSource sorgenteSuonoIntro;

        private void Awake()
        {
            // Intro univoca per avvio: se e' gia' stata riprodotta in questa
            // sessione (es. reload della scena) non tocca nulla: il menu e'
            // gia' attivo nel file di scena e appare subito.
            if (introGiaRiprodotta)
                return;

            CostruisciOverlay();

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
            // quindi MenuControls non puo' farlo). A fine intro e' il
            // MenuControls.OnEnable del menu che si attiva a ricongelarlo e
            // liberare il cursore: identico a LevelManager.TornaAlMenu.
            RisolviKart();

            if (kart != null)
                kart.SetControlsEnabled(false);
            else
                Debug.LogWarning("[IntroAvvio] Nessun KartController trovato (tag Player): il kart non e' congelato durante la intro.", this);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (audioSottoConFadeOut)
                SilenziaAudioDiScena();

            RiproduciSuonoIntro();

            StartCoroutine(SequenzaIntro());
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

        // Salva e azzera il volume di ogni AudioSource attiva in scena (suoni
        // di menu/scena). Le sorgenti nate dopo (es. SFX dei trigger) non
        // esistono ancora: nasceranno col loro volume e non vanno toccate.
        private void SilenziaAudioDiScena()
        {
            AudioSource[] tutte = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (AudioSource sorgente in tutte)
            {
                if (sorgente == null || sorgente == sorgenteSuonoIntro)
                    continue;

                sorgentiSilenziate.Add(sorgente);
                volumiOriginali.Add(sorgente.volume);
                sorgente.volume = 0f;
            }
        }

        // Rientro dei suoni di scena, sincronizzato con la dissolvenza del
        // velo (fattore 0 = muto, 1 = volume originale). A fattore 1 i volumi
        // esatti di partenza sono ripristinati e le sorgenti non vengono piu'
        // toccate.
        private void RipristinaAudio(float fattore)
        {
            for (int i = 0; i < sorgentiSilenziate.Count; i++)
            {
                AudioSource sorgente = sorgentiSilenziate[i];
                if (sorgente == null)
                    continue;

                sorgente.volume = volumiOriginali[i] * fattore;
            }

            if (fattore >= 1f)
            {
                sorgentiSilenziate.Clear();
                volumiOriginali.Clear();
            }
        }

        // Il suono della intro vive su un oggetto figlio del gestore (non
        // dell'overlay): se il clip dura piu' della intro continua a suonare
        // finche' non finisce, poi si autodistrugge.
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

        // 1) velo pieno per durataNero; 2) dissolvenza del velo (e del logo)
        // con eventuale rientro sincronizzato dei suoni di scena; 3)
        // attivazione del menu e chiusura dell'intro.
        private IEnumerator SequenzaIntro()
        {
            float nero = Mathf.Max(0f, durataNero);
            if (nero > 0f)
                yield return new WaitForSecondsRealtime(nero);

            float dissolvenza = Mathf.Max(0f, durataDissolvenza);
            float tempo = 0f;
            while (tempo < dissolvenza)
            {
                tempo += Time.unscaledDeltaTime;
                float normale = Mathf.Clamp01(tempo / dissolvenza);
                float easing = normale * normale * (3f - 2f * normale); // dolce ai bordi

                gruppoOverlay.alpha = 1f - easing;
                if (audioSottoConFadeOut)
                    RipristinaAudio(easing);

                yield return null;
            }

            gruppoOverlay.alpha = 0f;
            if (audioSottoConFadeOut)
                RipristinaAudio(1f);

            // Il menu si attiva: MenuControls.OnEnable ricongela il kart e
            // libera il cursore, come gia' avviene al ritorno al menu.
            if (oggettoMenu != null)
                oggettoMenu.SetActive(true);

            introGiaRiprodotta = true;

            if (overlay != null)
                Destroy(overlay);
        }
    }
}
