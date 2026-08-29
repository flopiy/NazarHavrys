using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FungalCurse.UI
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Main Menu Controller.
    ///
    /// SINGLE RESPONSIBILITY: Handles UI button actions on the Main Menu page,
    /// loading the first level or routing to the separate level selection scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Configurations")]
        [Tooltip("The name of the first gameplay scene of the campaign.")]
        [SerializeField] private string firstLevelSceneName = "MidnightBramble_Level1";

        [Tooltip("The name of the level selection scene.")]
        [SerializeField] private string levelSelectSceneName = "LevelSelect";

        [Header("Menu Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button upgradesButton;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            // Reset GameManager lives whenever we return to the Main Menu
            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                FungalCurse.Systems.GameManager.Instance.ResetGameSession();
            }

            // Wire action events
            if (startButton != null)
                startButton.onClick.AddListener(PlayGame);

            if (upgradesButton != null)
                upgradesButton.onClick.AddListener(OpenUpgrades);

            if (levelSelectButton != null)
                levelSelectButton.onClick.AddListener(OpenLevelSelect);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        private void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }

        private void PlayGame()
        {
            LoadLevel(firstLevelSceneName);
        }

        private void OpenUpgrades()
        {
            LoadLevel("Upgrades");
        }

        private void OpenLevelSelect()
        {
            LoadLevel(levelSelectSceneName);
        }

        private void QuitGame()
        {
            Debug.Log("[MainMenuController] Quitting game.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void LoadLevel(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[MainMenuController] Scene name is empty!");
                return;
            }

            Debug.Log($"[MainMenuController] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
}
