using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace FungalCurse.UI
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Level Selection Menu Controller.
    ///
    /// SINGLE RESPONSIBILITY: Evaluates unlocked levels at start, updates level select buttons'
    /// interactability and text labels, and handles scene routing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSelectController : MonoBehaviour
    {
        [Header("Level Buttons")]
        [SerializeField] private Button level1Button;
        [SerializeField] private Button level2Button;
        [SerializeField] private Button level3Button;
        [SerializeField] private Button level4Button;
        [SerializeField] private Button backButton;

        [Header("Button Text References (TMP)")]
        [SerializeField] private TextMeshProUGUI level1Text;
        [SerializeField] private TextMeshProUGUI level2Text;
        [SerializeField] private TextMeshProUGUI level3Text;
        [SerializeField] private TextMeshProUGUI level4Text;

        [Header("Gothic Button Styling Colors")]
        [SerializeField] private Color unlockedColor = new Color(0.65f, 0.99f, 0.96f); // A6FDF5 (Light cyan)
        [SerializeField] private Color lockedColor = new Color(0.4f, 0.45f, 0.5f);     // Greyed out slate

        private void Start()
        {
            // Reset player lives session when returning to selection screens
            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                FungalCurse.Systems.GameManager.Instance.ResetGameSession();
            }

            ConfigureButtons();

            // Wire action events
            if (level1Button != null)
                level1Button.onClick.AddListener(() => LoadLevel("MidnightBramble_Level1"));

            if (level2Button != null)
                level2Button.onClick.AddListener(() => LoadLevel("MidnightBramble_Level2"));

            if (level3Button != null)
                level3Button.onClick.AddListener(() => LoadLevel("MidnightBramble_Level3"));

            if (level4Button != null)
                level4Button.onClick.AddListener(() => LoadLevel("MidnightBramble_Level4"));

            if (backButton != null)
                backButton.onClick.AddListener(GoBack);
        }

        private void OnEnable()
        {
            FungalCurse.Systems.GameSettings.OnProgressReset += ConfigureButtons;
        }

        private void OnDisable()
        {
            FungalCurse.Systems.GameSettings.OnProgressReset -= ConfigureButtons;
        }

        private void ConfigureButtons()
        {
            int highestUnlocked = 1;
            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                highestUnlocked = FungalCurse.Systems.GameManager.GetHighestUnlockedLevel();
            }
            else
            {
                highestUnlocked = PlayerPrefs.GetInt("HighestUnlockedLevel", 1);
            }

            Debug.Log($"[LevelSelectController] Configuring buttons. Highest unlocked level: {highestUnlocked}");

            // Level 1: Always unlocked
            ConfigureButtonState(level1Button, level1Text, true, "[ РІВЕНЬ 1 ]");

            // Level 2: Unlocked if highestUnlocked >= 2
            ConfigureButtonState(level2Button, level2Text, highestUnlocked >= 2, "[ РІВЕНЬ 2 ]");

            // Level 3: Unlocked if highestUnlocked >= 3
            ConfigureButtonState(level3Button, level3Text, highestUnlocked >= 3, "[ РІВЕНЬ 3 ]");

            // Level 4: Unlocked if highestUnlocked >= 4
            ConfigureButtonState(level4Button, level4Text, highestUnlocked >= 4, "[ РІВЕНЬ 4 (БОС) ]");
        }

        private void ConfigureButtonState(Button button, TextMeshProUGUI text, bool isUnlocked, string levelName)
        {
            if (button == null) return;

            button.interactable = isUnlocked;

            if (text != null)
            {
                if (isUnlocked)
                {
                    text.text = levelName;
                    text.color = unlockedColor;
                }
                else
                {
                    text.text = "[ ЗАБЛОКОВАНО ]";
                    text.color = lockedColor;
                }
            }

            // Outline visual feedback for locked vs unlocked
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = isUnlocked ? new Color(0.0f, 0.9f, 1.0f, 1.0f) : new Color(0.2f, 0.22f, 0.27f, 0.8f);
            }
        }

        private void LoadLevel(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            Debug.Log($"[LevelSelectController] Routing to scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        private void GoBack()
        {
            Debug.Log("[LevelSelectController] Routing back to MainMenu.");
            SceneManager.LoadScene("MainMenu");
        }
    }
}
