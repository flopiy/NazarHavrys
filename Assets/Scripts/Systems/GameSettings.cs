using System;
using UnityEngine;

namespace FungalCurse.Systems
{
    /// <summary>
    /// Global manager for game settings (Volume, Fullscreen, Graphics Quality) and progress reset.
    /// </summary>
    public static class GameSettings
    {
        public static event Action OnProgressReset;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("Settings_MasterVolume", 1f);
            set
            {
                float clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("Settings_MasterVolume", clamped);
                AudioListener.volume = clamped;
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt("Settings_Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt("Settings_Fullscreen", value ? 1 : 0);
                Screen.fullScreen = value;
            }
        }

        public static int QualityLevel
        {
            get => PlayerPrefs.GetInt("Settings_QualityLevel", QualitySettings.GetQualityLevel());
            set
            {
                int max = QualitySettings.names.Length - 1;
                int clamped = Mathf.Clamp(value, 0, max);
                PlayerPrefs.SetInt("Settings_QualityLevel", clamped);
                QualitySettings.SetQualityLevel(clamped, true);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeSettings()
        {
            AudioListener.volume = MasterVolume;
            if (PlayerPrefs.HasKey("Settings_Fullscreen"))
            {
                Screen.fullScreen = Fullscreen;
            }
            if (PlayerPrefs.HasKey("Settings_QualityLevel"))
            {
                QualitySettings.SetQualityLevel(QualityLevel, true);
            }
        }

        /// <summary>
        /// Resets all player progression, unlocked levels, stats upgrades and score points.
        /// </summary>
        public static void ResetAllProgress()
        {
            PlayerPrefs.SetInt("HighestUnlockedLevel", 1);
            PlayerPrefs.SetInt("UpgradePoints", 0);
            PlayerPrefs.SetInt("Upgrade_Speed_Level", 0);
            PlayerPrefs.SetInt("Upgrade_MaxMana_Level", 0);
            PlayerPrefs.SetInt("Upgrade_AttackDamage_Level", 0);
            PlayerPrefs.SetInt("Upgrade_Blink_Level", 0);
            PlayerPrefs.SetInt("Upgrade_Jump_Level", 0);
            PlayerPrefs.Save();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResetGameSession();
            }

            OnProgressReset?.Invoke();
            Debug.Log("[GameSettings] All player progress and upgrades have been reset to default!");
        }
    }
}
