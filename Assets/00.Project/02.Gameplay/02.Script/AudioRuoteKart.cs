using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Suono delle ruote del kart in loop mentre si muove.
    /// Due set di parametri fusi in continuo con il peso fase del KartController:
    /// CAMMINATA (boost rilasciato) = pitch e volume ridotti;
    /// CORSA (boost mouse sx tenuto) = pitch e volume pieni.
    /// A kart fermo il suono sfuma e va in pausa.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioRuoteKart : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("KartController da cui leggere velocita' e fase (camminata/corsa); se vuoto viene cercato su questo GameObject e nei padri.")]
        [SerializeField] private KartController kart;

        [Tooltip("AudioSource del suono ruote; se vuoto usa quello su questo GameObject (aggiunto in automatico se manca).")]
        [SerializeField] private AudioSource sorgenteAudio;

        [Tooltip("Clip del suono ruote; se vuota lascia quella gia' assegnata all'AudioSource.")]
        [SerializeField] private AudioClip suonoRuote;

        [Header("Camminata (boost rilasciato)")]
        [Tooltip("Volume del suono ruote in fase camminata.")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeCamminata = 0.45f;

        [Tooltip("Pitch in fase camminata: sotto 1 il suono viene riprodotto piu' lentamente (ruote piu' lente).")]
        [SerializeField] private float pitchCamminata = 0.85f;

        [Header("Corsa (boost mouse sx tenuto)")]
        [Tooltip("Volume del suono ruote in fase corsa.")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeCorsa = 1f;

        [Tooltip("Pitch in fase corsa: 1 = velocita' di riproduzione normale.")]
        [SerializeField] private float pitchCorsa = 1f;

        [Header("Transizioni")]
        [Tooltip("Velocita' planare minima del kart per tenere il suono attivo; sotto questa soglia sfuma e va in pausa.")]
        [SerializeField] private float sogliaMovimento = 0.3f;

        [Tooltip("Velocita' di smorzamento del blend volume/pitch e del fade in pausa. Piu' alto = transizioni piu' rapide.")]
        [SerializeField] private float velocitaTransizione = 8f;

        [Header("Modulazione velocita'")]
        [Tooltip("Se attivo, pitch e volume si modulano anche sul rapporto tra velocita' reale del kart e soffitto corrente: reagiscono alle decelerazioni (es. inversioni a 180 gradi) e alle ripartenze, oltre alla fase camminata/corsa.")]
        [SerializeField] private bool modulaConVelocita = true;

        [Tooltip("Fattore pitch a velocita' quasi nulla rispetto al soffitto: 0.6 = a kart quasi fermo il suono gira al 60% della velocita' di fase corrente.")]
        [Range(0f, 1f)]
        [SerializeField] private float pitchAVelocitaZero = 0.6f;

        [Tooltip("Fattore volume a velocita' quasi nulla rispetto al soffitto.")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeAVelocitaZero = 0.5f;

        // Volume corrente smorzato: governa il fade a zero quando il kart e'
        // fermo (niente tagli netti = niente click in coda al suono).
        private float volumeAttuale;

        private void Awake()
        {
            if (kart == null)
                kart = GetComponentInParent<KartController>();

            if (sorgenteAudio == null)
                sorgenteAudio = GetComponent<AudioSource>();

            if (sorgenteAudio == null)
                sorgenteAudio = gameObject.AddComponent<AudioSource>();

            if (suonoRuote != null)
                sorgenteAudio.clip = suonoRuote;

            sorgenteAudio.loop = true;
            sorgenteAudio.playOnAwake = false;

            volumeAttuale = 0f;
            sorgenteAudio.volume = 0f;
        }

        private void Update()
        {
            if (kart == null || sorgenteAudio == null || sorgenteAudio.clip == null)
                return;

            // Peso fase (0 = camminata, 1 = corsa) gia' smorzato dal
            // controller: e' lo stesso con cui si fondono grip e sterzo,
            // cosi' l'audio segue la stessa fase percepita dal resto del kart.
            float pesoCorsa = kart.PesoCorsaFase;

            float volumeTarget = Mathf.Lerp(volumeCamminata, volumeCorsa, pesoCorsa);
            float pitchTarget = Mathf.Lerp(pitchCamminata, pitchCorsa, pesoCorsa);

            float velocitaAssoluta = Mathf.Abs(kart.CurrentSpeed);

            // Modulazione sulla velocita' REALE: il rapporto con il soffitto
            // corrente cala quando il kart decelera (es. inversioni a 180
            // gradi) e risale quando riprende velocita', cosi' il suono segue
            // il ritmo del kart invece di restare bloccato sui valori di fase.
            if (modulaConVelocita)
            {
                float rapporto = Mathf.Clamp01(
                    velocitaAssoluta
                    / Mathf.Max(kart.SoffittoVelocitaAttuale, 0.01f));
                pitchTarget *= Mathf.Lerp(pitchAVelocitaZero, 1f, rapporto);
                volumeTarget *= Mathf.Lerp(volumeAVelocitaZero, 1f, rapporto);
            }

            bool inMovimento = velocitaAssoluta >= sogliaMovimento;
            if (!inMovimento)
                volumeTarget = 0f;

            // Smorzamento esponenziale (stesso schema del soffitto nel controller).
            float passo = 1f - Mathf.Exp(-velocitaTransizione * Time.deltaTime);
            volumeAttuale = Mathf.Lerp(volumeAttuale, volumeTarget, passo);
            sorgenteAudio.pitch = Mathf.Lerp(sorgenteAudio.pitch, pitchTarget, passo);
            sorgenteAudio.volume = volumeAttuale;

            if (inMovimento)
            {
                if (!sorgenteAudio.isPlaying)
                    sorgenteAudio.Play();
            }
            else if (sorgenteAudio.isPlaying && volumeAttuale < 0.001f)
            {
                sorgenteAudio.Pause();
            }
        }
    }
}
