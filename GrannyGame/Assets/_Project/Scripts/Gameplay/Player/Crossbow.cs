using Granny.Core;
using Granny.Gameplay.AI;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// The one thing in the house that can stop her, for a while.
    ///
    /// It fires on the throw button, because a crossbow in hand is what you
    /// would throw if you had nothing better — and because the player never has
    /// to learn a key that only matters for the ten minutes they are holding it.
    ///
    /// A hit is loud, which is the trade: the shot that buys a minute of quiet
    /// also tells the whole house exactly where that minute is being spent.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class Crossbow : MonoBehaviour
    {
        [Header("Shooting")]
        [SerializeField, Min(1f)] float range = 30f;

        [Tooltip("Spawned where the dart lands, so three darts can last all night.")]
        [SerializeField] ItemDefinition dartItem;

        [Header("Wiring")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform muzzle;

        PlayerInventory inventory;

        /// <summary>Raised on a shot: true when it put her down.</summary>
        public event System.Action<bool> Fired;

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            if (input == null) input = GetComponentInChildren<PlayerInputReader>();
            if (muzzle == null)
            {
                var cam = GetComponentInChildren<Camera>();
                muzzle = cam != null ? cam.transform : transform;
            }
        }

        void OnEnable()
        {
            if (input != null) input.Threw += OnThrew;
        }

        void OnDisable()
        {
            if (input != null) input.Threw -= OnThrew;
        }

        /// <summary>
        /// The throw button. With the crossbow in hand this shoots; with anything
        /// else it does nothing and PlayerInventory throws the item as usual.
        /// </summary>
        void OnThrew()
        {
            if (!inventory.HasWeapon) return;
            Fire();
        }

        /// <summary>
        /// Takes the shot. Public so a test can pull the trigger without needing
        /// a virtual keyboard.
        /// </summary>
        /// <returns>True if the shot was taken at all — false means no darts.</returns>
        public bool Fire()
        {
            if (!inventory.HasWeapon) return false;
            if (!inventory.SpendDart()) return false;

            var from = muzzle.position;
            var direction = muzzle.forward;

            // Two passes, because she is a trigger.
            //
            // Her only collider is the trigger the brain uses to decide it has
            // reached the player — she is deliberately not solid, so she can
            // never shove anyone through a wall. An ordinary raycast that ignores
            // triggers therefore goes straight through her, and the first version
            // of this could not hit her at all.
            //
            // So: find the wall first, then look for her in front of it.
            var solids = ~((1 << GameLayers.Player) | (1 << GameLayers.Granny));
            var wallDistance = range;

            if (Physics.Raycast(from, direction, out var wall, range,
                    solids, QueryTriggerInteraction.Ignore))
                wallDistance = wall.distance;

            var hitHer = false;
            var landedAt = from + direction * wallDistance - direction * 0.2f;

            if (Physics.Raycast(from, direction, out var her, wallDistance,
                    1 << GameLayers.Granny, QueryTriggerInteraction.Collide))
            {
                var brain = her.collider.GetComponentInParent<GrannyBrain>();
                if (brain != null)
                {
                    var profile = brain.GetComponent<GrannyPerception>()?.Difficulty;
                    brain.Stun(profile != null ? profile.StunSeconds : 60f);

                    hitHer = true;
                    landedAt = her.point - direction * 0.2f;
                }
            }

            // The dart is recoverable wherever it ended up, hers or the wall's.
            if (dartItem != null)
                Interaction.PickupItem.Spawn(dartItem, landedAt, Random.rotation);

            // Loud either way. Firing is never the quiet option.
            NoiseBus.Emit(from, NoiseKind.ToolWork, gameObject);

            Fired?.Invoke(hitHer);
            return true;
        }
    }
}
