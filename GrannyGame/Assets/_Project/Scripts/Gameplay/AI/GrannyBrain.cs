using System;
using System.Collections.Generic;
using Granny.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.Gameplay.AI
{
    /// <summary>What she is doing right now.</summary>
    public enum GrannyState
    {
        /// <summary>Wandering the house on her own business.</summary>
        Patrol,

        /// <summary>Walking to where a sound came from.</summary>
        Investigate,

        /// <summary>At the spot, poking around — opening wardrobes, looking under beds.</summary>
        Search,

        /// <summary>Has the player in sight and is closing.</summary>
        Chase,

        /// <summary>Close enough to grab. The catch is resolved by the day cycle.</summary>
        Catch,
    }

    /// <summary>
    /// The hunter's decision-making.
    ///
    /// The whole design goal is that her behaviour is legible: the player should
    /// always be able to work out what she knows and why she is where she is.
    /// So there is no cheating — every transition below is driven by something
    /// she saw or heard, and she gives up in a bounded, predictable time.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(GrannyPerception))]
    public sealed class GrannyBrain : MonoBehaviour
    {
        [Header("Patrol")]
        [Tooltip("Places she wanders between. Filled from the scene's patrol markers if empty.")]
        [SerializeField] List<Transform> patrolPoints = new();
        [SerializeField, Min(0f)] float patrolPauseSeconds = 2f;

        [Header("Catching")]
        [Tooltip("Distance at which the player is caught.")]
        [SerializeField, Min(0.2f)] float catchRange = 1.35f;

        [Header("Doors")]
        [Tooltip("How far ahead she checks for a closed door to shove open.")]
        [SerializeField, Min(0f)] float doorReach = 1.6f;

        NavMeshAgent agent;
        GrannyPerception perception;

        int patrolIndex;
        float stateTimer;
        float pauseTimer;
        Interaction.HidingSpot searchingSpot;

        public GrannyState State { get; private set; } = GrannyState.Patrol;

        /// <summary>Raised when she reaches the player. The day cycle listens.</summary>
        public event Action<GameObject> Caught;

        /// <summary>Raised on every state change, for audio cues and debugging.</summary>
        public event Action<GrannyState> StateChanged;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            perception = GetComponent<GrannyPerception>();

            if (patrolPoints.Count == 0) CollectPatrolPoints();
        }

        void Start() => Enter(GrannyState.Patrol);

        void Update()
        {
            stateTimer += Time.deltaTime;

            ShoveDoorsInTheWay();

            switch (State)
            {
                case GrannyState.Patrol: TickPatrol(); break;
                case GrannyState.Investigate: TickInvestigate(); break;
                case GrannyState.Search: TickSearch(); break;
                case GrannyState.Chase: TickChase(); break;
                case GrannyState.Catch: break;
            }
        }

        // ------------------------------------------------------------------
        // States
        // ------------------------------------------------------------------

        void TickPatrol()
        {
            if (perception.CanSeeTarget) { Enter(GrannyState.Chase); return; }
            if (perception.LastKnownPosition.HasValue) { Enter(GrannyState.Investigate); return; }

            if (patrolPoints.Count == 0) return;

            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
                return;
            }

            if (HasArrived())
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
                pauseTimer = patrolPauseSeconds;
                SetDestination(patrolPoints[patrolIndex].position);
                return;
            }

            if (!agent.hasPath) SetDestination(patrolPoints[patrolIndex].position);
        }

        void TickInvestigate()
        {
            if (perception.CanSeeTarget) { Enter(GrannyState.Chase); return; }

            if (!perception.LastKnownPosition.HasValue) { Enter(GrannyState.Patrol); return; }

            // A fresher noise redirects her mid-walk, which is what makes throwing
            // a bottle across the hall actually useful.
            SetDestination(perception.LastKnownPosition.Value);

            if (HasArrived()) Enter(GrannyState.Search);
        }

        void TickSearch()
        {
            if (perception.CanSeeTarget) { Enter(GrannyState.Chase); return; }

            var profile = perception.Difficulty;
            var duration = profile != null ? profile.SearchDuration : 6f;

            if (searchingSpot != null && stateTimer > duration * 0.5f)
            {
                // Opening the wardrobe is the moment the gamble pays off or does not.
                searchingSpot.ForceExit();
                searchingSpot = null;
            }

            if (stateTimer < duration) return;

            perception.Forget();
            Enter(GrannyState.Patrol);
        }

        void TickChase()
        {
            var profile = perception.Difficulty;
            var memory = profile != null ? profile.ChaseMemory : 5f;

            if (perception.CanSeeTarget)
            {
                SetDestination(perception.Target.position);

                if (Vector3.Distance(transform.position, perception.Target.position) <= catchRange)
                {
                    Enter(GrannyState.Catch);
                    Caught?.Invoke(perception.Target.gameObject);
                }
                return;
            }

            // Lost sight. She keeps going to where the player was, then searches
            // there — she does not magically track them through walls.
            if (perception.TimeSinceSeen > memory)
            {
                Enter(GrannyState.Search);
                return;
            }

            if (perception.LastKnownPosition.HasValue)
                SetDestination(perception.LastKnownPosition.Value);
        }

        // ------------------------------------------------------------------
        // Plumbing
        // ------------------------------------------------------------------

        void Enter(GrannyState next)
        {
            State = next;
            stateTimer = 0f;

            var profile = perception.Difficulty;

            agent.speed = next switch
            {
                GrannyState.Chase => profile != null ? profile.ChaseSpeed : 4f,
                GrannyState.Investigate => profile != null ? profile.InvestigateSpeed : 2.2f,
                _ => profile != null ? profile.PatrolSpeed : 1.5f,
            };

            if (next == GrannyState.Search)
            {
                agent.ResetPath();
                searchingSpot = FindHidingSpotToCheck();
            }
            else
            {
                searchingSpot = null;
            }

            if (next == GrannyState.Catch) agent.ResetPath();

            StateChanged?.Invoke(next);
        }

        bool HasArrived() =>
            !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f;

        void SetDestination(Vector3 position)
        {
            if (!agent.isOnNavMesh) return;

            if (NavMesh.SamplePosition(position, out var hit, 2.5f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        /// <summary>
        /// A closed door would otherwise stop her dead, since the NavMesh is baked
        /// with doorways open. She shoves whatever is directly in front of her.
        /// </summary>
        void ShoveDoorsInTheWay()
        {
            if (State == GrannyState.Catch) return;

            var origin = transform.position + Vector3.up * 1.1f;
            if (!Physics.Raycast(origin, transform.forward, out var hit, doorReach,
                    1 << GameLayers.Door, QueryTriggerInteraction.Ignore))
                return;

            var door = hit.collider.GetComponentInParent<Interaction.HingeDoor>();
            if (door == null || door.IsOpen) return;

            if (State == GrannyState.Chase) door.Slam(gameObject);
            else door.SetOpen(true, gameObject);
        }

        Interaction.HidingSpot FindHidingSpotToCheck()
        {
            var profile = perception.Difficulty;
            var chance = profile != null ? profile.HidingSpotCheckChance : 0.5f;

            if (UnityEngine.Random.value > chance) return null;

            Interaction.HidingSpot nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var spot in FindObjectsByType<Interaction.HidingSpot>(FindObjectsSortMode.None))
            {
                var distance = (spot.transform.position - transform.position).sqrMagnitude;
                if (distance > 25f || distance >= nearestDistance) continue;

                nearest = spot;
                nearestDistance = distance;
            }

            return nearest;
        }

        void CollectPatrolPoints()
        {
            foreach (var marker in GameObject.FindGameObjectsWithTag("SpawnPoint"))
                patrolPoints.Add(marker.transform);
        }

        /// <summary>Puts her back at the start of a new day.</summary>
        public void ResetForNewDay(Vector3 position)
        {
            if (agent.isOnNavMesh) agent.ResetPath();

            agent.Warp(position);
            perception.Forget();
            patrolIndex = 0;
            pauseTimer = 0f;
            Enter(GrannyState.Patrol);
        }

        public void SetPatrolPoints(IEnumerable<Transform> points)
        {
            patrolPoints.Clear();
            patrolPoints.AddRange(points);
            patrolIndex = 0;
        }
    }
}
