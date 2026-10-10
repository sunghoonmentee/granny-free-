using Granny.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Granny.UI
{
    /// <summary>
    /// The title screen.
    ///
    /// Continue is offered only when a run actually exists, and starting a new
    /// one asks first — losing four days of progress to a misclick would be a
    /// worse fright than anything in the house.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button continueButton;
        [SerializeField] Button newRunButton;
        [SerializeField] Button quitButton;

        [Header("Modes")]
        [Tooltip("The house with the lights almost out.")]
        [SerializeField] Button darkButton;
        [SerializeField] Text darkLabel;

        [Tooltip("Every fastening on the front door, whatever the difficulty.")]
        [SerializeField] Button locksButton;
        [SerializeField] Text locksLabel;

        [Header("Difficulty")]
        [SerializeField] Button difficultyButton;
        [SerializeField] Text difficultyLabel;
        [SerializeField] string[] difficultyNames = { "Practice", "Easy", "Normal", "Hard", "Extreme" };

        [Header("Confirmation")]
        [SerializeField] CanvasGroup confirmPanel;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;

        [Header("Status")]
        [SerializeField] Text statusLabel;

        const string DifficultyKey = "granny.difficulty";

        int difficultyIndex;

        void Awake()
        {
            PlayerLook_SetCursorVisible();

            difficultyIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(DifficultyKey, 2), 0, Mathf.Max(0, difficultyNames.Length - 1));

            if (continueButton != null) continueButton.onClick.AddListener(ContinueRun);
            if (newRunButton != null) newRunButton.onClick.AddListener(RequestNewRun);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(CycleDifficulty);

            if (darkButton != null)
                darkButton.onClick.AddListener(() => { GameModes.Dark = !GameModes.Dark; Refresh(); });

            if (locksButton != null)
                locksButton.onClick.AddListener(() =>
                {
                    GameModes.ExtraLocks = !GameModes.ExtraLocks;
                    Refresh();
                });
            if (confirmButton != null) confirmButton.onClick.AddListener(StartNewRun);
            if (cancelButton != null) cancelButton.onClick.AddListener(() => ShowConfirm(false));

            Refresh();
            ShowConfirm(false);
        }

        void Refresh()
        {
            var save = SaveSystem.Load();
            var hasRun = save != null;

            if (continueButton != null) continueButton.interactable = hasRun;

            if (statusLabel != null)
                statusLabel.text = hasRun
                    ? $"Day {save.day} of an unfinished run — {save.difficultyName}"
                    : "No run in progress";

            if (difficultyLabel != null && difficultyNames.Length > 0)
                difficultyLabel.text = $"Difficulty   {difficultyNames[difficultyIndex]}";

            // Spelled out rather than ticked, because a tick in a menu this
            // sparse is easy to miss and both of these change the whole night.
            if (darkLabel != null)
                darkLabel.text = GameModes.Dark ? "Dark house   ON" : "Dark house   off";

            if (locksLabel != null)
                locksLabel.text = GameModes.ExtraLocks ? "Extra locks   ON" : "Extra locks   off";
        }

        void CycleDifficulty()
        {
            if (difficultyNames.Length == 0) return;

            difficultyIndex = (difficultyIndex + 1) % difficultyNames.Length;
            PlayerPrefs.SetInt(DifficultyKey, difficultyIndex);
            Refresh();
        }

        void ContinueRun() => SceneManager.LoadScene(SceneNames.House, LoadSceneMode.Single);

        void RequestNewRun()
        {
            // Only worth confirming when there is something to lose.
            if (SaveSystem.HasSave) ShowConfirm(true);
            else StartNewRun();
        }

        void StartNewRun()
        {
            SaveSystem.Delete();
            SceneManager.LoadScene(SceneNames.House, LoadSceneMode.Single);
        }

        void ShowConfirm(bool visible)
        {
            if (confirmPanel == null) return;

            confirmPanel.alpha = visible ? 1f : 0f;
            confirmPanel.interactable = visible;
            confirmPanel.blocksRaycasts = visible;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void PlayerLook_SetCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
