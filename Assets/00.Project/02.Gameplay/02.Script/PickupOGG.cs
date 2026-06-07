using System.Collections;
using UnityEngine;

namespace ArcadeKart.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField, Tooltip("Tipo logico dell'oggetto raccolto. Es: coin, gem, key.")]
        private string visualType = "coin";

        [SerializeField, Tooltip("Tag richiesto per poter raccogliere l'oggetto.")]
        private string requiredTag = "Player";

        [Header("Fly To Stack")]
        [SerializeField, Tooltip("Durata del volo verso lo stack root.")]
        private float flyDuration = 0.35f;

        [SerializeField, Tooltip("Altezza massima della parabola.")]
        private float arcHeight = 1.25f;

        [SerializeField, Tooltip("Rotazione visiva durante il volo.")]
        private Vector3 spinDegreesPerSecond = new Vector3(0f, 360f, 0f);

        [SerializeField, Tooltip("Se true, durante il volo l'oggetto si riduce leggermente.")]
        private bool shrinkOnFly = true;

        [SerializeField, Tooltip("Scala finale relativa durante il volo.")]
        private float endScaleMultiplier = 0.75f;

        private bool collected;
        private Collider cachedCollider;
        private Rigidbody cachedRb;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            cachedRb = GetComponent<Rigidbody>();

            if (cachedCollider != null && !cachedCollider.isTrigger)
                cachedCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            KartCollectedStack stack = other.GetComponentInParent<KartCollectedStack>();
            if (stack == null)
            {
                Debug.LogWarning("[Pickup] Il player non ha KartCollectedStack.", other);
                return;
            }

            if (stack.StackRoot == null)
            {
                Debug.LogWarning("[Pickup] Il kart non ha StackRoot assegnato.", stack);
                return;
            }

            StartCoroutine(FlyToStackRoutine(stack));
        }

        private IEnumerator FlyToStackRoutine(KartCollectedStack stack)
        {
            collected = true;

            if (cachedCollider != null)
                cachedCollider.enabled = false;

            if (cachedRb != null)
            {
                cachedRb.linearVelocity = Vector3.zero;
                cachedRb.angularVelocity = Vector3.zero;
                cachedRb.isKinematic = true;
                cachedRb.detectCollisions = false;
            }

            Transform target = stack.StackRoot;
            Transform tr = transform;

            Vector3 startPos = tr.position;
            Quaternion startRot = tr.rotation;
            Vector3 startScale = tr.localScale;

            float t = 0f;
            float duration = Mathf.Max(0.01f, flyDuration);

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float eased = Mathf.Clamp01(t);

                Vector3 endPos = target.position;
                Vector3 pos = Vector3.Lerp(startPos, endPos, eased);

                float arc = 4f * arcHeight * eased * (1f - eased);
                pos.y += arc;

                tr.position = pos;
                tr.rotation = startRot * Quaternion.Euler(spinDegreesPerSecond * (duration * eased));

                if (shrinkOnFly)
                {
                    float scaleMul = Mathf.Lerp(1f, endScaleMultiplier, eased);
                    tr.localScale = startScale * scaleMul;
                }

                yield return null;
            }

            stack.AddCollectedItem(visualType);
            Destroy(gameObject);
        }
    }
}
