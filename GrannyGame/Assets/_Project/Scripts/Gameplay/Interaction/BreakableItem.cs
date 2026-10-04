using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Interaction
{
    /// <summary>
    /// A jar of preserves, or anything else that does not survive being thrown.
    ///
    /// This is our version of the original's habit of hiding things inside fruit:
    /// some of the jars on the cellar shelves have something in them, and the only
    /// way to find out is to break one — which is heard from anywhere in the house.
    /// Getting the thing you need and telling her exactly where you are is the
    /// same action.
    /// </summary>
    [RequireComponent(typeof(PickupItem))]
    public sealed class BreakableItem : MonoBehaviour
    {
        [Tooltip("Impact speed that shatters it. Below this it just clatters.")]
        [SerializeField, Min(0.1f)] float breakSpeed = 3f;

        [Tooltip("Left behind when it breaks. Assigned by the spawn table.")]
        [SerializeField] ItemDefinition contents;

        bool broken;

        public bool IsBroken => broken;
        public ItemDefinition Contents => contents;

        void OnCollisionEnter(Collision collision)
        {
            if (broken) return;
            if (collision.relativeVelocity.magnitude < breakSpeed) return;

            Shatter();
        }

        /// <summary>Breaks it now — used by scripted scenarios and by tests.</summary>
        public void Shatter()
        {
            if (broken) return;
            broken = true;

            NoiseBus.Emit(transform.position, NoiseKind.Breakage, gameObject);

            if (contents != null)
                PickupItem.Spawn(contents, transform.position + Vector3.up * 0.15f, Random.rotation);

            Destroy(gameObject);
        }

        public void SetContents(ItemDefinition item) => contents = item;
    }
}
