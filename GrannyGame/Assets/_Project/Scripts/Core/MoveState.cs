namespace Granny.Core
{
    /// <summary>
    /// How the player is currently moving. This is the single input to both the
    /// footstep noise volume and the animation/UI feedback, so stance and loudness
    /// can never drift out of sync.
    /// </summary>
    public enum MoveState
    {
        Idle,
        Crouching,
        Walking,
        Sprinting,
    }
}
