using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Game;
using YARG.Localization;
using YARG.Player;

namespace YARG.Gameplay.PartyPlay
{
    public class PartyPlayHUD : MonoBehaviour
    {
        [SerializeField]
        private GameObject _joinPromptContainer;
        [SerializeField]
        private TextMeshProUGUI _joinPromptText;

        [SerializeField]
        private GameObject _dropInDialog;
        [SerializeField]
        private TextMeshProUGUI _dialogTitleText;
        [SerializeField]
        private TextMeshProUGUI _dialogProfileText;
        [SerializeField]
        private TextMeshProUGUI _dialogInstrumentText;
        [SerializeField]
        private TextMeshProUGUI _dialogDifficultyText;
        [SerializeField]
        private TextMeshProUGUI _dialogHelpText;

        private bool _isDropInActive;
        public bool IsDropInActive => _isDropInActive;

        private void Awake()
        {
            if (_joinPromptContainer == null)
            {
                CreateDefaultHUD();
            }
        }

        private void CreateDefaultHUD()
        {
            var canvasGo = new GameObject("PartyPlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Join Prompt Banner at top-center
            _joinPromptContainer = new GameObject("JoinPromptContainer");
            _joinPromptContainer.transform.SetParent(canvasGo.transform, false);
            var promptRect = _joinPromptContainer.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 1f);
            promptRect.anchorMax = new Vector2(0.5f, 1f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -20f);
            promptRect.sizeDelta = new Vector2(600f, 50f);

            var promptBg = _joinPromptContainer.AddComponent<Image>();
            promptBg.color = new Color(0f, 0f, 0f, 0.65f);

            var textGo = new GameObject("PromptText");
            textGo.transform.SetParent(_joinPromptContainer.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            _joinPromptText = textGo.AddComponent<TextMeshProUGUI>();
            _joinPromptText.alignment = TextAlignmentOptions.Center;
            _joinPromptText.fontSize = 24;
            _joinPromptText.color = Color.white;
            _joinPromptText.text = "PARTY PLAY - Press [START / ANY BUTTON] to Join!";

            // Drop-in overlay dialog (centered)
            _dropInDialog = new GameObject("DropInDialog");
            _dropInDialog.transform.SetParent(canvasGo.transform, false);
            var dialogRect = _dropInDialog.AddComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = Vector2.zero;
            dialogRect.sizeDelta = new Vector2(500f, 320f);

            var dialogBg = _dropInDialog.AddComponent<Image>();
            dialogBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Title
            var titleGo = new GameObject("DialogTitle");
            titleGo.transform.SetParent(_dropInDialog.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.75f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.sizeDelta = Vector2.zero;
            _dialogTitleText = titleGo.AddComponent<TextMeshProUGUI>();
            _dialogTitleText.alignment = TextAlignmentOptions.Center;
            _dialogTitleText.fontSize = 28;
            _dialogTitleText.fontStyle = FontStyles.Bold;
            _dialogTitleText.color = new Color(1f, 0.85f, 0.2f);
            _dialogTitleText.text = "DROP IN & PLAY";

            // Profile
            var profGo = new GameObject("DialogProfile");
            profGo.transform.SetParent(_dropInDialog.transform, false);
            var profRect = profGo.AddComponent<RectTransform>();
            profRect.anchorMin = new Vector2(0.1f, 0.55f);
            profRect.anchorMax = new Vector2(0.9f, 0.72f);
            profRect.sizeDelta = Vector2.zero;
            _dialogProfileText = profGo.AddComponent<TextMeshProUGUI>();
            _dialogProfileText.alignment = TextAlignmentOptions.Left;
            _dialogProfileText.fontSize = 22;
            _dialogProfileText.color = Color.white;

            // Instrument
            var instGo = new GameObject("DialogInstrument");
            instGo.transform.SetParent(_dropInDialog.transform, false);
            var instRect = instGo.AddComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0.1f, 0.38f);
            instRect.anchorMax = new Vector2(0.9f, 0.55f);
            instRect.sizeDelta = Vector2.zero;
            _dialogInstrumentText = instGo.AddComponent<TextMeshProUGUI>();
            _dialogInstrumentText.alignment = TextAlignmentOptions.Left;
            _dialogInstrumentText.fontSize = 22;
            _dialogInstrumentText.color = Color.white;

            // Difficulty
            var diffGo = new GameObject("DialogDiff");
            diffGo.transform.SetParent(_dropInDialog.transform, false);
            var diffRect = diffGo.AddComponent<RectTransform>();
            diffRect.anchorMin = new Vector2(0.1f, 0.21f);
            diffRect.anchorMax = new Vector2(0.9f, 0.38f);
            diffRect.sizeDelta = Vector2.zero;
            _dialogDifficultyText = diffGo.AddComponent<TextMeshProUGUI>();
            _dialogDifficultyText.alignment = TextAlignmentOptions.Left;
            _dialogDifficultyText.fontSize = 22;
            _dialogDifficultyText.color = Color.white;

            // Help Footer
            var helpGo = new GameObject("DialogHelp");
            helpGo.transform.SetParent(_dropInDialog.transform, false);
            var helpRect = helpGo.AddComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(0f, 0f);
            helpRect.anchorMax = new Vector2(1f, 0.2f);
            helpRect.sizeDelta = Vector2.zero;
            _dialogHelpText = helpGo.AddComponent<TextMeshProUGUI>();
            _dialogHelpText.alignment = TextAlignmentOptions.Center;
            _dialogHelpText.fontSize = 18;
            _dialogHelpText.color = new Color(0.7f, 0.7f, 0.7f);
            _dialogHelpText.text = "[UP/DOWN] Select Field   [LEFT/RIGHT] Change   [GREEN/ENTER] Join   [RED/ESC] Cancel";

            _dropInDialog.SetActive(false);
        }

        public void SetJoinPromptVisible(bool visible)
        {
            if (_joinPromptContainer != null)
            {
                _joinPromptContainer.SetActive(visible);
            }
        }

        public void ShowDropInDialog(string profileName, Instrument instrument, Difficulty difficulty, int selectedRow)
        {
            _isDropInActive = true;
            _dropInDialog.SetActive(true);

            string profPrefix = selectedRow == 0 ? "> " : "  ";
            string instPrefix = selectedRow == 1 ? "> " : "  ";
            string diffPrefix = selectedRow == 2 ? "> " : "  ";

            _dialogProfileText.text = $"{profPrefix}Profile: <b>{profileName}</b>";
            _dialogInstrumentText.text = $"{instPrefix}Instrument: <b>{instrument}</b>";
            _dialogDifficultyText.text = $"{diffPrefix}Difficulty: <b>{difficulty}</b>";
        }

        public void HideDropInDialog()
        {
            _isDropInActive = false;
            if (_dropInDialog != null)
            {
                _dropInDialog.SetActive(false);
            }
        }
    }
}
