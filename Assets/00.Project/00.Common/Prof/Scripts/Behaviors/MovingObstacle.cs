// =============================================================================
// MovingObstacle
// -----------------------------------------------------------------------------
// COSA FA:
//   Fa muovere e/o ruotare un GameObject. Tre modalita': solo Translate,
//   solo Rotate, oppure entrambi (Both). Per la traslazione usa una lista di
//   Transform "waypoint" e si muove tra di essi a velocita' costante.
//
// COME SI USA:
//   1) Crea uno o piu' GameObject vuoti come waypoint, posizionali in scena.
//   2) Aggiungi questo componente all'oggetto da muovere.
//   3) Trascina i waypoint nella lista. Imposta la modalita' e la velocita'.
//
// DA SAPERE:
//   - In modalita' Translate/Both servono almeno 2 waypoint, altrimenti il
//     componente disabilita la traslazione e stampa un Warning.
//   - LoopType: PingPong = avanti/indietro, Loop = ricomincia da capo, Once = si ferma.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeKart.Behaviors
{
    public enum MotionMode { Translate, Rotate, Both }
    public enum LoopType { PingPong, Loop, Once }

    public class MovingObstacle : MonoBehaviour
    {
        #region Inspector

        [SerializeField, Tooltip("Cosa fa l'oggetto: solo traslazione, solo rotazione, o entrambe.")]
        private MotionMode mode = MotionMode.Translate;

        [Header("Translation")]
        [SerializeField, Tooltip("Lista di Transform tra cui muoversi (servono almeno 2).")]
        private List<Transform> waypoints = new List<Transform>();

        // PER MODIFICARE: alza per piattaforme veloci, abbassa per movimenti lenti.
        [SerializeField, Tooltip("Velocita' di traslazione in unita'/secondo.")]
        private float translateSpeed = 3f;

        [SerializeField, Tooltip("Pausa in secondi al termine di ogni segmento.")]
        private float pauseAtEnds = 0.5f;

        [SerializeField, Tooltip("Modalita' di loop: PingPong, Loop o Once.")]
        private LoopType loopType = LoopType.PingPong;

        [Header("Rotation")]
        [SerializeField, Tooltip("Asse di rotazione (di solito Vector3.up = Y).")]
        private Vector3 rotateAxis = Vector3.up;

        // PER MODIFICARE: 60 = giro completo in 6 sec, 360 = giro al secondo.
        [SerializeField, Tooltip("Velocita' di rotazione in gradi/secondo.")]
        private float rotateSpeed = 60f;

        #endregion

        #region Unity callbacks

        private void Start()
        {
            // Validazione: se siamo in Translate/Both ma mancano waypoint,
            // disabilitiamo solo la traslazione, lasciando attiva la rotazione.
            translationEnabled = mode != MotionMode.Rotate;
            if (translationEnabled && waypoints.Count < 2)
            {
                Debug.LogWarning("[MovingObstacle] Servono almeno 2 waypoint per traslazione su " + name + ". Disabilito la traslazione.", this);
                translationEnabled = false;
            }
            // Posiziona l'oggetto al primo waypoint per partire dal posto giusto.
            if (translationEnabled) transform.position = waypoints[0].position;
            currentIndex = 0;
            nextIndex = waypoints.Count > 1 ? 1 : 0;
        }

        private void Update()
        {
            if (mode != MotionMode.Translate) DoRotation();
            if (translationEnabled && !paused) DoTranslation();
        }

        #endregion

        #region Internal

        private bool translationEnabled;
        private int currentIndex;
        private int nextIndex;
        private bool paused;
        private bool forward = true; // Direzione attuale del PingPong.

        private void DoRotation()
        {
            // Rotazione costante attorno all'asse scelto. Non influenza la fisica
            // del kart (e' un movimento del transform, non una forza Rigidbody).
            transform.Rotate(rotateAxis.normalized * rotateSpeed * Time.deltaTime, Space.World);
        }

        private void DoTranslation()
        {
            Vector3 target = waypoints[nextIndex].position;
            transform.position = Vector3.MoveTowards(transform.position, target, translateSpeed * Time.deltaTime);

            // Se siamo arrivati al waypoint successivo, attendiamo (se richiesto)
            // e avanziamo al prossimo secondo loopType.
            if ((transform.position - target).sqrMagnitude < 0.0001f)
            {
                if (pauseAtEnds > 0f) StartCoroutine(PauseRoutine());
                AdvanceWaypoint();
            }
        }

        private IEnumerator PauseRoutine()
        {
            paused = true;
            yield return new WaitForSeconds(pauseAtEnds);
            paused = false;
        }

        private void AdvanceWaypoint()
        {
            currentIndex = nextIndex;

            // Calcoliamo il prossimo indice in base al tipo di loop scelto.
            switch (loopType)
            {
                case LoopType.Loop:
                    nextIndex = (currentIndex + 1) % waypoints.Count;
                    break;
                case LoopType.Once:
                    // Se siamo arrivati alla fine restiamo li' senza ricominciare.
                    if (currentIndex < waypoints.Count - 1) nextIndex = currentIndex + 1;
                    else translationEnabled = false;
                    break;
                case LoopType.PingPong:
                default:
                    if (forward)
                    {
                        if (currentIndex >= waypoints.Count - 1) { forward = false; nextIndex = currentIndex - 1; }
                        else nextIndex = currentIndex + 1;
                    }
                    else
                    {
                        if (currentIndex <= 0) { forward = true; nextIndex = currentIndex + 1; }
                        else nextIndex = currentIndex - 1;
                    }
                    break;
            }
        }

        #endregion
    }
}
