using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Anima gli arti del personaggio dentro il kart.
    /// Ogni arto e' una linea (LineRenderer) a 3 punti:
    /// partenza (spalla/anca) -> punto centrale (gomito/ginocchio, calcolato via script) -> arrivo (mano/piede).
    /// Le gambe simulano una camminata/corsa in base alla velocita' del kart:
    /// il piede percorre un arco (scivola di lato e si alza nella meta' "in aria" del ciclo) e il ginocchio lo segue.
    /// A kart fermo tutto torna fermo sulle ancore; piu' il kart corre, piu' il passo e' rapido ed ampio.
    /// </summary>
    [DefaultExecutionOrder(200)] // dopo KartController e SpriteBillboard: le ancore sono gia' aggiornate nel frame
    public class ArtiPersonaggio : MonoBehaviour
    {
        [System.Serializable]
        public class Arto
        {
            [Tooltip("Ancora di partenza (spalla o anca), di solito figlia di Base2D per seguire lo sprite.")]
            public Transform partenza;

            [Tooltip("Ancora di arrivo (mano o piede), di solito figlia del kart per restare fissa sul volante/pedali.")]
            public Transform arrivo;

            [Tooltip("LineRenderer dell'arto; se vuoto viene creato automaticamente come figlio.")]
            public LineRenderer linea;

            [Tooltip("Curvatura del punto centrale (gomito/ginocchio), in metri: positiva piega da un lato, negativa dall'altro.")]
            public float curvatura = 0.05f;
        }

        [System.Serializable]
        public class Gamba : Arto
        {
            [Tooltip("Sfasamento della pedalata (0-1 giri): 0.5 rende le gambe in controfase.")]
            [Range(0f, 1f)] public float sfasamento = 0f;
        }

        [Header("Riferimenti")]
        [Tooltip("KartController da cui leggere la velocita'; se vuoto viene cercato nei padri.")]
        [SerializeField] private KartController kart;

        [Header("Aspetto linee")]
        [Tooltip("Materiale nero delle linee (LineaNera.mat).")]
        [SerializeField] private Material materialeLinea;
        [Tooltip("Spessore delle linee delle braccia.")]
        [SerializeField] private float spessoreBraccia = 0.02f;
        [Tooltip("Spessore delle linee delle gambe.")]
        [SerializeField] private float spessoreGambe = 0.022f;
        [Tooltip("Ordine di sorting: con 10 le linee disegnano sopra lo sprite del personaggio (che e' a 0).")]
        [SerializeField] private int ordineSorting = 10;

        [Header("Braccia")]
        [SerializeField] private Arto braccioSinistro;
        [SerializeField] private Arto braccioDestro;

        [Header("Gambe")]
        [SerializeField] private Gamba gambaSinistra;
        [SerializeField] private Gamba gambaDestra;

        [Header("Pedalata")]
        [Tooltip("Cicli di pedalata al secondo quando il kart va alla velocita' massima.")]
        [SerializeField] private float frequenzaMassima = 3.5f;
        [Tooltip("Escursione massima del ginocchio durante la pedalata, in metri.")]
        [SerializeField] private float ampiezzaMassima = 0.045f;
        [Tooltip("Escursione laterale del piede durante il passo, in metri.")]
        [SerializeField] private float ampiezzaPasso = 0.03f;
        [Tooltip("Quanto si alza il piede nella fase in aria del passo, in metri.")]
        [SerializeField] private float ampiezzaAlzo = 0.035f;

        // Fase corrente della pedalata, espressa in giri (0-1).
        private float fase;

        private void Awake()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();

            Prepara(braccioSinistro, spessoreBraccia, "Linea_BraccioSX");
            Prepara(braccioDestro, spessoreBraccia, "Linea_BraccioDX");
            Prepara(gambaSinistra, spessoreGambe, "Linea_GambaSX");
            Prepara(gambaDestra, spessoreGambe, "Linea_GambaDX");
        }

        private void LateUpdate()
        {
            // Velocita' normalizzata 0-1 rispetto alla massima del kart.
            float velocita = kart != null ? Mathf.Abs(kart.CurrentSpeed) : 0f;
            float massima = (kart != null && kart.MaxSpeed > 0.01f) ? kart.MaxSpeed : 1f;
            float velocita01 = Mathf.Clamp01(velocita / massima);

            // La fase avanza solo in movimento: kart fermo = gambe ferme.
            fase = Mathf.Repeat(fase + frequenzaMassima * velocita01 * Time.deltaTime, 1f);

            // Verso della camera rispetto al personaggio: serve per rendere le pieghe visibili da ogni angolo.
            Camera cam = Camera.main;
            Vector3 versoCamera = cam != null
                ? (cam.transform.position - transform.position).normalized
                : Vector3.forward;

            // Base visibile a schermo, calcolata una volta per frame:
            // suSchermo = verticale proiettata sul piano perpendicolare alla camera,
            // destraSchermo = orizzontale a schermo (perpendicolare a entrambe).
            Vector3 suSchermo = ProiettaSuSchermo(Vector3.up, versoCamera);
            suSchermo = suSchermo.sqrMagnitude > 0.001f
                ? suSchermo.normalized
                : ProiettaSuSchermo(Vector3.forward, versoCamera).normalized;
            Vector3 destraSchermo = Vector3.Cross(suSchermo, versoCamera).normalized;

            DisegnaBraccio(braccioSinistro, versoCamera);
            DisegnaBraccio(braccioDestro, versoCamera);
            float sfasamentoSX = gambaSinistra != null ? gambaSinistra.sfasamento : 0f;
            float sfasamentoDX = gambaDestra != null ? gambaDestra.sfasamento : 0.5f;
            DisegnaGamba(gambaSinistra, versoCamera, suSchermo, destraSchermo, velocita01, fase + sfasamentoSX);
            DisegnaGamba(gambaDestra, versoCamera, suSchermo, destraSchermo, velocita01, fase + sfasamentoDX);
        }

        /// <summary>Braccio: spalla -> gomito (punto medio piegato) -> mano.</summary>
        private void DisegnaBraccio(Arto arto, Vector3 versoCamera)
        {
            if (!Valido(arto)) return;

            Vector3 partenza = arto.partenza.position;
            Vector3 arrivo = arto.arrivo.position;
            Vector3 suSchermo = DirezioneVisibile(arrivo - partenza, versoCamera);
            Vector3 gomito = Vector3.Lerp(partenza, arrivo, 0.5f) + suSchermo * arto.curvatura;

            arto.linea.SetPosition(0, partenza);
            arto.linea.SetPosition(1, gomito);
            arto.linea.SetPosition(2, arrivo);
        }

        /// <summary>
        /// Gamba: anca -> ginocchio -> piede. Il piede percorre un arco a mezz'aria:
        /// scivola di lato mentre e' sollevato (meta' "in aria" del ciclo) e torna appoggiato nell'altra meta'.
        /// Il ginocchio segue il piede animato e si alza in sincrono con lui.
        /// </summary>
        private void DisegnaGamba(Gamba gamba, Vector3 versoCamera, Vector3 suSchermo, Vector3 destraSchermo, float velocita01, float faseGamba)
        {
            if (!Valido(gamba)) return;

            Vector3 anca = gamba.partenza.position;
            Vector3 piedeBase = gamba.arrivo.position;

            // Arco del passo: lo scorrimento laterale e' continuo, l'alzo esiste solo nella meta' in aria (arco semicircolare).
            float angolo = faseGamba * Mathf.PI * 2f;
            Vector3 piedeAnimato = piedeBase
                + destraSchermo * (Mathf.Cos(angolo) * ampiezzaPasso * velocita01)
                + suSchermo * (Mathf.Max(0f, Mathf.Sin(angolo)) * ampiezzaAlzo * velocita01);

            // Il ginocchio e' il punto medio tra anca e piede animato, con alzo sincronizzato al piede.
            Vector3 suGinocchio = DirezioneVisibile(piedeAnimato - anca, versoCamera);
            float alzoGinocchio = Mathf.Max(0f, Mathf.Sin(angolo)) * ampiezzaMassima * velocita01;
            Vector3 ginocchio = Vector3.Lerp(anca, piedeAnimato, 0.5f)
                + suGinocchio * (gamba.curvatura + alzoGinocchio);

            gamba.linea.SetPosition(0, anca);
            gamba.linea.SetPosition(1, ginocchio);
            gamba.linea.SetPosition(2, piedeAnimato);
        }

        /// <summary>Proietta un vettore sul piano perpendicolare alla direzione di vista.</summary>
        private static Vector3 ProiettaSuSchermo(Vector3 vettore, Vector3 versoCamera)
        {
            return vettore - versoCamera * Vector3.Dot(vettore, versoCamera);
        }

        /// <summary>
        /// Direzione perpendicolare all'arto ma visibile a schermo:
        /// la proietta sul piano perpendicolare alla camera, cosi' la piega non "svanisce" guardando l'arto di fronte.
        /// </summary>
        private static Vector3 DirezioneVisibile(Vector3 direzioneArto, Vector3 versoCamera)
        {
            Vector3 dir = direzioneArto.sqrMagnitude > 0.000001f ? direzioneArto.normalized : Vector3.forward;

            // Parte dal "su" del mondo proiettato sul piano dello schermo.
            Vector3 suSchermo = Vector3.up - versoCamera * Vector3.Dot(Vector3.up, versoCamera);
            Vector3 risultato = suSchermo - dir * Vector3.Dot(suSchermo, dir);

            // Caso limite: arto quasi parallelo alla verticale, si piega di lato rispetto alla camera.
            if (risultato.sqrMagnitude < 0.0001f) risultato = Vector3.Cross(versoCamera, dir);

            return risultato.sqrMagnitude > 0.0001f ? risultato.normalized : Vector3.up;
        }

        /// <summary>L'arto e' utilizzabile solo se ha ancora, arrivo e linea assegnati.</summary>
        private static bool Valido(Arto arto)
        {
            return arto != null && arto.partenza != null && arto.arrivo != null && arto.linea != null;
        }

        /// <summary>
        /// Crea (o riusa) il LineRenderer di un arto e ne fissa le impostazioni grafiche.
        /// Nome deterministico: se esiste gia' un figlio con lo stesso nome viene riusato.
        /// </summary>
        private void Prepara(Arto arto, float spessore, string nomeLinea)
        {
            if (arto == null) return;

            // Riusa la linea gia' assegnata, altrimenti cerca il figlio con quel nome, altrimenti lo crea.
            if (arto.linea == null)
            {
                Transform trovato = transform.Find(nomeLinea);
                arto.linea = trovato != null ? trovato.GetComponent<LineRenderer>() : null;
            }
            if (arto.linea == null)
            {
                var go = new GameObject(nomeLinea);
                go.transform.SetParent(transform, false);
                arto.linea = go.AddComponent<LineRenderer>();
            }

            var linea = arto.linea;
            linea.positionCount = 3;
            linea.useWorldSpace = true;
            linea.alignment = LineAlignment.View; // la linea si volge sempre verso la camera
            linea.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            linea.widthMultiplier = spessore;
            linea.numCapVertices = 2; // punte arrotondate
            linea.numCornerVertices = 0;
            linea.startColor = Color.black;
            linea.endColor = Color.black;
            if (materialeLinea != null) linea.sharedMaterial = materialeLinea;
            linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linea.receiveShadows = false;
            linea.sortingOrder = ordineSorting;

            // Aggiorna subito i punti cosi' la linea e' visibile anche fuori dal Play Mode.
            if (arto.partenza != null && arto.arrivo != null)
            {
                linea.SetPosition(0, arto.partenza.position);
                linea.SetPosition(1, arto.partenza.position);
                linea.SetPosition(2, arto.arrivo.position);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rigenera linee in editor")]
        private void RigeneraLineeInEditor()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            Prepara(braccioSinistro, spessoreBraccia, "Linea_BraccioSX");
            Prepara(braccioDestro, spessoreBraccia, "Linea_BraccioDX");
            Prepara(gambaSinistra, spessoreGambe, "Linea_GambaSX");
            Prepara(gambaDestra, spessoreGambe, "Linea_GambaDX");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
