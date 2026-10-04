using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// An item lying in the world. Picking it up removes it from the scene and
    /// hands the definition to the inventory; dropping spawns one of these again.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDefinition definition;

        [Tooltip("Impact speed below which landing is silent, so settling does not chatter.")]
        [SerializeField, Min(0f)] float quietImpactSpeed = 2.2f;

        Rigidbody body;
        float noiseCooldownUntil;

        public ItemDefinition Definition => definition;

        public Transform Transform => transform;

        public string Prompt => definition != null ? $"Take {definition.DisplayName}" : "Take";

        void Awake()
        {
            body = GetComponent<Rigidbody>();

            if (definition == null)
                Debug.LogError($"[{nameof(PickupItem)}] '{name}' has no item definition.", this);

            gameObject.layer = GameLayers.Pickup;
        }

        public bool CanInteract(GameObject interactor) => definition != null;

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponentInChildren<Player.PlayerInventory>();
            if (inventory == null) return;

            // One hand: whatever was already held is put down right here, in the
            // spot this item is taken from, so the swap never flings anything.
            if (inventory.TryTake(definition, transform.position))
                Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (definition == null || body == null) return;

            // Only a real landing counts. Without this, an item resting on an
            // uneven floor emits a stream of tiny noises and draws Granny to it.
            if (collision.relativeVelocity.magnitude < quietImpactSpeed) return;
            if (Time.time < noiseCooldownUntil) return;

            noiseCooldownUntil = Time.time + 0.35f;

            // Paper, cloth and the like land silently however hard they are thrown.
            if (definition.ImpactLoudness <= 0f) return;

            NoiseBus.Emit(transform.position, NoiseKind.ItemImpact, gameObject);
        }

        /// <summary>
        /// Spawns the world form of an item. Used by dropping and throwing so both
        /// paths produce an identical object.
        /// </summary>
        public static PickupItem Spawn(ItemDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.WorldPrefab == null) return null;

            var instance = Instantiate(definition.WorldPrefab, position, rotation);
            var pickup = instance.GetComponent<PickupItem>();

            if (pickup == null)
                Debug.LogError(
                    $"[{nameof(PickupItem)}] World prefab for '{definition.Id}' has no {nameof(PickupItem)}.",
                    instance);

            return pickup;
        }
    }
}
