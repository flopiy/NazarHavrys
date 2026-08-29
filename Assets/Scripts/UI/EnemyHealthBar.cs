using UnityEngine;
using UnityEngine.UI;
using FungalCurse.Enemies;

namespace FungalCurse.UI
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — World Space Enemy Health Bar.
    ///
    /// SINGLE RESPONSIBILITY: Updates a small overhead world-space health slider based on
    /// the parent's <see cref="EliteEnemyAI"/> current health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The parent enemy's combat component. If empty, searches the parent hierarchy on Start.")]
        [SerializeField] private EliteEnemyAI enemyAI;
        
        [Tooltip("The UI Slider representing the health bar.")]
        [SerializeField] private Slider healthSlider;

        [Header("Visibility Options")]
        [Tooltip("If true, the health bar is hidden when at maximum health (cleaner screen).")]
        [SerializeField] private bool hideAtFullHealth = false;

        private void Start()
        {
            if (enemyAI == null)
            {
                enemyAI = GetComponentInParent<EliteEnemyAI>();
            }

            if (healthSlider == null)
            {
                healthSlider = GetComponentInChildren<Slider>();
            }

            if (enemyAI == null || healthSlider == null)
            {
                Debug.LogWarning($"[EnemyHealthBar] Missing dependencies on {gameObject.name}. Disabling bar.", this);
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (enemyAI == null || healthSlider == null) return;

            int cur = enemyAI.CurrentHealth;
            int max = enemyAI.MaxHealth;

            // Handle the bar state when dead (so it doesn't float briefly after death)
            if (cur <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            // Optional cleanup for full-health units
            if (hideAtFullHealth && cur >= max)
            {
                if (healthSlider.gameObject.activeSelf)
                {
                    healthSlider.gameObject.SetActive(false);
                }
                return;
            }

            if (!healthSlider.gameObject.activeSelf)
            {
                healthSlider.gameObject.SetActive(true);
            }

            healthSlider.maxValue = max;
            healthSlider.value = cur;
        }
    }
}