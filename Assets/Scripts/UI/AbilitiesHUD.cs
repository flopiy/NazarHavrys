using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FungalCurse.Player;

namespace FungalCurse.UI
{
    /// <summary>
    /// HUD controller for displaying and interacting with character abilities in the bottom-right corner.
    /// Handles icons, cooldown overlays, hotkey labels, and click/hotkey triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilitiesHUD : MonoBehaviour
    {
        [System.Serializable]
        public struct AbilitySlotUI
        {
            public Button button;
            public UnityEngine.UI.Image iconImage;
            public UnityEngine.UI.Image cooldownOverlay;
            public TextMeshProUGUI cooldownText;
            public TextMeshProUGUI keyText;
            public TextMeshProUGUI costText;
        }

        [Header("Player Reference")]
        [SerializeField] private WizardStatsAndCombat playerStats;

        [Header("Ability Slots")]
        [SerializeField] private AbilitySlotUI arrowSlot;
        [SerializeField] private AbilitySlotUI spikeSlot;
        [SerializeField] private AbilitySlotUI shieldSlot;
        [SerializeField] private AbilitySlotUI blinkSlot;

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

            if (arrowSlot.button != null)
                arrowSlot.button.onClick.AddListener(OnArrowClicked);

            if (spikeSlot.button != null)
                spikeSlot.button.onClick.AddListener(OnSpikeClicked);

            if (shieldSlot.button != null)
                shieldSlot.button.onClick.AddListener(OnShieldClicked);

            if (blinkSlot.button != null)
                blinkSlot.button.onClick.AddListener(OnBlinkClicked);
        }

        private void OnArrowClicked()
        {
            if (playerStats != null) playerStats.CastArrow();
        }

        private void OnSpikeClicked()
        {
            if (playerStats != null) playerStats.CastSpikeStorm();
        }

        private void OnShieldClicked()
        {
            if (playerStats != null) playerStats.RaiseShield();
        }

        private void OnBlinkClicked()
        {
            if (playerStats != null) playerStats.Blink();
        }

        private void Update()
        {
            if (playerStats == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    playerStats = playerObj.GetComponent<WizardStatsAndCombat>();
                }
                if (playerStats == null) return;
            }

            UpdateArrowSlot();
            UpdateSpikeSlot();
            UpdateShieldSlot();
            UpdateBlinkSlot();
        }

        private void UpdateArrowSlot()
        {
            float cd = playerStats.ArrowCooldownTimer;
            float fill = playerStats.ArrowCooldownNormalized;
            bool hasMana = playerStats.CurrentMana >= playerStats.ArrowManaCost;

            UpdateSlotVisuals(arrowSlot, cd, fill, hasMana);
        }

        private void UpdateSpikeSlot()
        {
            float cd = playerStats.SpikeCooldownTimer;
            float fill = playerStats.SpikeCooldownNormalized;
            bool hasMana = playerStats.CurrentMana >= playerStats.SpikeManaCost;

            UpdateSlotVisuals(spikeSlot, cd, fill, hasMana);
        }

        private void UpdateShieldSlot()
        {
            float cd = playerStats.ShieldCooldownTimer;
            float fill = playerStats.ShieldCooldownNormalized;
            bool hasMana = playerStats.CurrentMana >= playerStats.ShieldManaCost;

            UpdateSlotVisuals(shieldSlot, cd, fill, hasMana && !playerStats.IsShieldActive);
        }

        private void UpdateBlinkSlot()
        {
            bool hasResources = playerStats.CurrentMana >= playerStats.BlinkManaCost && 
                                playerStats.CurrentStamina >= playerStats.BlinkStaminaCost;

            UpdateSlotVisuals(blinkSlot, 0f, 0f, hasResources);
        }

        private void UpdateSlotVisuals(AbilitySlotUI slot, float cdTimer, float fillAmount, bool canCast)
        {
            if (slot.cooldownOverlay != null)
            {
                slot.cooldownOverlay.fillAmount = fillAmount;
                slot.cooldownOverlay.gameObject.SetActive(fillAmount > 0.001f);
            }

            if (slot.cooldownText != null)
            {
                if (cdTimer > 0.05f)
                {
                    slot.cooldownText.gameObject.SetActive(true);
                    slot.cooldownText.text = cdTimer >= 1f ? cdTimer.ToString("F1") : cdTimer.ToString("F1");
                }
                else
                {
                    slot.cooldownText.gameObject.SetActive(false);
                }
            }

            if (slot.button != null)
            {
                slot.button.interactable = canCast && (cdTimer <= 0.05f);
            }

            if (slot.iconImage != null)
            {
                Color c = slot.iconImage.color;
                c.a = (!canCast || cdTimer > 0.05f) ? 0.45f : 1f;
                slot.iconImage.color = c;
            }
        }
    }
}
