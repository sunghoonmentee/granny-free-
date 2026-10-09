using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// First-person locomotion on a <see cref="CharacterController"/>.
    ///
    /// The three stances are the whole stealth economy of the game: crouching is
    /// slow and nearly silent, walking is the default, sprinting is fast, loud and
    /// limited by stamina. <see cref="State"/> is the single value the noise system
    /// reads, so how fast you move and how far you are heard can never disagree.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField, Min(0f)] float crouchSpeed = 1.15f;
        [SerializeField, Min(0f)] float walkSpeed = 2.6f;
        [SerializeField, Min(0f)] float sprintSpeed = 4.8f;

        [Header("Feel")]
        [Tooltip("How quickly horizontal velocity chases the input. Lower feels heavier.")]
        [SerializeField, Min(0.1f)] float acceleration = 14f;
        [SerializeField, Range(0f, 1f)] float airControl = 0.35f;
        [SerializeField] float gravity = -18f;
        [Tooltip("Downward push kept on while grounded so slopes and steps do not bounce.")]
        [SerializeField, Min(0f)] float groundStick = 2f;

        [Header("Stance")]
        [SerializeField, Min(0.5f)] float standingHeight = 1.8f;
        [SerializeField, Min(0.4f)] float crouchHeight = 1.05f;
        [SerializeField, Min(0.01f)] float stanceLerpSpeed = 11f;
        [Tooltip("Distance from the top of the capsule down to the eyes.")]
        [SerializeField, Min(0f)] float eyeOffsetFromTop = 0.14f;

        [Header("Stamina")]
        [SerializeField, Min(0.1f)] float staminaSeconds = 6f;
        [SerializeField, Min(0f)] float staminaRegenPerSecond = 0.55f;
        [SerializeField, Min(0f)] float staminaRegenDelay = 1.25f;
        [Tooltip("Fraction of the pool that must refill before sprinting unlocks again.")]
        [SerializeField, Range(0f, 1f)] float staminaRecoveryFraction = 0.35f;

        [Header("Wiring")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform cameraPivot;

        CharacterController controller;
        StaminaPool stamina;

        Vector3 horizontalVelocity;
        float verticalVelocity;
        float currentHeight;
        bool crouching;

        /// <summary>Current stance and pace — the input to footstep noise.</summary>
        float limpFactor = 1f;
        float limpRemaining;

        public MoveState State { get; private set; } = MoveState.Idle;

        /// <summary>Horizontal speed in metres per second.</summary>
        public float Speed => horizontalVelocity.magnitude;

        public bool IsCrouching => crouching;
        public bool IsGrounded => controller != null && controller.isGrounded;

        /// <summary>0-1 sprint budget remaining, for the HUD.</summary>
        public float StaminaNormalized => stamina?.Normalized ?? 1f;
        public bool IsExhausted => stamina?.IsExhausted ?? false;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentHeight = standingHeight;
            ApplyHeight(standingHeight);

            stamina = new StaminaPool(
                staminaSeconds,
                drainPerSecond: 1f,
                staminaRegenPerSecond,
                staminaRegenDelay,
                staminaRecoveryFraction);

            if (input == null) input = GetComponentInChildren<PlayerInputReader>();
            if (input == null)
                Debug.LogError($"[{nameof(PlayerMotor)}] No {nameof(PlayerInputReader)} wired up.", this);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            var moveInput = input != null ? input.Move : Vector2.zero;
            var wantsCrouch = input != null && input.CrouchHeld;
            var wantsSprint = input != null && input.SprintHeld;

            UpdateStance(wantsCrouch, dt);

            // Sprinting only counts as effort when actually going somewhere, and
            // never while crouched — a crouch-sprint would defeat the stealth loop.
            var tryingToSprint = wantsSprint && !crouching && moveInput.sqrMagnitude > 0.01f;
            var sprinting = stamina.Tick(dt, tryingToSprint);

            var targetSpeed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            targetSpeed *= LimpFactor(dt);
            var wish = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            var responsiveness = acceleration * (controller.isGrounded ? 1f : airControl);
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                wish * targetSpeed,
                responsiveness * dt);

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -groundStick;
            else
                verticalVelocity += gravity * dt;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);

            State = ClassifyMovement(sprinting);
        }

        MoveState ClassifyMovement(bool sprinting)
        {
            // Compare against the crouch speed rather than zero: brushing a wall
            // leaves a sliver of velocity that should not read as walking.
            if (horizontalVelocity.sqrMagnitude < crouchSpeed * crouchSpeed * 0.09f)
                return MoveState.Idle;

            if (crouching) return MoveState.Crouching;
            return sprinting ? MoveState.Sprinting : MoveState.Walking;
        }

        void UpdateStance(bool wantsCrouch, float dt)
        {
            // Standing up under a low shelf would eject the player through it.
            if (crouching && !wantsCrouch && !HasHeadroom(standingHeight))
                wantsCrouch = true;

            crouching = wantsCrouch;

            var targetHeight = crouching ? crouchHeight : standingHeight;
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, 1f - Mathf.Exp(-stanceLerpSpeed * dt));
            if (Mathf.Abs(currentHeight - targetHeight) < 0.005f) currentHeight = targetHeight;

            ApplyHeight(currentHeight);
        }

        void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraPivot != null)
            {
                var eye = cameraPivot.localPosition;
                eye.y = height - eyeOffsetFromTop;
                cameraPivot.localPosition = eye;
            }
        }

        bool HasHeadroom(float height)
        {
            var radius = controller.radius * 0.95f;
            var bottom = transform.position + Vector3.up * (radius + 0.05f);
            var top = transform.position + Vector3.up * (height - radius);

            return !Physics.CheckCapsule(
                bottom, top, radius,
                GameLayers.WorldMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>Teleports the player, e.g. waking up in bed at the start of a day.</summary>
        /// <summary>
        /// Slows the player for a while — what a bear trap leaves behind.
        ///
        /// A limp cannot be shortened by sprinting it off or sitting it out, so
        /// the cost of walking into a trap is paid in the only currency that
        /// matters here: the time she gets to close the distance.
        /// </summary>
        public void Limp(float factor, float seconds)
        {
            limpFactor = Mathf.Clamp(factor, 0.1f, 1f);
            limpRemaining = Mathf.Max(limpRemaining, seconds);
        }

        /// <summary>True while a trap is still being paid for.</summary>
        public bool IsLimping => limpRemaining > 0f;

        float LimpFactor(float dt)
        {
            if (limpRemaining <= 0f) return 1f;

            limpRemaining -= dt;
            return limpFactor;
        }

        public void Warp(Vector3 position, float yawDegrees)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            controller.enabled = true;

            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            crouching = false;
            currentHeight = standingHeight;
            ApplyHeight(standingHeight);
            stamina.Refill();
            limpRemaining = 0f;
            State = MoveState.Idle;
        }
    }
}
