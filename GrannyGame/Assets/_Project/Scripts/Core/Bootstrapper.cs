using UnityEngine;
using UnityEngine.SceneManagement;

namespace Granny.Core
{
    /// <summary>
    /// Sole occupant of the Boot scene. Boot exists so that the game always starts
    /// from a known, near-empty state — no matter which scene a developer happened
    /// to have open — and so global systems have one obvious place to be created.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Tooltip("Scene loaded once boot-time setup is finished.")]
        [SerializeField] string firstScene = SceneNames.MainMenu;

        [Tooltip("Frames to wait before loading, so the first frame is not also the heaviest.")]
        [SerializeField, Min(0)] int warmupFrames = 1;

        void Awake()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            // Nothing else runs yet; global services are registered here as later
            // phases add them (save system, audio bank, difficulty profile).
        }

        void Start()
        {
            StartCoroutine(LoadFirstScene());
        }

        System.Collections.IEnumerator LoadFirstScene()
        {
            for (var i = 0; i < warmupFrames; i++)
                yield return null;

            if (Application.CanStreamedLevelBeLoaded(firstScene))
            {
                SceneManager.LoadScene(firstScene, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError(
                    $"[Bootstrapper] '{firstScene}' is not in Build Settings. " +
                    "Run Granny > Bootstrap Project, or add the scene manually.");
            }
        }
    }
}
