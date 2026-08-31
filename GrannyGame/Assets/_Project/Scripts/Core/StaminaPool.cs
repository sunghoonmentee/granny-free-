using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// Sprint budget, as plain C# so it can be unit tested without a scene.
    ///
    /// The rule that matters for the chase: running out is punishing. Once the
    /// pool empties the player is locked out of sprinting until it has refilled
    /// to <see cref="recoveryFraction"/>, so a panicked sprint down a corridor
    /// leaves you walking when Granny rounds the corner.
    /// </summary>
    public sealed class StaminaPool
    {
        readonly float max;
        readonly float drainPerSecond;
        readonly float regenPerSecond;
        readonly float regenDelay;
        readonly float recoveryFraction;

        float current;
        float timeSinceDrain;
        bool exhausted;

        public StaminaPool(
            float max = 6f,
            float drainPerSecond = 1f,
            float regenPerSecond = 0.55f,
            float regenDelay = 1.25f,
            float recoveryFraction = 0.35f)
        {
            this.max = Mathf.Max(0.01f, max);
            this.drainPerSecond = Mathf.Max(0f, drainPerSecond);
            this.regenPerSecond = Mathf.Max(0f, regenPerSecond);
            this.regenDelay = Mathf.Max(0f, regenDelay);
            this.recoveryFraction = Mathf.Clamp01(recoveryFraction);

            current = this.max;
            timeSinceDrain = this.regenDelay;
        }

        public float Current => current;
        public float Max => max;
        public float Normalized => current / max;

        /// <summary>True while sprinting is locked out after emptying the pool.</summary>
        public bool IsExhausted => exhausted;

        /// <summary>
        /// Advances the pool by one frame.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        /// <param name="wantsToSprint">Whether the player is holding sprint and moving.</param>
        /// <returns>Whether sprinting is permitted this frame.</returns>
        public bool Tick(float deltaTime, bool wantsToSprint)
        {
            if (deltaTime < 0f) deltaTime = 0f;

            var sprinting = wantsToSprint && !exhausted && current > 0f;

            if (sprinting)
            {
                current -= drainPerSecond * deltaTime;
                timeSinceDrain = 0f;

                if (current <= 0f)
                {
                    current = 0f;
                    exhausted = true;
                    sprinting = false;
                }

                return sprinting;
            }

            timeSinceDrain += deltaTime;
            if (timeSinceDrain >= regenDelay)
                current = Mathf.Min(max, current + regenPerSecond * deltaTime);

            if (exhausted && current >= max * recoveryFraction)
                exhausted = false;

            return false;
        }

        /// <summary>Refills the pool — used when a new day starts.</summary>
        public void Refill()
        {
            current = max;
            exhausted = false;
            timeSinceDrain = regenDelay;
        }
    }
}
