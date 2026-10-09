using UnityEngine;

namespace Granny.Gameplay.AI
{
    /// <summary>
    /// A place worth putting a trap: the head and foot of a flight of stairs, a
    /// corridor, the cellar door.
    ///
    /// She does not drop traps wherever she happens to be standing. The threat
    /// only works if traps end up on the routes the player has to use anyway —
    /// a trap in the middle of a bedroom floor is scenery.
    /// </summary>
    public sealed class TrapSpot : MonoBehaviour
    {
        [Tooltip("Marks a route nobody can avoid; preferred over an ordinary spot.")]
        [SerializeField] bool chokePoint = true;

        public bool IsChokePoint => chokePoint;

        /// <summary>True while a trap of hers is sitting here.</summary>
        public bool IsTaken { get; set; }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = chokePoint ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
    }
}
