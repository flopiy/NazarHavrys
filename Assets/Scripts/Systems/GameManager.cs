using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FungalCurse.Systems
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Central Game Manager.
    /// 
    /// SINGLE RESPONSIBILITY: Manages high-level game rules, including player lives (3 lives),
    /// Level Completion (Victory), and Game Over (Defeat) states. It persists across scene loads
    /// to keep track of remaining lives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameManager (Auto-Spawned)");
                go.AddComponent<GameManager>();
            }
        }

        [Header("Lives Configuration")]
        [SerializeField] private int maxLives = 3;
        private int _currentLives;

        [Header("UI Overlays")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        // Decoupled events for UI updates
        public event Action<int> OnLivesChanged;
        public event Action OnVictory;
        public event Action OnDefeat;

        public int CurrentLives => _currentLives;
        public int MaxLives => maxLives;

        private string _pendingNextSceneName;
        private bool _isLevelComplete;

        /// <summary>
        /// Unlocks the specified level index (1-based).
        /// </summary>
        public static void UnlockLevel(int levelIndex)
        {
            int highestUnlocked = PlayerPrefs.GetInt("HighestUnlockedLevel", 1);
            if (levelIndex > highestUnlocked)
            {
                PlayerPrefs.SetInt("HighestUnlockedLevel", levelIndex);
                PlayerPrefs.Save();
                Debug.Log($"[GameManager] Level {levelIndex} unlocked! Highest unlocked: {levelIndex}");
            }
        }

        /// <summary>
        /// Gets the highest unlocked level index (1-based).
        /// </summary>
        public static int GetHighestUnlockedLevel()
        {
            return PlayerPrefs.GetInt("HighestUnlockedLevel", 1);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResetGameSession();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Find panels in the newly loaded scene
            FindUIOverlays();
            _isLevelComplete = false;
            _pendingNextSceneName = null;

            // Notify UI about current lives count
            OnLivesChanged?.Invoke(_currentLives);
        }

        private void FindUIOverlays()
        {
            victoryPanel = null;
            defeatPanel = null;

            var canvas = GameObject.Find("PlayerHUDCanvas");
            if (canvas != null)
            {
                var victoryTransform = canvas.transform.Find("VictoryPanel");
                if (victoryTransform != null)
                {
                    victoryPanel = victoryTransform.gameObject;
                    victoryPanel.SetActive(false);
                }

                var defeatTransform = canvas.transform.Find("DefeatPanel");
                if (defeatTransform != null)
                {
                    defeatPanel = defeatTransform.gameObject;
                    defeatPanel.SetActive(false);
                }
            }

            // Fallback: search all root objects and their descendants recursively
            if (victoryPanel == null || defeatPanel == null)
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var r in roots)
                {
                    if (victoryPanel == null)
                    {
                        var t = FindDescendant(r.transform, "VictoryPanel");
                        if (t != null)
                        {
                            victoryPanel = t.gameObject;
                            victoryPanel.SetActive(false);
                        }
                    }
                    if (defeatPanel == null)
                    {
                        var t = FindDescendant(r.transform, "DefeatPanel");
                        if (t != null)
                        {
                            defeatPanel = t.gameObject;
                            defeatPanel.SetActive(false);
                        }
                    }
                }
            }
        }

        private Transform FindDescendant(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var found = FindDescendant(child, name);
                if (found != null) return found;
            }
            return null;
        }

        public void ResetGameSession()
        {
            _currentLives = maxLives;
            _isLevelComplete = false;
            _pendingNextSceneName = null;
            OnLivesChanged?.Invoke(_currentLives);
        }

        /// <summary>
        /// Called when the player dies. Reduces lives and either reloads the level or triggers Game Over.
        /// </summary>
        public void OnPlayerDeath()
        {
            if (_isLevelComplete) return;

            _currentLives--;
            OnLivesChanged?.Invoke(_currentLives);
            Debug.Log($"[GameManager] Player lost a life. Remaining lives: {_currentLives}");

            if (_currentLives > 0)
            {
                StartCoroutine(ReloadLevelCoroutine(2.2f));
            }
            else
            {
                TriggerDefeat();
            }
        }

        private IEnumerator ReloadLevelCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        /// <summary>
        /// Displays the Defeat panel when player lives run out.
        /// </summary>
        private void TriggerDefeat()
        {
            if (defeatPanel != null)
            {
                defeatPanel.SetActive(true);
            }
            OnDefeat?.Invoke();
            Debug.Log("[GameManager] Player has run out of lives! Game Over.");
        }

        /// <summary>
        /// Called when the player reaches the level exit teleport. Displays the Victory panel.
        /// </summary>
        public void OnLevelComplete(string nextSceneName)
        {
            if (_isLevelComplete) return;
            _isLevelComplete = true;
            _pendingNextSceneName = nextSceneName;

            // Automatically unlock the next level based on the current scene name
            string activeScene = SceneManager.GetActiveScene().name;
            if (activeScene.Contains("Level1")) UnlockLevel(2);
            else if (activeScene.Contains("Level2")) UnlockLevel(3);
            else if (activeScene.Contains("Level3")) UnlockLevel(4);

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
            OnVictory?.Invoke();
            Debug.Log("[GameManager] Level complete! Victory banner displayed.");
        }

        /// <summary>
        /// Wired to the 'Next Level' button on the Victory Panel.
        /// </summary>
        public void LoadNextLevel()
        {
            if (!string.IsNullOrEmpty(_pendingNextSceneName))
            {
                SceneManager.LoadScene(_pendingNextSceneName);
            }
            else
            {
                Debug.LogWarning("[GameManager] No pending next scene name configured.");
            }
        }

        /// <summary>
        /// Wired to the 'Restart' button on the Defeat Panel.
        /// </summary>
        public void RestartGame()
        {
            ResetGameSession();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }
    }
}
