// =============================================================================
// FollowCamera
// -----------------------------------------------------------------------------
// COSA FA:
//   Camera "third-person" semplice che segue un Transform (di solito il kart).
//   Niente Cinemachine: una sola classe, smorzamenti morbidi, look-at fluido.
//
// COME SI USA:
//   1) Aggiungi questo componente alla Main Camera (o a una qualsiasi camera).
//   2) Trascina nello slot "Target" il kart (o il suo Transform di follow).
//   3) Tara "Offset" se vuoi cambiare angolazione (default: dietro e in alto).
//
// DA SAPERE:
//   - L'aggiornamento avviene in LateUpdate per evitare scatti rispetto al
//     movimento del kart (che si aggiorna in Update/FixedUpdate).
//   - Se "Target" e' nullo, stampa un Warning UNA SOLA VOLTA e si disattiva.
// =============================================================================

using UnityEngine;

namespace ArcadeKart.Utility
{
    public class FollowCamera : MonoBehaviour
    {
        #region Inspector

        [SerializeField, Tooltip("Transform da seguire. Di solito il kart.")]
        private Transform target;

        // PER MODIFICARE: cambia per spostare la camera in altre posizioni.
        // X = laterale, Y = altezza sopra il target, Z = distanza (negativa = dietro).
        [SerializeField, Tooltip("Offset locale rispetto al target (X laterale, Y altezza, Z distanza).")]
        private Vector3 offset = new Vector3(0f, 4f, -7f);

        // PER MODIFICARE: alza per camera piu' "incollata" al kart, abbassa per
        // un seguito piu' rilassato. Valori tipici: 3 (rilassato) - 10 (reattivo).
        [SerializeField, Tooltip("Quanto velocemente la posizione della camera raggiunge il target.")]
        private float followDamping = 5f;

        // PER MODIFICARE: alza per rotazione piu' reattiva, abbassa per piu' fluida.
        [SerializeField, Tooltip("Quanto velocemente la camera ruota verso il target.")]
        private float lookDamping = 8f;

        [SerializeField, Tooltip("Punto di osservazione locale rispetto al target (utile per guardare leggermente in alto).")]
        private Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);

        [SerializeField, Tooltip("Se true la camera mantiene un'altezza fissa nel mondo (utile per camere 'piatte').")]
        private bool useFixedY = false;

        [SerializeField, Tooltip("Altezza fissa nel mondo (usata solo se 'Use Fixed Y' e' true).")]
        private float fixedY = 5f;

        [SerializeField, Tooltip("Se true, l'offset segue la rotazione del target (camera 'dietro' al kart). Se false, offset in coordinate mondo.")]
        private bool followTargetRotation = true;

        #endregion

        #region Public API

        /// <summary>Re-targets the camera at runtime. Useful for cinematic switches.</summary>
        /// <param name="newTarget">New transform to follow. May be null to disable following.</param>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            warningLogged = false;
        }

        #endregion

        #region Unity callbacks

        private void LateUpdate()
        {
            // Senza target non possiamo seguire nulla. Logghiamo una sola volta
            // per evitare spam in console e ci spegniamo.
            if (target == null)
            {
                if (!warningLogged)
                {
                    Debug.LogWarning("[FollowCamera] Target non assegnato su " + name + ". Disattivo il componente.", this);
                    warningLogged = true;
                }
                enabled = false;
                return;
            }

            // Calcoliamo la posizione desiderata. Se vogliamo che l'offset segua
            // la rotazione del target (camera "dietro al kart"), lo trasformiamo
            // in coordinate mondo via TransformPoint. Altrimenti usiamo l'offset
            // come un offset assoluto.
            Vector3 desiredPos = followTargetRotation
                ? target.TransformPoint(offset)
                : target.position + offset;

            // Opzionale: blocchiamo l'altezza per camere "platform" o "top-down soft".
            if (useFixedY) desiredPos.y = fixedY;

            // Smoothing della posizione: piu' alto e' followDamping, piu' la
            // camera "incolla" il target. La formula 1 - exp(-d * dt) e' uno
            // smorzamento esponenziale frame-rate independent.
            float posT = 1f - Mathf.Exp(-followDamping * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, posT);

            // Smoothing della rotazione verso il target.
            Vector3 lookAtPoint = target.position + lookAtOffset;
            Vector3 dir = lookAtPoint - transform.position;
            // Se siamo praticamente coincidenti col target (raro) non ruotiamo,
            // altrimenti LookRotation darebbe NaN.
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            float rotT = 1f - Mathf.Exp(-lookDamping * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotT);
        }

        #endregion

        #region Internal

        // Evita di stampare il warning piu' di una volta se il target e' nullo.
        private bool warningLogged;

        #endregion
    }
}
