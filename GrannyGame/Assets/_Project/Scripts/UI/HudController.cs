using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Granny.UI
{
    /// <summary>
    /// The whole in-game HUD, kept deliberately sparse.
    ///
    /// A horror game that tells you everything is not tense, so there is no
    /// health bar, no map and no objective list — only what the player could
    /// plausibly know: what they are looking at, what they are carrying, how
    /// winded they are and how much torch is left.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerFlashlight flashlight;

        // Plain UI.Text rather than TextMeshPro: TMP needs its Essential Resources
        // imported before any text renders, and that import cannot be relied on
        // from a headless build. Revisit when the HUD gets its real styling.
        [Header("Widgets")]
        [SerializeField] Image crosshair;
        [SerializeField] Text promptLabel;
        [SerializeField] Text inventoryLabel;
        [SerializeField] Image staminaFill;
        [SerializeField] CanvasGroup staminaGroup;
        [SerializeField] Image batteryFill;
        [SerializeField] CanvasGroup batteryGroup;

        [Header("Look")]
        [SerializeField] Color crosshairIdle = new(1f, 1f, 1f, 0.35f);
        [SerializeField] Color crosshairActive = new(1f, 0.9f, 0.55f, 0.95f);
        [SerializeField] Color staminaNormal = new(0.85f, 0.85f, 0.9f, 0.8f);
        [SerializeField] Color staminaExhausted = new(0.9f, 0.35f, 0.3f, 0.9f);

        void Awake()
        {
            var player = FindPlayer();
            if (player != null)
            {
                if (interactor == null) interactor = player.GetComponentInChildren<PlayerInteractor>();
                if (inventory == null) inventory = player.GetComponentInChildren<PlayerInventory>();
                if (motor == null) motor = player.GetComponentInChildren<PlayerMotor>();
                if (flashlight == null) flashlight = player.GetComponentInChildren<PlayerFlashlight>(true);
            }
        }

        void OnEnable()
        {
            if (interactor != null) interactor.TargetChanged += OnTargetChanged;
            if (inventory != null) inventory.Changed += RefreshInventory;

            OnTargetChanged(interactor != null ? interactor.Target : null);
            RefreshInventory();
        }

        void OnDisable()
        {
            if (interactor != null) interactor.TargetChanged -= OnTargetChanged;
            if (inventory != null) inventory.Changed -= RefreshInventory;
        }

        void Update()
        {
            UpdateStamina();
            UpdateBattery();
        }

        static GameObject FindPlayer()
        {
            var motor = Object.FindAnyObjectByType<PlayerMotor>();
            return motor != null ? motor.gameObject : null;
        }

        void OnTargetChanged(IInteractable target)
        {
            var hasTarget = target != null;

            if (promptLabel != null)
                promptLabel.text = hasTarget ? target.Prompt : string.Empty;

            if (crosshair != null)
                crosshair.color = hasTarget ? crosshairActive : crosshairIdle;
        }

        /// <summary>
        /// One hand, one line. Empty-handed shows nothing at all rather than the
        /// word "empty" — the HUD should only speak when it has something to say.
        /// </summary>
        void RefreshInventory()
        {
            if (inventoryLabel == null || inventory == null) return;

            inventoryLabel.text = inventory.Held != null ? inventory.Held.DisplayName : string.Empty;
        }

        void UpdateStamina()
        {
            if (motor == null) return;

            var value = motor.StaminaNormalized;

            if (staminaFill != null)
            {
                staminaFill.fillAmount = value;
                staminaFill.color = motor.IsExhausted ? staminaExhausted : staminaNormal;
            }

            // The bar is a distraction while it is full; it fades in only once it
            // is telling the player something.
            if (staminaGroup != null)
                staminaGroup.alpha = Mathf.MoveTowards(
                    staminaGroup.alpha, value >= 0.999f ? 0f : 1f, Time.deltaTime * 4f);
        }

        void UpdateBattery()
        {
            if (flashlight == null) return;

            if (batteryFill != null) batteryFill.fillAmount = flashlight.BatteryNormalized;

            if (batteryGroup != null)
                batteryGroup.alpha = Mathf.MoveTowards(
                    batteryGroup.alpha, flashlight.IsOn ? 1f : 0f, Time.deltaTime * 4f);
        }
    }
}
