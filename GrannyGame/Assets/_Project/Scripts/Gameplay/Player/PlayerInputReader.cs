using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// The single place the rest of the game touches the Input System. Everything
    /// downstream reads plain properties and C# events, so rebinding a key or
    /// swapping the action asset never ripples into gameplay code.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        [Tooltip("Hold to stay crouched (as in most PC horror games), or press to toggle.")]
        [SerializeField] bool holdToCrouch = true;

        InputActionMap gameplayMap;
        InputAction moveAction;
        InputAction lookAction;
        InputAction sprintAction;
        InputAction crouchAction;
        InputAction interactAction;
        InputAction dropAction;
        InputAction throwAction;
        InputAction flashlightAction;
        InputAction pauseAction;
        InputAction slotAction;

        bool crouchToggleState;

        /// <summary>WASD / left stick, normalised to at most unit length.</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Raw look input for this frame.</summary>
        public Vector2 Look { get; private set; }

        /// <summary>
        /// True when <see cref="Look"/> came from a mouse. Mouse deltas are already
        /// per-frame and must not be multiplied by delta time; stick input must be.
        /// </summary>
        public bool LookIsPointerDelta { get; private set; }

        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }

        public event Action Interacted;
        public event Action Dropped;
        public event Action Threw;
        public event Action FlashlightToggled;
        public event Action PauseRequested;

        /// <summary>Fires with a 1-based slot index when a number key is pressed.</summary>
        public event Action<int> SlotSelected;

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"[{nameof(PlayerInputReader)}] No InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            gameplayMap = actions.FindActionMap("Gameplay", throwIfNotFound: true);

            moveAction = gameplayMap.FindAction("Move", throwIfNotFound: true);
            lookAction = gameplayMap.FindAction("Look", throwIfNotFound: true);
            sprintAction = gameplayMap.FindAction("Sprint", throwIfNotFound: true);
            crouchAction = gameplayMap.FindAction("Crouch", throwIfNotFound: true);
            interactAction = gameplayMap.FindAction("Interact", throwIfNotFound: true);
            dropAction = gameplayMap.FindAction("Drop", throwIfNotFound: true);
            throwAction = gameplayMap.FindAction("Throw", throwIfNotFound: true);
            flashlightAction = gameplayMap.FindAction("Flashlight", throwIfNotFound: true);
            pauseAction = gameplayMap.FindAction("Pause", throwIfNotFound: true);
            slotAction = gameplayMap.FindAction("Slot", throwIfNotFound: true);
        }

        void OnEnable()
        {
            if (gameplayMap == null) return;

            interactAction.performed += OnInteract;
            dropAction.performed += OnDrop;
            throwAction.performed += OnThrow;
            flashlightAction.performed += OnFlashlight;
            pauseAction.performed += OnPause;
            slotAction.performed += OnSlot;
            crouchAction.performed += OnCrouchPerformed;

            gameplayMap.Enable();
        }

        void OnDisable()
        {
            if (gameplayMap == null) return;

            interactAction.performed -= OnInteract;
            dropAction.performed -= OnDrop;
            throwAction.performed -= OnThrow;
            flashlightAction.performed -= OnFlashlight;
            pauseAction.performed -= OnPause;
            slotAction.performed -= OnSlot;
            crouchAction.performed -= OnCrouchPerformed;

            gameplayMap.Disable();

            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            CrouchHeld = false;
        }

        void Update()
        {
            if (gameplayMap == null) return;

            Move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            Look = lookAction.ReadValue<Vector2>();
            LookIsPointerDelta = lookAction.activeControl?.device is Pointer;
            SprintHeld = sprintAction.IsPressed();
            CrouchHeld = holdToCrouch ? crouchAction.IsPressed() : crouchToggleState;
        }

        void OnCrouchPerformed(InputAction.CallbackContext _)
        {
            if (!holdToCrouch) crouchToggleState = !crouchToggleState;
        }

        void OnInteract(InputAction.CallbackContext _) => Interacted?.Invoke();
        void OnDrop(InputAction.CallbackContext _) => Dropped?.Invoke();
        void OnThrow(InputAction.CallbackContext _) => Threw?.Invoke();
        void OnFlashlight(InputAction.CallbackContext _) => FlashlightToggled?.Invoke();
        void OnPause(InputAction.CallbackContext _) => PauseRequested?.Invoke();

        void OnSlot(InputAction.CallbackContext context)
        {
            var slot = Mathf.RoundToInt(context.ReadValue<float>());
            if (slot > 0) SlotSelected?.Invoke(slot);
        }

        /// <summary>Silences gameplay input, e.g. while a menu is open or on death.</summary>
        public void SetGameplayEnabled(bool value)
        {
            if (gameplayMap == null) return;

            if (value)
            {
                gameplayMap.Enable();
                return;
            }

            gameplayMap.Disable();

            // Pause has to survive the silence, or there is no way back out of a menu.
            pauseAction.Enable();

            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            crouchToggleState = false;
            CrouchHeld = false;
        }
    }
}
