using UnityEngine;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Muove l'oggetto a cui e' attaccato in loop tra un punto A e un punto B.
    /// Tutto e' configurabile dall'Inspector: punti (con Transform di riferimento opzionali),
    /// spazio locale o mondiale, durata, curva di interpolazione, pause alle estremita',
    /// tipo di loop (ping-pong o ripartenza da A) e ritardo iniziale.
    /// In editor disegna i punti con i Gizmos ma resta fermo: si muove solo in Play.
    /// </summary>
    public class MuoviPuntoAPunto : MonoBehaviour
    {
        public enum TipoLoop
        {
            PingPong,   // A -> B -> A -> B ... (va e viene)
            RipartiDaA  // A -> B, poi salta di nuovo ad A e riparte
        }

        [Header("Punti del percorso")]
        [Tooltip("Punto A: se collegato un Transform viene seguita la sua posizione (anche in Play).")]
        [SerializeField] private Transform riferimentoA;
        [Tooltip("Punto B: se collegato un Transform viene seguita la sua posizione (anche in Play).")]
        [SerializeField] private Transform riferimentoB;
        [Tooltip("Punto A scritto a mano; usato solo se 'riferimentoA' e' vuoto.")]
        [SerializeField] private Vector3 puntoA = Vector3.zero;
        [Tooltip("Punto B scritto a mano; usato solo se 'riferimentoB' e' vuoto.")]
        [SerializeField] private Vector3 puntoB = new Vector3(0f, 0f, 5f);
        [Tooltip("Attivo: i punti scritti a mano sono locali rispetto al genitore (si muove con lui). Disattivo: sono coordinate del mondo.")]
        [SerializeField] private bool usaSpazioLocale = true;

        [Header("Movimento")]
        [Tooltip("Quanto dura una singola traversata (A verso B), in secondi.")]
        [Min(0.01f)]
        [SerializeField] private float durataTraversata = 2f;
        [Tooltip("Curva di interpolazione della traversata: 0 = punto A, 1 = punto B. Lasciare quella di default per un movimento dolce (accelera e frena). Una retta da 0 a 1 da' velocita' costante.")]
        [SerializeField] private AnimationCurve curvaMovimento = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Tipo di loop: PingPong (va e viene) o RipartiDaA (arrivato a B salta di nuovo ad A).")]
        [SerializeField] private TipoLoop tipoLoop = TipoLoop.PingPong;
        [Tooltip("Come tornare da B ad A: attivo = ritorno istantaneo (teletrasporto), disattivo = ritorno graduale con la stessa durata dell'andata. Nel loop 'RipartiDaA' il ritorno e' sempre istantaneo.")]
        [SerializeField] private bool ritornoIstantaneo = false;
        [Tooltip("Pausa sul punto B, in secondi, prima di ripartire.")]
        [Min(0f)]
        [SerializeField] private float pausaSuB = 0f;
        [Tooltip("Pausa sul punto A, in secondi (solo con ritorno graduale).")]
        [Min(0f)]
        [SerializeField] private float pausaSuA = 0f;
        [Tooltip("Attendo questi secondi prima della prima partenza.")]
        [Min(0f)]
        [SerializeField] private float ritardoIniziale = 0f;
        [Tooltip("Se disattivo l'oggetto resta al punto A (utile per attivarlo da altri script con enabled).")]
        [SerializeField] private bool parteSubito = true;

        // Timer del ciclo corrente, usato per calcolare la posizione lungo il percorso.
        private float tempoCiclo;
        private bool inRitardo;

        private void OnEnable()
        {
            // Alla (ri)attivazione si riparte dall'inizio del ciclo.
            tempoCiclo = 0f;
            inRitardo = ritardoIniziale > 0f;
            transform.position = PuntoMondo(riferimentoA, puntoA);
        }

        private void Update()
        {
            if (inRitardo)
            {
                tempoCiclo += Time.deltaTime;
                if (tempoCiclo < ritardoIniziale) return;
                tempoCiclo = 0f;
                inRitardo = false;
            }

            if (!parteSubito) return;

            tempoCiclo += Time.deltaTime;
            transform.position = CalcolaPosizione(tempoCiclo);
        }

        // Trasforma un punto in coordinate del mondo, in base allo spazio scelto e all'eventuale riferimento.
        private Vector3 PuntoMondo(Transform riferimento, Vector3 punto)
        {
            if (riferimento != null) return riferimento.position;

            if (usaSpazioLocale && transform.parent != null)
                return transform.parent.TransformPoint(punto);
            return punto;
        }

        // Data la durata delle varie fasi, calcola la posizione al tempo indicato del ciclo.
        private Vector3 CalcolaPosizione(float tempo)
        {
            Vector3 puntoMondoA = PuntoMondo(riferimentoA, puntoA);
            Vector3 puntoMondoB = PuntoMondo(riferimentoB, puntoB);
            float durata = Mathf.Max(0.01f, durataTraversata);

            if (tipoLoop == TipoLoop.RipartiDaA || ritornoIstantaneo)
            {
                // Ciclo: traversata A->B + pausa su B, poi ritorno istantaneo (salto di nuovo ad A).
                float ciclo = durata + pausaSuB;
                float t = Mathf.Repeat(tempo, ciclo);

                if (t < durata)
                    return Vector3.Lerp(puntoMondoA, puntoMondoB, curvaMovimento.Evaluate(t / durata));
                return puntoMondoB;
            }

            // PingPong con ritorno graduale: traversata A->B, pausa su B, traversata B->A, pausa su A.
            float cicloPingPong = durata + pausaSuB + durata + pausaSuA;
            float tPingPong = Mathf.Repeat(tempo, cicloPingPong);

            if (tPingPong < durata)
                return Vector3.Lerp(puntoMondoA, puntoMondoB, curvaMovimento.Evaluate(tPingPong / durata));
            tPingPong -= durata;

            if (tPingPong < pausaSuB)
                return puntoMondoB;
            tPingPong -= pausaSuB;

            if (tPingPong < durata)
                return Vector3.Lerp(puntoMondoB, puntoMondoA, curvaMovimento.Evaluate(tPingPong / durata));
            return puntoMondoA;
        }

#if UNITY_EDITOR
        // In editor disegna il percorso: sfera verde su A, rossa su B e linea tra i due.
        private void OnDrawGizmos()
        {
            Vector3 puntoMondoA = PuntoMondo(riferimentoA, puntoA);
            Vector3 puntoMondoB = PuntoMondo(riferimentoB, puntoB);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(puntoMondoA, 0.15f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(puntoMondoB, 0.15f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(puntoMondoA, puntoMondoB);
        }
#endif
    }
}
