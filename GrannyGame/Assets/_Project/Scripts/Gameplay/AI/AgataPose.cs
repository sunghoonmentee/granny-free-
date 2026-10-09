using UnityEngine;
using UnityEngine.AI;

namespace Granny.Gameplay.AI
{
    /// <summary>
    /// Moves her bones. There is no animation clip; everything here is arithmetic
    /// driven by how fast she is actually walking.
    ///
    /// That is a deliberate choice rather than a shortcut. A clip plays at its
    /// own speed and has to be blended into the speed she is really going, which
    /// is where the sliding feet in cheap horror games come from. Driving the
    /// legs from the agent's own velocity means her feet always match the floor,
    /// at a shuffle and at a run, with no blend tree to get wrong.
    ///
    /// What it is trying to do:
    ///
    ///   * the stick leads. Right arm forward, plant, then the body follows it.
    ///     She is pulling herself along rather than striding
    ///   * the head leads the body on a turn, and keeps looking a moment after
    ///     she has stopped. Heads that snap back to centre look like furniture
    ///   * nothing is symmetrical. The limp is in one leg, the braid is over one
    ///     shoulder, the lean is slightly off-axis
    ///   * when she goes down she folds, and stays folded until she gets up
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AgataPose : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField] Transform spine;
        [SerializeField] Transform chest;
        [SerializeField] Transform neck;
        [SerializeField] Transform head;
        [SerializeField] Transform armL, forearmL;
        [SerializeField] Transform armR, forearmR;
        [SerializeField] Transform thighL, shinL;
        [SerializeField] Transform thighR, shinR;
        [SerializeField] Transform body;

        [Header("Gait")]
        [Tooltip("Strides per metre. Higher is a shuffle, lower is a stride.")]
        [SerializeField, Min(0.1f)] float stridesPerMetre = 0.62f;

        [Tooltip("How far the legs swing, in degrees, at a walk.")]
        [SerializeField, Range(0f, 60f)] float legSwing = 26f;

        [Tooltip("The bad leg swings less and drags. Zero makes her walk evenly.")]
        [SerializeField, Range(0f, 1f)] float limp = 0.45f;

        [Header("Carriage")]
        [Tooltip("Extra forward fold while hurrying.")]
        [SerializeField, Range(0f, 30f)] float chaseLean = 11f;

        [Tooltip("How quickly her head catches up with where she is going.")]
        [SerializeField, Min(0.1f)] float headFollow = 2.6f;

        NavMeshAgent agent;
        GrannyBrain brain;

        float stride;
        float lean;
        float fold;
        float headYaw;

        // Rest poses, so everything here is an offset from what the builder made
        // rather than an absolute that would have to be kept in step with it.
        Quaternion spineRest, chestRest, neckRest, headRest;
        Quaternion armLRest, armRRest, foreLRest, foreRRest;
        Quaternion thighLRest, thighRRest, shinLRest, shinRRest;
        Quaternion bodyRest;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            brain = GetComponent<GrannyBrain>();

            spineRest = Rest(spine);
            chestRest = Rest(chest);
            neckRest = Rest(neck);
            headRest = Rest(head);
            armLRest = Rest(armL);
            armRRest = Rest(armR);
            foreLRest = Rest(forearmL);
            foreRRest = Rest(forearmR);
            thighLRest = Rest(thighL);
            thighRRest = Rest(thighR);
            shinLRest = Rest(shinL);
            shinRRest = Rest(shinR);
            bodyRest = Rest(body);
        }

        static Quaternion Rest(Transform bone) => bone != null ? bone.localRotation : Quaternion.identity;

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (brain != null && brain.IsDown)
            {
                Collapse(dt);
                return;
            }

            fold = Mathf.Lerp(fold, 0f, dt * 4f);

            var speed = agent != null ? agent.velocity.magnitude : 0f;

            // The gait phase advances with distance covered, not with time, so her
            // feet keep up with her however fast the difficulty makes her walk.
            stride += speed * stridesPerMetre * Mathf.PI * 2f * dt;

            var swing = Mathf.Sin(stride);
            var counter = Mathf.Sin(stride + Mathf.PI);
            var effort = Mathf.Clamp01(speed / 3.2f);

            Walk(swing, counter, effort);
            Carry(speed, effort, dt);
            Breathe(dt);
        }

        /// <summary>Legs, and the arms that work against them.</summary>
        void Walk(float swing, float counter, float effort)
        {
            var amount = legSwing * effort;

            // One leg does less of the work and bends less. That asymmetry is
            // most of why she reads as old rather than as a walking cylinder.
            Turn(thighL, thighLRest, swing * amount);
            Turn(thighR, thighRRest, counter * amount * (1f - limp));

            // Knees only bend one way. Taking the negative half of the swing and
            // clamping it is enough to look like a knee and nothing like a hinge.
            Turn(shinL, shinLRest, Mathf.Max(0f, -swing) * amount * 1.3f);
            Turn(shinR, shinRRest, Mathf.Max(0f, -counter) * amount * 0.7f);

            // The stick arm leads by a quarter phase: she plants it, then pulls
            // past it. The other arm hangs and barely moves.
            Turn(armR, armRRest, Mathf.Sin(stride + Mathf.PI * 0.5f) * amount * 0.9f);
            Turn(forearmR, foreRRest, -18f - Mathf.Sin(stride) * 8f * effort);

            Turn(armL, armLRest, counter * amount * 0.45f);
            Turn(forearmL, foreLRest, -10f * effort);
        }

        /// <summary>The fold of the back, and the head that leads it.</summary>
        void Carry(float speed, float effort, float dt)
        {
            var wanted = chaseLean * effort;
            lean = Mathf.Lerp(lean, wanted, dt * 3f);
            Turn(spine, spineRest, lean);

            // Where she is going, in her own frame. Her head gets there first.
            var heading = agent != null && agent.velocity.sqrMagnitude > 0.04f
                ? Vector3.SignedAngle(transform.forward, agent.velocity.normalized, Vector3.up)
                : 0f;

            headYaw = Mathf.Lerp(headYaw, Mathf.Clamp(heading, -55f, 55f), dt * headFollow);

            if (neck != null)
                neck.localRotation = neckRest * Quaternion.Euler(0f, headYaw * 0.4f, 0f);

            if (head != null)
                head.localRotation = headRest * Quaternion.Euler(-lean * 0.8f, headYaw * 0.6f, 0f);

            // Standing still she does not freeze; she sways, which is what stops
            // a stopped figure reading as a prop.
            if (speed < 0.1f && chest != null)
                chest.localRotation = chestRest * Quaternion.Euler(
                    0f, Mathf.Sin(Time.time * 0.7f) * 3f, Mathf.Sin(Time.time * 0.5f) * 2f);
        }

        void Breathe(float dt)
        {
            if (chest == null) return;

            var breath = Mathf.Sin(Time.time * 1.6f) * 1.2f;
            chest.localRotation = Quaternion.Slerp(
                chest.localRotation, chestRest * Quaternion.Euler(breath, 0f, 0f), dt * 2f);
        }

        /// <summary>
        /// Folding up where she stands. Not a ragdoll — a ragdoll of a figure made
        /// of boxes looks like a dropped chair — just a fast, heavy fold forward
        /// that holds until the brain says she is back on her feet.
        /// </summary>
        void Collapse(float dt)
        {
            fold = Mathf.Lerp(fold, 1f, dt * 6f);

            if (body != null)
                body.localRotation = Quaternion.Slerp(bodyRest,
                    bodyRest * Quaternion.Euler(84f, 0f, 14f), fold);

            Turn(spine, spineRest, 26f * fold);
            Turn(thighL, thighLRest, -52f * fold);
            Turn(thighR, thighRRest, -44f * fold);
            Turn(shinL, shinLRest, 68f * fold);
            Turn(shinR, shinRRest, 60f * fold);
            Turn(armL, armLRest, 36f * fold);
            Turn(armR, armRRest, 28f * fold);
        }

        static void Turn(Transform bone, Quaternion rest, float degrees)
        {
            if (bone != null) bone.localRotation = rest * Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
