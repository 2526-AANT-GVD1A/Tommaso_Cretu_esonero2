using UnityEngine;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Trigger audio per il kart, stesso schema di CameraPhaseTrigger: quando
    /// il kart (tag Player) attraversa il volume, due blocchi indipendenti
    /// possono attivarsi e tornare allo stato di partenza all'uscita.
    /// - Zona effetto: una AudioReverbZone (reverb/eco, es. galleria o grotta)
    ///   si accende con fade dell'intensita' e si spegne all'uscita.
    /// - Audio BG: una sorgente NON spazializzata (musica di zona) parte in
    ///   fade-in e sfuma in pausa all'uscita.
    /// I componenti mancanti vengono creati in automatico sul trigger, come
    /// le sorgenti di AudioRuoteKart.
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
        [SerializeField, Tooltip("Se attivo, all'ingresso accende una AudioReverbZone con fade (l'audio del kart dentro la zona suona con reverb).")]
        private bool usaReverb = true;

        [SerializeField, Tooltip("AudioReverbZone da controllare; se vuota viene creata in automatico su questo GameObject (raggi adattati al collider).")]
        private AudioReverbZone zonaReverb;

        [SerializeField, Tooltip("Preset della zona (Cave, Hallway, ecc.); riapplicato a ogni ingresso, quindi vale come impostazione del trigger.")]
        private AudioReverbPreset presetReverb = AudioReverbPreset.Cave;

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
            // Zona reverb automatica: creata sul trigger con il preset scelto
            // e i raggi adattati al collider, cosi' copre tutto il volume del
            // trigger anche se questo e' un box lungo (es. una galleria).
            if (usaReverb && zonaReverb == null)
            {
                zonaReverb = gameObject.AddComponent<AudioReverbZone>();
                zonaReverb.reverbPreset = presetReverb;

                float raggio = CalcolaRaggioCollider();
                zonaReverb.maxDistance = raggio;
                zonaReverb.minDistance = raggio * 0.9f;
            }

            if (zonaReverb != null)
            {
                // Parte spenta: il fade la accende solo al contatto col kart.
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

            if (usaReverb && zonaReverb != null)
            {
                zonaReverb.reverbPreset = presetReverb;
                zonaReverb.enabled = true;
                fadeReverbTarget = 1f;
                reverbAttiva = true;
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

        // Raggio che copre l'intero collider del trigger: la meta' diagonale
        // dei bounds mondiali. Per uno sphere/capsule e' il raggio reale, per
        // un box ruotato lo supera leggermente (AABB): meglio coprire troppo.
        private float CalcolaRaggioCollider()
        {
            Collider c = GetComponent<Collider>();
            if (c == null)
                return 20f;

            Vector3 estesi = c.bounds.extents;
            float raggio = estesi.magnitude;
            return raggio > 0.01f ? raggio : 20f;
        }
    }
}
