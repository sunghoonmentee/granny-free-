using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Interaction;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// Plays the scenario the fix has to be judged on, and records it.
    ///
    /// The player stands in the first-floor bedroom and throws a preserve jar out
    /// onto the landing. Granny starts two floors below, in the cellar. Everything
    /// after that is the real game: the real jar, the real physics, the real noise
    /// rules, the real NavMesh. Three cameras record it frame by frame and her
    /// route is logged, so the claim "she comes upstairs" can be looked at rather
    /// than taken on trust.
    ///
    /// Skipped unless asked for, because it renders and runs in real time:
    ///
    ///     ./tools/unity.ps1 test -Platform PlayMode -WithGraphics \
    ///         -Extra @('-runScenario','-testFilter','HearingScenarioCapture')
    /// </summary>
    public class HearingScenarioCapture
    {
        const string Flag = "-runScenario";
        const float CaptureInterval = 0.4f;
        const float GiveUpAfter = 75f;

        Scene house;
        GrannyBrain granny;
        PlayerMotor player;
        string outputDir;

        readonly List<string> log = new();
        readonly List<string> frames = new();

        Camera planCam;
        Camera chaseCam;
        Camera eyeCam;
        RenderTexture planRt;
        RenderTexture chaseRt;
        RenderTexture eyeRt;

        static bool Requested =>
            System.Environment.GetCommandLineArgs().Any(a => a == Flag);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (!Requested) yield break;

            SaveSystem.Delete();

            yield return SceneManager.LoadSceneAsync(SceneNames.House, LoadSceneMode.Additive);
            house = SceneManager.GetSceneByName(SceneNames.House);
            SceneManager.SetActiveScene(house);
            yield return null;

            granny = Object.FindAnyObjectByType<GrannyBrain>();
            player = Object.FindAnyObjectByType<PlayerMotor>();

            var spawn = GameObject.Find("GrannySpawn");
            granny.ResetForNewDay(spawn.transform.position);

            // In the south-east bedroom, facing the doorway onto the landing.
            player.Warp(new Vector3(8f, HouseLayout.FloorHeight + 0.1f, -2.5f), 0f);

            outputDir = Path.Combine(
                Directory.GetParent(Application.dataPath)!.Parent!.FullName,
                "unity-logs", "scenario", System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(outputDir);

            SetUpCameras();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!Requested) yield break;

            foreach (var cam in new[] { planCam, chaseCam, eyeCam })
                if (cam != null) Object.DestroyImmediate(cam.gameObject);

            foreach (var rt in new[] { planRt, chaseRt, eyeRt })
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }

            SaveSystem.Delete();

            var blank = SceneManager.CreateScene($"Blank_{System.Guid.NewGuid():N}");
            SceneManager.SetActiveScene(blank);

            if (house.IsValid() && house.isLoaded)
                yield return SceneManager.UnloadSceneAsync(house);
        }

        // ------------------------------------------------------------------
        // Cameras
        // ------------------------------------------------------------------

        /// <summary>
        /// Cameras are rendered on demand through URP's render-request API rather
        /// than by waiting for the pipeline's own frame. The editor runs this with
        /// -batchmode, and in batch mode Unity never fires the end-of-frame
        /// callback, so a coroutine that yields WaitForEndOfFrame to read the
        /// screen simply never resumes — which is exactly how the first attempt at
        /// this scenario hung with an empty output folder. A render request draws
        /// the camera synchronously and hands back the texture, no frame callback
        /// involved.
        /// </summary>
        void SetUpCameras()
        {
            planRt = new RenderTexture(720, 720, 24);
            chaseRt = new RenderTexture(960, 540, 24);
            eyeRt = new RenderTexture(960, 540, 24);

            planCam = MakeCamera("PlanCam", planRt);
            planCam.orthographic = true;
            planCam.orthographicSize = 12.5f;
            planCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            chaseCam = MakeCamera("ChaseCam", chaseRt);
            chaseCam.fieldOfView = 70f;

            eyeCam = MakeCamera("EyeCam", eyeRt);
            eyeCam.fieldOfView = 68f;
        }

        static Camera MakeCamera(string name, RenderTexture target)
        {
            var go = new GameObject(name, typeof(Camera));
            var cam = go.GetComponent<Camera>();

            cam.targetTexture = target;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 120f;
            cam.depth = -10;

            // Only drawn when asked for, by CaptureFrame.
            cam.enabled = false;

            return cam;
        }

        /// <summary>Draws one camera into its own texture, right now.</summary>
        static void RenderNow(Camera cam, RenderTexture target)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };

            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
                return;
            }

            // Built-in pipeline, or a URP version without render requests.
            cam.targetTexture = target;
            cam.Render();
        }

        /// <summary>
        /// Keeps the three views pointed at the right things: a plan view cut in
        /// just under the ceiling of whichever floor she is on, a chase camera
        /// pulled back until it hits a wall, and the player's own eyes.
        /// </summary>
        void AimCameras()
        {
            var her = granny.transform.position;
            var floorY = HouseLayout.FloorY(HouseLayout.FloorOf(her));

            planCam.transform.position = new Vector3(her.x, floorY + 2.9f, her.z);

            var back = -granny.transform.forward;
            var eye = her + Vector3.up * 1.7f;
            var wanted = eye + back * 3.2f + Vector3.up * 1.1f;

            if (Physics.Linecast(eye, wanted, out var hit, GameLayers.SightBlockers,
                    QueryTriggerInteraction.Ignore))
                wanted = hit.point - (wanted - eye).normalized * 0.3f;

            chaseCam.transform.position = wanted;
            chaseCam.transform.LookAt(her + Vector3.up * 1.2f);

            var playerEye = player.GetComponentInChildren<Camera>();
            if (playerEye != null)
                eyeCam.transform.SetPositionAndRotation(
                    playerEye.transform.position, playerEye.transform.rotation);
        }

        IEnumerator CaptureFrame(int index, float time)
        {
            RenderNow(planCam, planRt);
            RenderNow(chaseCam, chaseRt);
            RenderNow(eyeCam, eyeRt);

            yield return null;

            var name = $"{index:D3}";
            Save(planRt, Path.Combine(outputDir, $"{name}_plan.jpg"));
            Save(chaseRt, Path.Combine(outputDir, $"{name}_chase.jpg"));
            Save(eyeRt, Path.Combine(outputDir, $"{name}_eye.jpg"));

            frames.Add(name);

            var her = granny.transform.position;
            log.Add(string.Join(",", new[]
            {
                time.ToString("F2", CultureInfo.InvariantCulture),
                name,
                HouseLayout.FloorOf(her).ToString(),
                HouseLayout.FloorName(HouseLayout.FloorOf(her)),
                granny.State.ToString(),
                her.x.ToString("F2", CultureInfo.InvariantCulture),
                her.y.ToString("F2", CultureInfo.InvariantCulture),
                her.z.ToString("F2", CultureInfo.InvariantCulture),
            }));
        }

        static void Save(RenderTexture rt, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToJPG(88));

            Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
        }

        // ------------------------------------------------------------------
        // The run
        // ------------------------------------------------------------------

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SheComesUpstairsWhenAJarBreaks()
        {
            if (!Requested)
            {
                Assert.Ignore($"Pass {Flag} to record the scenario.");
                yield break;
            }

            log.Add("t,frame,floor,floorName,state,x,y,z");

            var startFloor = HouseLayout.FloorOf(granny.transform.position);
            Assert.AreEqual(HouseLayout.Basement, startFloor, "She should start in the cellar.");

            // A real jar, thrown by the real inventory, breaking on the real floor.
            var jar = FindJarDefinition();
            var inventory = player.GetComponentInChildren<PlayerInventory>();
            inventory.TryTake(jar);

            AimCameras();
            yield return CaptureFrame(0, 0f);

            inventory.ThrowHeld();

            var elapsed = 0f;
            var nextCapture = 0f;
            var index = 1;
            var reachedGround = false;
            var breakPoint = Vector3.zero;
            var broke = false;

            void OnHeard(Noise n)
            {
                if (n.Kind != NoiseKind.Breakage) return;
                broke = true;
                breakPoint = n.Position;
            }

            NoiseBus.Heard += OnHeard;

            try
            {
                while (elapsed < GiveUpAfter)
                {
                    elapsed += Time.deltaTime;
                    nextCapture -= Time.deltaTime;

                    var floor = HouseLayout.FloorOf(granny.transform.position);
                    if (floor >= HouseLayout.Ground) reachedGround = true;

                    if (nextCapture <= 0f)
                    {
                        nextCapture = CaptureInterval;
                        AimCameras();
                        yield return CaptureFrame(index++, elapsed);
                    }
                    else
                    {
                        yield return null;
                    }

                    if (broke && Vector3.Distance(granny.transform.position, breakPoint) < 2.5f)
                        break;
                }
            }
            finally
            {
                NoiseBus.Heard -= OnHeard;
            }

            // A few frames of her standing over the spot, which is the payoff shot.
            for (var i = 0; i < 4; i++)
            {
                AimCameras();
                yield return CaptureFrame(index++, elapsed);
            }

            var arrived = broke && Vector3.Distance(granny.transform.position, breakPoint) < 2.5f;
            WriteReport(elapsed, breakPoint, arrived, reachedGround);

            Assert.IsTrue(broke, "The jar never broke, so nothing was ever heard.");
            Assert.IsTrue(reachedGround, "She never left the cellar.");
            Assert.IsTrue(arrived,
                $"She did not reach the jar within {GiveUpAfter:F0}s. " +
                $"Last seen on {HouseLayout.FloorName(HouseLayout.FloorOf(granny.transform.position))}, " +
                $"state {granny.State}.");
        }

        ItemDefinition FindJarDefinition()
        {
            foreach (var drawer in Object.FindObjectsByType<Drawer>(FindObjectsSortMode.None))
                if (drawer.Contents != null && drawer.Contents.Id == "jar")
                    return drawer.Contents;

#if UNITY_EDITOR
            var found = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/_Project/Data/Items/Item_jar.asset");
            if (found != null) return found;
#endif
            Assert.Fail("No preserve jar item. Run Granny > Build Content.");
            return null;
        }

        void WriteReport(float seconds, Vector3 breakPoint, bool arrived, bool reachedGround)
        {
            File.WriteAllText(Path.Combine(outputDir, "path.csv"),
                string.Join("\n", log), Encoding.UTF8);

            var summary = new StringBuilder();
            summary.AppendLine("{");
            summary.AppendLine($"  \"arrived\": {arrived.ToString().ToLowerInvariant()},");
            summary.AppendLine($"  \"leftTheCellar\": {reachedGround.ToString().ToLowerInvariant()},");
            summary.AppendLine($"  \"seconds\": {seconds.ToString("F1", CultureInfo.InvariantCulture)},");
            summary.AppendLine($"  \"breakPoint\": [{breakPoint.x:F2}, {breakPoint.y:F2}, {breakPoint.z:F2}],");
            summary.AppendLine($"  \"frames\": {frames.Count}");
            summary.AppendLine("}");

            File.WriteAllText(Path.Combine(outputDir, "summary.json"), summary.ToString(), Encoding.UTF8);
            Debug.Log($"[Scenario] {frames.Count} frames written to {outputDir}");
        }
    }
}
