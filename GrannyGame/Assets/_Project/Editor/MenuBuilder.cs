using Granny.Core;
using Granny.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Granny.EditorTools
{
    /// <summary>
    /// Builds the title screen and the in-game pause panel.
    ///
    ///     Unity.exe -batchmode -quit -executeMethod \
    ///       Granny.EditorTools.MenuBuilder.Run
    /// </summary>
    public static class MenuBuilder
    {
        const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        const string HouseScenePath = "Assets/_Project/Scenes/House.unity";
        const string PrefabDir = "Assets/_Project/Prefabs";

        static readonly Color Ink = new(0.93f, 0.91f, 0.86f, 0.95f);
        static readonly Color Dim = new(0.72f, 0.70f, 0.66f, 0.85f);

        [MenuItem("Granny/Build Menus", priority = 25)]
        public static void Run()
        {
            BuildTitleScreen();
            BuildPausePanel();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Menus] done");
        }

        // ------------------------------------------------------------------
        // Title screen
        // ------------------------------------------------------------------

        static void BuildTitleScreen()
        {
            var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name is "Menu" or "EventSystem")
                    Object.DestroyImmediate(root);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = Canvas("Menu", out var root2);

            Backdrop(root2.transform);

            Label("Title", root2.transform, font, new Vector2(0.5f, 1f), new Vector2(0f, -180f),
                new Vector2(900f, 90f), TextAnchor.MiddleCenter, 62, Ink).text = GameIdentity.Title;

            Label("Tagline", root2.transform, font, new Vector2(0.5f, 1f), new Vector2(0f, -250f),
                new Vector2(900f, 40f), TextAnchor.MiddleCenter, 20, Dim).text = GameIdentity.Tagline;

            var status = Label("Status", root2.transform, font, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f), new Vector2(900f, 34f), TextAnchor.MiddleCenter, 19, Dim);

            var continueButton = Button("Continue", root2.transform, font, new Vector2(0f, 60f), "Continue");
            var newRunButton = Button("NewRun", root2.transform, font, new Vector2(0f, 0f), "New run");
            var difficultyButton = Button("Difficulty", root2.transform, font, new Vector2(0f, -60f), "Difficulty   Normal");
            var quitButton = Button("Quit", root2.transform, font, new Vector2(0f, -120f), "Quit");

            var (confirmPanel, confirmYes, confirmNo) = ConfirmDialog(root2.transform, font);

            var controller = root2.AddComponent<MainMenuController>();
            BuildKit.Wire(controller, "continueButton", continueButton);
            BuildKit.Wire(controller, "newRunButton", newRunButton);
            BuildKit.Wire(controller, "quitButton", quitButton);
            BuildKit.Wire(controller, "difficultyButton", difficultyButton);
            BuildKit.Wire(controller, "difficultyLabel", difficultyButton.GetComponentInChildren<Text>());
            BuildKit.Wire(controller, "confirmPanel", confirmPanel);
            BuildKit.Wire(controller, "confirmButton", confirmYes);
            BuildKit.Wire(controller, "cancelButton", confirmNo);
            BuildKit.Wire(controller, "statusLabel", status);

            EventSystem(scene);

            _ = canvas;
            BuildKit.SaveScene(scene, "Menus");
            Debug.Log("[Menus] title screen built");
        }

        static (CanvasGroup panel, Button yes, Button no) ConfirmDialog(Transform parent, Font font)
        {
            var container = new GameObject("Confirm", typeof(RectTransform), typeof(CanvasGroup));
            container.transform.SetParent(parent, false);

            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var group = container.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var shade = Image("Shade", container.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            shade.color = new Color(0f, 0f, 0f, 0.75f);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = Vector2.zero;
            shade.rectTransform.offsetMax = Vector2.zero;

            Label("Question", container.transform, font, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                new Vector2(900f, 60f), TextAnchor.MiddleCenter, 24, Ink).text =
                "진행 중인 기록이 사라집니다. 새로 시작할까요?";

            var yes = Button("Yes", container.transform, font, new Vector2(-110f, -20f), "Start over");
            var no = Button("No", container.transform, font, new Vector2(110f, -20f), "Keep it");

            return (group, yes, no);
        }

        // ------------------------------------------------------------------
        // Pause panel
        // ------------------------------------------------------------------

        /// <summary>
        /// The pause panel is added to the existing HUD prefab rather than being
        /// its own canvas, so there is one overlay to reason about at runtime.
        /// </summary>
        static void BuildPausePanel()
        {
            var hudPath = $"{PrefabDir}/HUD.prefab";
            var hudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(hudPath);

            if (hudAsset == null)
            {
                Debug.LogError($"[Menus] No HUD prefab at {hudPath}. Run Granny > Build Content first.");
                return;
            }

            var hud = PrefabUtility.LoadPrefabContents(hudPath);

            var existing = hud.transform.Find("Pause");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var container = new GameObject("Pause", typeof(RectTransform), typeof(CanvasGroup));
            container.transform.SetParent(hud.transform, false);

            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var group = container.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var shade = Image("Shade", container.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            shade.color = new Color(0f, 0f, 0f, 0.8f);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = Vector2.zero;
            shade.rectTransform.offsetMax = Vector2.zero;

            Label("Heading", container.transform, font, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f),
                new Vector2(600f, 60f), TextAnchor.MiddleCenter, 40, Ink).text = "Paused";

            var sensitivityLabel = Label("SensitivityLabel", container.transform, font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 30f),
                TextAnchor.MiddleCenter, 18, Dim);

            var slider = Slider(container.transform, new Vector2(0f, 26f));

            var resume = Button("Resume", container.transform, font, new Vector2(0f, -40f), "Resume");
            var quit = Button("QuitToMenu", container.transform, font, new Vector2(0f, -100f), "Quit to menu");

            var pause = hud.AddComponent<PauseMenu>();
            BuildKit.Wire(pause, "panel", group);
            BuildKit.Wire(pause, "resumeButton", resume);
            BuildKit.Wire(pause, "quitButton", quit);
            BuildKit.Wire(pause, "sensitivitySlider", slider);
            BuildKit.Wire(pause, "sensitivityLabel", sensitivityLabel);

            PrefabUtility.SaveAsPrefabAsset(hud, hudPath);
            PrefabUtility.UnloadPrefabContents(hud);

            // A component added to a prefab asset propagates to every instance, so
            // the House scene picks the pause panel up on its own. This builder
            // deliberately never opens that scene: an earlier version did, hit a
            // transient load error, and saved an empty scene over the house.
            Debug.Log("[Menus] pause panel added to the HUD");
        }

        // ------------------------------------------------------------------
        // Widgets
        // ------------------------------------------------------------------

        static Canvas Canvas(string name, out GameObject root)
        {
            root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        static void Backdrop(Transform parent)
        {
            var image = Image("Backdrop", parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            image.color = new Color(0.04f, 0.035f, 0.04f, 1f);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
        }

        static Image Image(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            return go.GetComponent<Image>();
        }

        static Text Label(string name, Transform parent, Font font, Vector2 anchor, Vector2 offset,
            Vector2 size, TextAnchor alignment, int fontSize, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = colour;
            text.raycastTarget = false;
            text.text = string.Empty;

            return text;
        }

        static Button Button(string name, Transform parent, Font font, Vector2 offset, string caption)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(360f, 46f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.11f, 0.10f, 0.9f);

            var button = go.GetComponent<Button>();
            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1f, 0.88f, 0.6f, 1f);
            colours.pressedColor = new Color(0.8f, 0.68f, 0.42f, 1f);
            colours.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            button.colors = colours;

            var label = Label("Label", go.transform, font, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(340f, 40f), TextAnchor.MiddleCenter, 22, Ink);
            label.text = caption;

            return button;
        }

        static Slider Slider(Transform parent, Vector2 offset)
        {
            var go = new GameObject("Sensitivity", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(360f, 20f);

            var background = Image("Background", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(360f, 6f));
            background.color = new Color(0.25f, 0.24f, 0.22f, 0.9f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillRect = fillArea.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.offsetMin = new Vector2(0f, -3f);
            fillRect.offsetMax = new Vector2(0f, 3f);

            var fill = Image("Fill", fillArea.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 6f));
            fill.color = new Color(0.9f, 0.78f, 0.5f, 0.95f);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            var handle = Image("Handle", handleArea.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(16f, 20f));
            handle.color = new Color(0.95f, 0.92f, 0.86f, 1f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            return slider;
        }

        static void EventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            var events = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, scene);
        }
    }
}
