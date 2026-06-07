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

        [SerializeField, Tooltip("Se true, distrugge l'oggetto quando viene raccolto.")]
        private bool destroyOnPickup = true;

        private void Awake()
        {
            Collider c = GetComponent<Collider>();
            if (c != null && !c.isTrigger)
                c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            KartCollectedStack stack = other.GetComponentInParent<KartCollectedStack>();
            if (stack == null)
            {
                Debug.LogWarning("[Pickup] Il player non ha KartCollectedStack.", other);
                return;
            }

            stack.AddCollectedItem(visualType);

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
