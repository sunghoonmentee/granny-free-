using System.Collections.Generic;
using System.IO;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.AI;
using Granny.Gameplay.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.EditorTools
{
    /// <summary>
    /// Builds the hunter, the difficulty table, the patrol markers and the NavMesh
    /// she walks on, then wires the whole thing together in the House scene.
    ///
    ///     Unity.exe -batchmode -quit -executeMethod \
    ///       Granny.EditorTools.GrannyBuilder.Run
    /// </summary>
    public static class GrannyBuilder
    {
        const string DifficultyDir = "Assets/_Project/Data/Difficulty";
        const string PrefabPath = "Assets/_Project/Prefabs/Granny.prefab";
        const string MaterialDir = "Assets/_Project/Art/Materials";
        const string HouseScenePath = "Assets/_Project/Scenes/House.unity";

        static readonly Vector3 GrannySpawn = new(8f, 0f, 6f);
        static readonly Vector3 BedPosition = new(0f, 0f, -7f);

        [MenuItem("Granny/Build Granny", priority = 30)]
        public static void Run()
        {
            var difficulties = BuildDifficulties();

            // Flush before anything references these. A newly created asset that
            // has not been written yet can be re-imported as a different instance
            // mid-run, and the scene then serialises the reference to it as null -
            // which is how GameDirector silently ended up with no difficulty.
            AssetDatabase.SaveAssets();

            var normal = difficulties.Find(d => d.DisplayName == "Normal") ?? difficulties[0];
            var prefab = BuildPrefab(normal);

            PopulateScene(prefab, normal);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Granny] done");
        }

        // ------------------------------------------------------------------
        // Difficulty table
        // ------------------------------------------------------------------

        /// <summary>
        /// Five settings that change how much of the house she can perceive at
        /// once, rather than how much damage anything does.
        /// </summary>
        static readonly (string name, int days, float patrol, float investigate, float chase,
            float reaction, float sight, float angle, float memory, float search, float check,
            float stun, int traps, float creak, int locks)[]
            DifficultyTable =
            {
                // Practice has no hunter at all, so her numbers are only there to
                // keep the asset valid. Its floorboards are turned up on purpose:
                // with nobody coming, a creak is a lesson rather than a threat.
                ("Practice", 99, 1.0f, 1.4f, 2.6f, 1.20f, 8f, 70f, 2.5f, 4f, 0.15f, 120f, 0, 0.50f, 3),
                ("Easy", 5, 1.2f, 1.8f, 3.4f, 0.70f, 10f, 85f, 4f, 5f, 0.30f, 120f, 2, 0f, 3),
                ("Normal", 5, 1.5f, 2.3f, 4.1f, 0.35f, 13f, 105f, 6f, 7f, 0.50f, 60f, 3, 0.40f, 4),
                ("Hard", 5, 1.8f, 2.8f, 4.7f, 0.15f, 17f, 125f, 9f, 10f, 0.70f, 30f, 3, 0.65f, 5),
                ("Extreme", 5, 2.2f, 3.3f, 5.4f, 0.05f, 22f, 150f, 13f, 14f, 0.90f, 15f, 4, 1.00f, 6),
            };

        static List<DifficultyProfile> BuildDifficulties()
        {
            if (!AssetDatabase.IsValidFolder(DifficultyDir))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Difficulty");

            var built = new List<DifficultyProfile>();

            foreach (var row in DifficultyTable)
            {
                var path = $"{DifficultyDir}/Difficulty_{row.name}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<DifficultyProfile>(path);

                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<DifficultyProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }

                var so = new SerializedObject(profile);
                so.FindProperty("displayName").stringValue = row.name;
                so.FindProperty("daysAllowed").intValue = row.days;
                so.FindProperty("patrolSpeed").floatValue = row.patrol;
                so.FindProperty("investigateSpeed").floatValue = row.investigate;
                so.FindProperty("chaseSpeed").floatValue = row.chase;
                so.FindProperty("reactionDelay").floatValue = row.reaction;
                so.FindProperty("sightRange").floatValue = row.sight;
                so.FindProperty("sightAngle").floatValue = row.angle;
                so.FindProperty("chaseMemory").floatValue = row.memory;
                so.FindProperty("searchDuration").floatValue = row.search;
                so.FindProperty("hidingSpotCheckChance").floatValue = row.check;
                so.FindProperty("stunSeconds").floatValue = row.stun;
                so.FindProperty("trapLimit").intValue = row.traps;
                so.FindProperty("creakyFloorShare").floatValue = row.creak;
                so.FindProperty("frontDoorLocks").intValue = row.locks;
                so.ApplyModifiedPropertiesWithoutUndo();

                built.Add(profile);
            }

            return built;
        }

        /// <summary>
        /// Hands the poser its bones. Wired explicitly rather than looked up by
        /// name at runtime, so a renamed bone is a compile-time or inspector
        /// problem rather than a limb that silently stops moving.
        /// </summary>
        static void WirePose(AgataPose pose, AgataBuilder.Rig rig)
        {
            var so = new SerializedObject(pose);

            void Set(string field, Transform bone) =>
                so.FindProperty(field).objectReferenceValue = bone;

            Set("body", rig.Root);
            Set("spine", rig.Spine);
            Set("chest", rig.Chest);
            Set("neck", rig.Neck);
            Set("head", rig.Head);
            Set("armL", rig.ArmL);
            Set("forearmL", rig.ForearmL);
            Set("armR", rig.ArmR);
            Set("forearmR", rig.ForearmR);
            Set("thighL", rig.ThighL);
            Set("shinL", rig.ShinL);
            Set("thighR", rig.ThighR);
            Set("shinR", rig.ShinR);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // The hunter
        // ------------------------------------------------------------------

        static GameObject BuildPrefab(DifficultyProfile difficulty)
        {
            var root = new GameObject("Granny");
            root.layer = GameLayers.Granny;
            root.tag = "Granny";

            // A jointed figure rather than a capsule: bent forward, arms too
            // long, eyes that catch what little light the house has. What she
            // looks like up close matters far less than what her outline does at
            // the end of a corridor, and the outline is the part built here.
            var rig = AgataBuilder.Build(root.transform, GameLayers.Granny);
            var eye = rig.Eye;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.34f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = difficulty.PatrolSpeed;
            agent.angularSpeed = 520f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            // A trigger, not a solid collider: being caught is a decision the brain
            // makes, and she must never physically shove the player through a wall.
            var reach = root.AddComponent<CapsuleCollider>();
            reach.isTrigger = true;
            reach.radius = 0.4f;
            reach.height = 1.8f;
            reach.center = new Vector3(0f, 0.9f, 0f);

            var perception = root.AddComponent<GrannyPerception>();
            Wire(perception, "difficulty", difficulty);
            Wire(perception, "eye", eye);

            // She carries her own supply of traps. Looking the prefab up here
            // rather than at runtime keeps the dependency visible in the asset,
            // not hidden in a Resources.Load buried in the brain.
            var setter = root.AddComponent<TrapSetter>();
            var trapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Props/BearTrap.prefab");

            if (trapPrefab == null)
                Debug.LogWarning("[Granny] No BearTrap prefab yet — run Granny > Build Content first.");
            else
                Wire(setter, "trapPrefab", trapPrefab);

            root.AddComponent<GrannyBrain>();
            root.AddComponent<GrannyVoice>();

            WirePose(root.AddComponent<AgataPose>(), rig);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var ok);
            Object.DestroyImmediate(root);

            if (!ok) Debug.LogError($"[Granny] Failed to save prefab at {PrefabPath}");
            else Debug.Log($"[Granny] prefab written to {PrefabPath}");

            return saved;
        }

        // ------------------------------------------------------------------
        // Scene
        // ------------------------------------------------------------------

        /// <summary>
        /// Places the hunter and the director. The patrol markers, the bed and her
        /// starting spot belong to the level, so they are looked up rather than
        /// recreated — re-running this to retune difficulty must not move the house.
        /// </summary>
        static void PopulateScene(GameObject grannyPrefab, DifficultyProfile difficulty)
        {
            var scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Granny" or "Director")
                    Object.DestroyImmediate(root);

            var patrolPoints = new List<Transform>();
            foreach (var marker in GameObject.FindGameObjectsWithTag("SpawnPoint"))
                patrolPoints.Add(marker.transform);

            if (patrolPoints.Count == 0)
                Debug.LogWarning("[Granny] No patrol markers found. Run Granny > Build House first.");

            var spawnMarker = FindByName("GrannySpawn");
            var bed = FindByName("Bed");
            var spawnPosition = spawnMarker != null ? spawnMarker.position : GrannySpawn;

            var granny = (GameObject)PrefabUtility.InstantiatePrefab(grannyPrefab, scene);
            granny.transform.position = spawnPosition;
            granny.GetComponent<GrannyBrain>().SetPatrolPoints(patrolPoints);

            var director = new GameObject("Director").AddComponent<GameDirector>();
            Wire(director, "difficulty", difficulty);
            Wire(director, "granny", granny.GetComponent<GrannyBrain>());
            if (bed != null) Wire(director, "bed", bed);
            if (spawnMarker != null) Wire(director, "grannySpawn", spawnMarker);

            var player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player != null) Wire(director, "player", player);

            BuildKit.SaveScene(scene, "Granny");
            Debug.Log($"[Granny] placed with {patrolPoints.Count} patrol points");
        }

        static Transform FindByName(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (transform != null && transform.name == name)
                    return transform;

            return null;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        static Material EnsureMaterial(string name, Color colour)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Wire(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[Granny] {target.GetType().Name} has no field '{fieldName}'");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
