using UnityEngine;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Trigger audio per il kart, stesso schema di CameraPhaseTrigger: quando
    /// il kart (tag Player) attraversa il volume, due blocchi indipendenti
    /// possono attivarsi e tornare allo stato di partenza all'uscita.
    /// - Zona effetto: una AudioReverbZone (reverb/eco, es. galleria o grotta)
    ///   figlia dell'AudioListener (la camera che segue il kart) si accende
    ///   con fade dell'intensita' e si spegne all'uscita. Le Reverb Zone di
    ///   Unity si valutano sulla posizione del LISTENER, non delle sorgenti:
    ///   col listener a distanza zero dal centro la reverb e' piena in tutto
    ///   il trigger, qualsiasi forma/dimensione abbia il collider, e tocca
    ///   anche l'audio BG 2D. (Versioni precedenti agganciavano la zona al
    ///   kart o la lasciavano fissa al centro del trigger: in entrambi i
    ///   casi la camera/listener restava fuori dai raggi e l'effetto non si
    ///   sentiva o solo in parte.)
    /// - Audio BG: una sorgente NON spazializzata (musica di zona) parte in
    ///   fade-in e sfuma in pausa all'uscita.
    /// I componenti mancanti vengono creati in automatico, come le sorgenti
    /// di AudioRuoteKart.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerAudio : MonoBehaviour
    {
        [Header("Filtering")]
        [SerializeField, Tooltip("Se assegnato, il trigger reagisce solo a questo tag. Consigliato: Player.")]
        private string requiredTag = "Player";

        [SerializeField, Tooltip("Se true, il trigger funziona una sola volta (l'effetto resta attivo, l'uscita non spegne nulla).")]
        private bool oneShot = false;

        [Header("Zona effetto (reverb/eco)")]
        [SerializeField, Tooltip("Se attivo, all'ingresso accende una AudioReverbZone con fade (mentre il trigger e' attivo l'audio in scena suona con reverb/eco).")]
        private bool usaReverb = true;

        [SerializeField, Tooltip("AudioReverbZone da controllare; se vuota, al primo ingresso viene creata in automatico come figlia dell'AudioListener (reverb piena in tutto il trigger). Se la assegni tu, il trigger ne controlla solo accensione e fade: posizionala dove passa la CAMERA (le Reverb Zone si valutano sul listener, non sul kart).")]
        private AudioReverbZone zonaReverb;

        [SerializeField, Tooltip("Preset della zona (Cave, Hallway, ecc.); riapplicato a ogni ingresso, quindi vale come impostazione del trigger.")]
        private AudioReverbPreset presetReverb = AudioReverbPreset.Cave;

        [SerializeField, Tooltip("Solo zona automatica: raggio interno della zona creata sul listener. Col listener a distanza zero dal centro il valore conta poco; serve solo da margine.")]
        private float raggioReverbInterno = 2f;

        [SerializeField, Tooltip("Solo zona automatica: raggio esterno della zona creata sul listener (oltre questo raggio la reverb svanisce).")]
        private float raggioReverbEsterno = 6f;

        [SerializeField, Tooltip("Intensita' di reverb a regime (0 = nessuna, 10000 = massima)."), Range(0f, 10000f)]
        private float intensitaReverb = 9000f;

        [SerializeField, Tooltip("Velocita' del fade della reverb all'ingresso/uscita. Piu' alto = transizione piu' rapida.")]
        private float velocitaFadeReverb = 4f;

        [Header("Audio BG (non 3D)")]
        [SerializeField, Tooltip("Se attivo, all'ingresso avvia un audio di sfondo NON spazializzato (musica di zona).")]
        private bool usaAudioBG = false;

        [SerializeField, Tooltip("AudioSource del BG; se vuota viene creata in automatico su questo GameObject. Viene forzata 2D (spatialBlend 0), in loop e con volume a zero.")]
        private AudioSource sorgenteBG;

        [SerializeField, Tooltip("Clip del BG; se assegnata sovrascrive quella della sorgente (necessaria se la sorgente viene creata in automatico).")]
        private AudioClip clipBG;

        [SerializeField, Tooltip("Volume del BG a regime."), Range(0f, 1f)]
        private float volumeBG = 1f;

        [SerializeField, Tooltip("Velocita' del fade-in/fade-out del BG. Piu' alto = transizione piu' rapida.")]
        private float velocitaFadeBG = 2f;

        // Stato reverb: flag di possesso (il trigger ha acceso la zona),
        // intensita' corrente e obiettivo del fade (0 = spenta, 1 = regime).
        private bool reverbAttiva;
        private float fadeReverb;
        private float fadeReverbTarget;

        // Stato BG: solo il trigger che ha AVVIATO la musica la spegne
        // all'uscita, cosi' trigger sovrapposti non se la spengono a vicenda.
        private bool hoAvviatoBg;
        private float fadeBg;
        private float fadeBgTarget;

        private bool used;

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            // La zona reverb automatica NON si crea qui: in Awake non si sa
            // ancora dove sta l'AudioListener di scena. Viene creata al
            // primo contatto come figlia del listener (AssicuraZonaSul-
            // Listener). Una zona assegnata a mano parte comunque spenta.
            if (zonaReverb != null)
            {
                zonaReverb.reverb = 0;
                zonaReverb.enabled = false;
            }

            // Sorgente BG automatica: 2D, in loop, volume zero.
            if (usaAudioBG && sorgenteBG == null)
            {
                sorgenteBG = gameObject.AddComponent<AudioSource>();
                sorgenteBG.playOnAwake = false;
                sorgenteBG.loop = true;
                sorgenteBG.spatialBlend = 0f;
                sorgenteBG.dopplerLevel = 0f;
            }

            if (sorgenteBG != null)
            {
                sorgenteBG.loop = true;
                sorgenteBG.spatialBlend = 0f;
                sorgenteBG.playOnAwake = false;
                sorgenteBG.volume = 0f;
                if (clipBG != null)
                    sorgenteBG.clip = clipBG;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (used && oneShot)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            if (usaReverb)
            {
                AssicuraZonaSulListener();

                if (zonaReverb != null)
                {
                    zonaReverb.reverbPreset = presetReverb;
                    zonaReverb.enabled = true;
                    fadeReverbTarget = 1f;
                    reverbAttiva = true;
                }
            }

            if (usaAudioBG && sorgenteBG != null && sorgenteBG.clip != null)
            {
                // Se il fade-out era ancora in corso al rientro, la musica
                // e' gia' nostra e basta risalire di volume.
                if (!hoAvviatoBg)
                {
                    hoAvviatoBg = true;
                    if (!sorgenteBG.isPlaying)
                        sorgenteBG.Play();
                }
                fadeBgTarget = 1f;
            }

            if (oneShot)
                used = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (used && oneShot)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            if (reverbAttiva)
                fadeReverbTarget = 0f;

            if (hoAvviatoBg)
                fadeBgTarget = 0f;

            if (oneShot)
                used = true;
        }

        private void Update()
        {
            // Reverb: fade dell'intensita' verso l'obiettivo; a fade-out
            // concluso la zona si spegne del tutto (nessun costo residuo).
            if (reverbAttiva && zonaReverb != null)
            {
                float passo = 1f - Mathf.Exp(-Mathf.Max(velocitaFadeReverb, 0.01f) * Time.deltaTime);
                fadeReverb = Mathf.Lerp(fadeReverb, fadeReverbTarget, passo);
                zonaReverb.reverb = Mathf.RoundToInt(intensitaReverb * fadeReverb);

                if (fadeReverbTarget <= 0f && fadeReverb < 0.001f)
                {
                    zonaReverb.reverb = 0;
                    zonaReverb.enabled = false;
                    reverbAttiva = false;
                }
            }

            // BG: fade del volume verso l'obiettivo; a fade-out concluso la
            // sorgente va in pausa e il trigger rilascia la "proprieta'".
            if (sorgenteBG != null)
            {
                float passo = 1f - Mathf.Exp(-Mathf.Max(velocitaFadeBG, 0.01f) * Time.deltaTime);
                fadeBg = Mathf.Lerp(fadeBg, fadeBgTarget, passo);
                sorgenteBG.volume = volumeBG * fadeBg;

                if (fadeBgTarget <= 0f && fadeBg < 0.001f)
                {
                    sorgenteBG.volume = 0f;
                    if (hoAvviatoBg)
                    {
                        if (sorgenteBG.isPlaying)
                            sorgenteBG.Pause();
                        hoAvviatoBg = false;
                    }
                }
            }
        }

        private void OnDisable()
        {
            // Safety: se il trigger viene spento col kart dentro, ripristina
            // lo stato audio (niente reverb bloccata o musica orfana in pausa
            // a meta' fade).
            if (zonaReverb != null && reverbAttiva)
            {
                zonaReverb.reverb = 0;
                zonaReverb.enabled = false;
            }
            reverbAttiva = false;
            fadeReverb = 0f;
            fadeReverbTarget = 0f;

            if (sorgenteBG != null && hoAvviatoBg)
            {
                if (sorgenteBG.isPlaying)
                    sorgenteBG.Pause();
                hoAvviatoBg = false;
            }
            fadeBg = 0f;
            fadeBgTarget = 0f;
        }

        // Crea la zona reverb come figlia dell'AudioListener (una sola
        // volta), con i raggi fissi configurabili. Le Reverb Zone di Unity
        // si valutano sulla posizione del LISTENER (nel progetto: la camera
        // che segue il kart), non delle sorgenti: col listener a distanza
        // zero dal centro la reverb e' sempre piena mentre il trigger e'
        // attivo, per qualsiasi forma/dimensione del collider. Se il
        // riferimento esiste ma punta a una zona distrutta, viene ricreata.
        private void AssicuraZonaSulListener()
        {
            if (zonaReverb != null)
                return;

            AudioListener listener = FindFirstObjectByType<AudioListener>(FindObjectsInactive.Exclude);
            if (listener == null && Camera.main != null)
                listener = Camera.main.GetComponent<AudioListener>();

            if (listener == null)
            {
                Debug.LogWarning("[TriggerAudio] Nessun AudioListener in scena: la reverb non puo' funzionare.", this);
                return;
            }

            GameObject oggetto = new GameObject("ZonaReverbListener");
            oggetto.transform.SetParent(listener.transform, false);

            zonaReverb = oggetto.AddComponent<AudioReverbZone>();
            zonaReverb.reverbPreset = presetReverb;
            zonaReverb.minDistance = raggioReverbInterno;
            zonaReverb.maxDistance = raggioReverbEsterno;
            zonaReverb.reverb = 0;
            zonaReverb.enabled = false;
        }
    }
}
