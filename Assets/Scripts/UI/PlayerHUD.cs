using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FungalCurse.Player;

namespace FungalCurse.UI
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Player HUD Controller.
    ///
    /// SINGLE RESPONSIBILITY: Reads stats from <see cref="WizardStatsAndCombat"/>
    /// and updates the UI sliders and text readouts. Contains no gameplay or stat calculation logic.
    /// Uses polling in Update to avoid event hook/unhook mismatches during scene transitions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHUD : MonoBehaviour
    {
        [Header("Player Reference")]
        [Tooltip("The wizard stats component. If left empty, will attempt to find by tag 'Player' at Start.")]
        [SerializeField] private WizardStatsAndCombat playerStats;

        [Header("HP Bar UI")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Mana Bar UI")]
        [SerializeField] private Slider manaSlider;
        [SerializeField] private TextMeshProUGUI manaText;

        [Header("Stamina Bar UI")]
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private TextMeshProUGUI staminaText;

        [Header("Lives UI")]
        [SerializeField] private TextMeshProUGUI livesText;

        private void Start()
        {
            if (playerStats == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    playerStats = playerObj.GetComponent<WizardStatsAndCombat>();
                }
            }

            if (playerStats == null)
            {
                Debug.LogWarning("[PlayerHUD] WizardStatsAndCombat reference is missing and could not find by tag 'Player'.", this);
            }
        }

        private void Update()
        {
            if (playerStats == null) return;

            UpdateHP();
            UpdateMana();
            UpdateStamina();
            UpdateLives();
        }

        private void UpdateHP()
        {
            if (hpSlider == null) return;

            int cur = playerStats.CurrentHealth;
            int max = playerStats.MaxHealth;

            hpSlider.maxValue = max;
            hpSlider.value = cur;

            if (hpText != null)
            {
                hpText.text = $"HP: {cur} / {max}";
            }
        }

        private void UpdateMana()
        {
            if (manaSlider == null) return;

            float cur = playerStats.CurrentMana;
            float max = playerStats.MaxMana;

            manaSlider.maxValue = max;
            manaSlider.value = cur;

            if (manaText != null)
            {
                // Format with no decimal places for clean layout
                manaText.text = $"Mana: {Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(max)}";
            }
        }

        private void UpdateStamina()
        {
            if (staminaSlider == null) return;

            float cur = playerStats.CurrentStamina;
            float max = playerStats.MaxStamina;

            staminaSlider.maxValue = max;
            staminaSlider.value = cur;

            if (staminaText != null)
            {
                staminaText.text = $"Stamina: {Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(max)}";
            }
        }

        private void UpdateLives()
        {
            if (livesText == null) return;

            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                int currentLives = FungalCurse.Systems.GameManager.Instance.CurrentLives;
                livesText.text = $"Lives: {currentLives}";
            }
            else
            {
                livesText.text = "Lives: 3"; // fallback in editor/test if no GM is present
            }
        }
    }
}