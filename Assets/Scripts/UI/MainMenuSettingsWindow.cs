using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FungalCurse.Systems;

namespace FungalCurse.UI
{
    /// <summary>
    /// Controller for the Settings & Progress Reset Window in the Main Menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuSettingsWindow : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TextMeshProUGUI volumeValueText;

        [Header("Display Settings")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TextMeshProUGUI fullscreenValueText;

        [Header("Progress Reset")]
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private GameObject confirmResetDialog;
        [SerializeField] private Button confirmResetButton;
        [SerializeField] private Button cancelResetButton;
        [SerializeField] private TextMeshProUGUI resetStatusText;

        [Header("Navigation")]
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            RefreshSettingsUI();

            if (volumeSlider != null)
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            if (resetProgressButton != null)
                resetProgressButton.onClick.AddListener(OpenResetConfirmDialog);

            if (confirmResetButton != null)
                confirmResetButton.onClick.AddListener(ConfirmResetProgress);

            if (cancelResetButton != null)
                cancelResetButton.onClick.AddListener(CloseResetConfirmDialog);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseWindow);

            if (confirmResetDialog != null)
                confirmResetDialog.SetActive(false);

            if (resetStatusText != null)
                resetStatusText.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            if (resetProgressButton != null) resetProgressButton.onClick.RemoveListener(OpenResetConfirmDialog);
            if (confirmResetButton != null) confirmResetButton.onClick.RemoveListener(ConfirmResetProgress);
            if (cancelResetButton != null) cancelResetButton.onClick.RemoveListener(CloseResetConfirmDialog);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseWindow);
        }

        public void RefreshSettingsUI()
        {
            // 1. Volume
            float vol = GameSettings.MasterVolume;
            if (volumeSlider != null)
            {
                volumeSlider.value = vol;
            }
            if (volumeValueText != null)
            {
                volumeValueText.text = $"{Mathf.RoundToInt(vol * 100f)}%";
            }

            // 2. Fullscreen
            bool fs = GameSettings.Fullscreen;
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = fs;
            }
            if (fullscreenValueText != null)
            {
                fullscreenValueText.text = fs ? "УВІМК." : "ВИМК.";
            }
        }

        private void OnVolumeChanged(float value)
        {
            GameSettings.MasterVolume = value;
            if (volumeValueText != null)
            {
                volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
            }
        }

        private void OnFullscreenChanged(bool value)
        {
            GameSettings.Fullscreen = value;
            if (fullscreenValueText != null)
            {
                fullscreenValueText.text = value ? "УВІМК." : "ВИМК.";
            }
        }

        private void OpenResetConfirmDialog()
        {
            if (confirmResetDialog != null)
            {
                confirmResetDialog.SetActive(true);
            }
        }

        private void CloseResetConfirmDialog()
        {
            if (confirmResetDialog != null)
            {
                confirmResetDialog.SetActive(false);
            }
        }

        private void ConfirmResetProgress()
        {
            GameSettings.ResetAllProgress();
            CloseResetConfirmDialog();

            if (resetStatusText != null)
            {
                resetStatusText.gameObject.SetActive(true);
                resetStatusText.text = "✓ Прогрес успішно скинуто!";
                StopAllCoroutines();
                StartCoroutine(HideResetStatusCoroutine());
            }
        }

        private IEnumerator HideResetStatusCoroutine()
        {
            yield return new WaitForSeconds(3f);
            if (resetStatusText != null)
            {
                resetStatusText.gameObject.SetActive(false);
            }
        }

        private void CloseWindow()
        {
            gameObject.SetActive(false);
        }
    }
}
