using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// Anything the player can point at and press E on: a drawer, a door, a
    /// wardrobe to hide in, a hammer to pick up, a padlock to unlock.
    ///
    /// Lives in Core so that the interaction raycast, the HUD prompt and the
    /// objects themselves share one contract without depending on each other.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>The transform used for distance checks and prompt placement.</summary>
        Transform Transform { get; }

        /// <summary>
        /// Short verb phrase for the HUD, e.g. "Open drawer" or "Hide". Read every
        /// frame the object is looked at, so it may change with state.
        /// </summary>
        string Prompt { get; }

        /// <summary>
        /// Whether the interaction is currently offered. A locked door still
        /// reports true — it answers with "It's locked", which is information the
        /// player needs. Returning false hides the prompt entirely.
        /// </summary>
        bool CanInteract(GameObject interactor);

        /// <summary>Runs the interaction. Only called when <see cref="CanInteract"/> passed.</summary>
        void Interact(GameObject interactor);
    }
}
