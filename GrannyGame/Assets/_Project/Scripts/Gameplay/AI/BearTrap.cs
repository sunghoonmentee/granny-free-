using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.AI
{
    /// <summary>
    /// A trap on the floor. Stepping in one pins the player in place for a few
    /// seconds and makes a great deal of noise — which is the real damage, since
    /// it tells Granny exactly where you are and guarantees she arrives before
    /// you are free.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BearTrap : MonoBehaviour, IInteractable
    {
        [Header("Bite")]
        [SerializeField, Min(0.5f)] float holdSeconds = 4.5f;

        [Header("Freeing")]
        [Tooltip("Seconds shaved off by prying it open with a tool.")]
        [SerializeField, Min(0f)] float pryBonusSeconds = 3f;
        [SerializeField] string pryTag = "pry";

        [Header("Visuals")]
        [Tooltip("Swapped in once the trap has fired, so a sprung trap reads as harmless.")]
        [SerializeField] GameObject armedVisual;
        [SerializeField] GameObject sprungVisual;

        GameObject captive;
        float releaseTime;

        public bool IsArmed { get; private set; } = true;
        public bool HasCaptive => captive != null;

        public Transform Transform => transform;

        public string Prompt => HasCaptive ? "Pry open" : IsArmed ? "Disarm" : "Sprung";

        void Awake() => SetVisual(armed: true);

        void Update()
        {
            if (captive == null) return;
            if (Time.time < releaseTime) return;

            Release();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsArmed || captive != null) return;
            if (other.gameObject.layer != GameLayers.Player) return;

            var motor = other.GetComponentInParent<Player.PlayerMotor>();
            if (motor == null) return;

            IsArmed = false;
            captive = motor.gameObject;
            releaseTime = Time.time + holdSeconds;
            motor.enabled = false;

            SetVisual(armed: false);

            // Loud on purpose. Being trapped is only frightening because it is
            // also an announcement.
            NoiseBus.Emit(transform.position, NoiseKind.Trap, gameObject);
        }

        public bool CanInteract(GameObject interactor) => IsArmed || HasCaptive;

        public void Interact(GameObject interactor)
        {
            if (HasCaptive)
            {
                var inventory = interactor.GetComponentInChildren<Player.PlayerInventory>();
                if (inventory != null && inventory.HasTag(pryTag))
                    releaseTime -= pryBonusSeconds;

                return;
            }

            if (!IsArmed) return;

            // Disarming is quiet and permanent — worth the seconds spent, if you
            // spot it before you walk into it.
            IsArmed = false;
            SetVisual(armed: false);
        }

        void Release()
        {
            if (captive != null && captive.TryGetComponent<Player.PlayerMotor>(out var motor))
                motor.enabled = true;

            captive = null;
        }

        void SetVisual(bool armed)
        {
            if (armedVisual != null) armedVisual.SetActive(armed);
            if (sprungVisual != null) sprungVisual.SetActive(!armed);
        }

        /// <summary>Re-arms the trap at the start of a new day.</summary>
        public void Rearm()
        {
            Release();
            IsArmed = true;
            SetVisual(armed: true);
        }
    }
}
