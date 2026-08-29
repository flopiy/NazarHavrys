using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FungalCurse.UI
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Main Menu Upgrade Window.
    ///
    /// SINGLE RESPONSIBILITY: Manages PlayerPrefs meta-progression stats upgrades (Speed, Max Mana,
    /// Attack Damage, Teleport, and Jumps) using accumulated points, and updates the UI labels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuUpgradeWindow : MonoBehaviour
    {
        [Header("Points Balance")]
        [SerializeField] private TextMeshProUGUI pointsBalanceText;

        [Header("Max Health Upgrade (Reused for Speed)")]
        [SerializeField] private TextMeshProUGUI hpLevelText;
        [SerializeField] private TextMeshProUGUI hpEffectText;
        [SerializeField] private Button hpUpgradeButton;
        [SerializeField] private TextMeshProUGUI hpButtonText;

        [Header("Max Mana Upgrade")]
        [SerializeField] private TextMeshProUGUI manaLevelText;
        [SerializeField] private TextMeshProUGUI manaEffectText;
        [SerializeField] private Button manaUpgradeButton;
        [SerializeField] private TextMeshProUGUI manaButtonText;

        [Header("Attack Damage Upgrade")]
        [SerializeField] private TextMeshProUGUI damageLevelText;
        [SerializeField] private TextMeshProUGUI damageEffectText;
        [SerializeField] private Button damageUpgradeButton;
        [SerializeField] private TextMeshProUGUI damageButtonText;

        [Header("Teleport Upgrade")]
        [SerializeField] private TextMeshProUGUI teleportLevelText;
        [SerializeField] private TextMeshProUGUI teleportEffectText;
        [SerializeField] private Button teleportUpgradeButton;
        [SerializeField] private TextMeshProUGUI teleportButtonText;

        [Header("Jumps Upgrade")]
        [SerializeField] private TextMeshProUGUI jumpsLevelText;
        [SerializeField] private TextMeshProUGUI jumpsEffectText;
        [SerializeField] private Button jumpsUpgradeButton;
        [SerializeField] private TextMeshProUGUI jumpsButtonText;

        [Header("Window Navigation")]
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            RefreshUI();

            // Wire action events
            if (hpUpgradeButton != null) hpUpgradeButton.onClick.AddListener(UpgradeSpeed);
            if (manaUpgradeButton != null) manaUpgradeButton.onClick.AddListener(UpgradeMaxMana);
            if (damageUpgradeButton != null) damageUpgradeButton.onClick.AddListener(UpgradeAttackDamage);
            if (teleportUpgradeButton != null) teleportUpgradeButton.onClick.AddListener(UpgradeTeleport);
            if (jumpsUpgradeButton != null) jumpsUpgradeButton.onClick.AddListener(UpgradeJumps);
            if (closeButton != null) closeButton.onClick.AddListener(CloseWindow);
        }

        private void OnDisable()
        {
            if (hpUpgradeButton != null) hpUpgradeButton.onClick.RemoveListener(UpgradeSpeed);
            if (manaUpgradeButton != null) manaUpgradeButton.onClick.RemoveListener(UpgradeMaxMana);
            if (damageUpgradeButton != null) damageUpgradeButton.onClick.RemoveListener(UpgradeAttackDamage);
            if (teleportUpgradeButton != null) teleportUpgradeButton.onClick.RemoveListener(UpgradeTeleport);
            if (jumpsUpgradeButton != null) jumpsUpgradeButton.onClick.RemoveListener(UpgradeJumps);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseWindow);
        }

        public void RefreshUI()
        {
            int points = PlayerPrefs.GetInt("UpgradePoints", 0);
            if (pointsBalanceText != null)
            {
                pointsBalanceText.text = $"Очки: {points}";
            }

            // --- 1. Movement Speed ---
            int speedLvl = PlayerPrefs.GetInt("Upgrade_Speed_Level", 0);
            if (hpLevelText != null) hpLevelText.text = $"Рівень: {speedLvl} / 5";
            if (hpEffectText != null) hpEffectText.text = $"+{(speedLvl * 0.2f):F1} Шв. (Наступна: +{((speedLvl + 1) * 0.2f):F1} Шв.)";
            ConfigureUpgradeButton(hpUpgradeButton, hpButtonText, speedLvl, points);

            // --- 2. Max Mana ---
            int manaLvl = PlayerPrefs.GetInt("Upgrade_MaxMana_Level", 0);
            if (manaLevelText != null) manaLevelText.text = $"Рівень: {manaLvl} / 5";
            if (manaEffectText != null) manaEffectText.text = $"+{manaLvl * 10} Мана (Наступна: +{(manaLvl + 1) * 10} Мана)";
            ConfigureUpgradeButton(manaUpgradeButton, manaButtonText, manaLvl, points);

            // --- 3. Attack Damage / Strength ---
            int dmgLvl = PlayerPrefs.GetInt("Upgrade_AttackDamage_Level", 0);
            if (damageLevelText != null) damageLevelText.text = $"Рівень: {dmgLvl} / 5";
            if (damageEffectText != null) damageEffectText.text = $"Сила: +{dmgLvl * 1} Стр. / +{dmgLvl * 1} Шип (Наступна: +{(dmgLvl + 1) * 1} / +{(dmgLvl + 1) * 1})";
            ConfigureUpgradeButton(damageUpgradeButton, damageButtonText, dmgLvl, points);

            // --- 4. Teleport Distance ---
            int teleportLvl = PlayerPrefs.GetInt("Upgrade_Blink_Level", 0);
            if (teleportLevelText != null) teleportLevelText.text = $"Рівень: {teleportLvl} / 5";
            if (teleportEffectText != null) teleportEffectText.text = $"+{(teleportLvl * 0.3f):F1} Дист. (Наступна: +{((teleportLvl + 1) * 0.3f):F1} Дист.)";
            ConfigureUpgradeButton(teleportUpgradeButton, teleportButtonText, teleportLvl, points);

            // --- 5. Jump Height ---
            int jumpsLvl = PlayerPrefs.GetInt("Upgrade_Jump_Level", 0);
            if (jumpsLevelText != null) jumpsLevelText.text = $"Рівень: {jumpsLvl} / 5";
            if (jumpsEffectText != null) jumpsEffectText.text = $"+{(jumpsLvl * 0.3f):F1} Вис. (Наступна: +{((jumpsLvl + 1) * 0.3f):F1} Вис.)";
            ConfigureUpgradeButton(jumpsUpgradeButton, jumpsButtonText, jumpsLvl, points);
        }

        private void ConfigureUpgradeButton(Button button, TextMeshProUGUI btnText, int currentLevel, int currentPoints)
        {
            if (button == null) return;

            if (currentLevel >= 5)
            {
                button.interactable = false;
                if (btnText != null)
                {
                    btnText.text = "МАКС.";
                    btnText.color = new Color(0.4f, 0.45f, 0.5f);
                }
            }
            else
            {
                int cost = GetUpgradeCost(currentLevel);
                bool canAfford = currentPoints >= cost;
                button.interactable = canAfford;

                if (btnText != null)
                {
                    btnText.text = $"{cost} ОЧОК";
                    btnText.color = canAfford ? new Color(0.65f, 0.99f, 0.96f) : new Color(0.9f, 0.15f, 0.15f);
                }
            }
        }

        private int GetUpgradeCost(int currentLevel)
        {
            // Linear cost scaling: Level 0 = 100, Level 1 = 200, Level 2 = 300, etc.
            return (currentLevel + 1) * 100;
        }

        private void UpgradeSpeed()
        {
            TryUpgrade("Upgrade_Speed_Level");
        }

        private void UpgradeMaxMana()
        {
            TryUpgrade("Upgrade_MaxMana_Level");
        }

        private void UpgradeAttackDamage()
        {
            TryUpgrade("Upgrade_AttackDamage_Level");
        }

        private void UpgradeTeleport()
        {
            TryUpgrade("Upgrade_Blink_Level");
        }

        private void UpgradeJumps()
        {
            TryUpgrade("Upgrade_Jump_Level");
        }

        private void TryUpgrade(string prefKey)
        {
            int lvl = PlayerPrefs.GetInt(prefKey, 0);
            if (lvl >= 5) return;

            int cost = GetUpgradeCost(lvl);
            int points = PlayerPrefs.GetInt("UpgradePoints", 0);

            if (points >= cost)
            {
                PlayerPrefs.SetInt("UpgradePoints", points - cost);
                PlayerPrefs.SetInt(prefKey, lvl + 1);
                PlayerPrefs.Save();
                RefreshUI();
                Debug.Log($"[MainMenuUpgradeWindow] Upgraded {prefKey} to level {lvl + 1}. Spent {cost} points.");
            }
        }

        private void CloseWindow()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
            {
                gameObject.SetActive(false);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
    }
}