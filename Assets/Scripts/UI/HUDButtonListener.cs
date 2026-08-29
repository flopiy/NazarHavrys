using UnityEngine;
using UnityEngine.UI;
using FungalCurse.Systems;

namespace FungalCurse.UI
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — HUD Button Listener.
    /// 
    /// SINGLE RESPONSIBILITY: Automatically registers UI button clicks to the GameManager at runtime,
    /// avoiding complex and fragile Unity Editor serialization.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HUDButtonListener : MonoBehaviour
    {
        [Header("Buttons")]
        [Tooltip("The 'Next Level' button on the Victory Panel.")]
        [SerializeField] private Button nextLevelButton;

        [Tooltip("The 'Restart' button on the Defeat Panel.")]
        [SerializeField] private Button restartButton;

        private void Start()
        {
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }
        }

        private void OnNextLevelClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadNextLevel();
            }
            else
            {
                Debug.LogWarning("[HUDButtonListener] GameManager instance is missing; cannot load next level.");
            }
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
            else
            {
                Debug.LogWarning("[HUDButtonListener] GameManager instance is missing; cannot restart game.");
            }
        }
    }
}
