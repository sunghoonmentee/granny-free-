using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Granny.UI
{
    /// <summary>
    /// The pause screen.
    ///
    /// Pausing stops time completely, which is a real concession in a horror
    /// game — but the alternative, a menu that leaves you being hunted while you
    /// adjust the mouse sensitivity, is worse. There is deliberately no "save
    /// now": the run saves when a day starts, so quitting cannot undo a mistake.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLook look;

        [Header("Widgets")]
        [SerializeField] CanvasGroup panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button quitButton;
        [SerializeField] Slider sensitivitySlider;
        [SerializeField] Text sensitivityLabel;

        const string SensitivityKey = "granny.sensitivity";

        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>();
            if (look == null) look = FindAnyObjectByType<PlayerLook>();

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = 0.03f;
                sensitivitySlider.maxValue = 0.4f;
                sensitivitySlider.value = PlayerPrefs.GetFloat(SensitivityKey, 0.12f);
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
                OnSensitivityChanged(sensitivitySlider.value);
            }

            SetPanelVisible(false);
        }

        void OnEnable()
        {
            if (input != null) input.PauseRequested += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.PauseRequested -= Toggle;

            // Leaving the scene while paused would freeze the next one too.
            if (IsPaused) Time.timeScale = 1f;
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;

            input?.SetGameplayEnabled(false);
            PlayerLook.SetCursorLocked(false);
            SetPanelVisible(true);
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;

            input?.SetGameplayEnabled(true);
            PlayerLook.SetCursorLocked(true);
            SetPanelVisible(false);
        }

        void QuitToMenu()
        {
            Time.timeScale = 1f;
            PlayerLook.SetCursorLocked(false);
            SceneManager.LoadScene(SceneNames.MainMenu, LoadSceneMode.Single);
        }

        void OnSensitivityChanged(float value)
        {
            if (look != null) look.MouseSensitivity = value;

            if (sensitivityLabel != null)
                sensitivityLabel.text = $"Look sensitivity   {value:0.00}";

            PlayerPrefs.SetFloat(SensitivityKey, value);
        }

        void SetPanelVisible(bool visible)
        {
            if (panel == null) return;

            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }
    }
}
