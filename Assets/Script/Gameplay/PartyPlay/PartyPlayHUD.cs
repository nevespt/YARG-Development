using System;
using System.Collections.Generic;
using DG.Tweening;
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
        // 0-Players Idle Center Emblem & Bottom Bar (GH:WoR Inspired)
        private GameObject _idleEmblemContainer;
        private TextMeshProUGUI _idleTitleText;
        private TextMeshProUGUI _idleSubText;
        private GameObject _bottomBarContainer;

        // Active Gameplay Top Join Banner
        private GameObject _topJoinPromptContainer;
        private TextMeshProUGUI _topJoinPromptText;

        // Drop-In Modal Dialog
        private GameObject _dropInDialog;
        private TextMeshProUGUI _dialogTitleText;
        private TextMeshProUGUI _dialogProfileText;
        private TextMeshProUGUI _dialogInstrumentText;
        private TextMeshProUGUI _dialogDifficultyText;
        private TextMeshProUGUI _dialogHelpText;

        // Cinematic Transition Fader Overlay
        private CanvasGroup _transitionFaderCanvasGroup;

        private bool _isDropInActive;
        public bool IsDropInActive => _isDropInActive;

        private void Awake()
        {
            CreateDefaultHUD();
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

            // ==========================================
            // 0. Cinematic Transition Fader Overlay
            // ==========================================
            var faderGo = new GameObject("TransitionFader");
            faderGo.transform.SetParent(canvasGo.transform, false);
            var faderRect = faderGo.AddComponent<RectTransform>();
            faderRect.anchorMin = Vector2.zero;
            faderRect.anchorMax = Vector2.one;
            faderRect.sizeDelta = Vector2.zero;

            var faderImg = faderGo.AddComponent<Image>();
            faderImg.color = new Color(0.04f, 0.04f, 0.07f, 1f);

            _transitionFaderCanvasGroup = faderGo.AddComponent<CanvasGroup>();
            _transitionFaderCanvasGroup.alpha = 0f;
            _transitionFaderCanvasGroup.blocksRaycasts = false;

            // ==========================================
            // 1. Idle Bottom-Center Emblem ("PARTY PLAY")
            // ==========================================
            _idleEmblemContainer = new GameObject("IdleEmblemContainer");
            _idleEmblemContainer.transform.SetParent(canvasGo.transform, false);
            var emblemRect = _idleEmblemContainer.AddComponent<RectTransform>();
            emblemRect.anchorMin = new Vector2(0.5f, 0f);
            emblemRect.anchorMax = new Vector2(0.5f, 0f);
            emblemRect.pivot = new Vector2(0.5f, 0f);
            emblemRect.anchoredPosition = new Vector2(0f, 96f);
            emblemRect.sizeDelta = new Vector2(440f, 72f);

            // Emblem Outer Glow / Border
            var emblemBorder = _idleEmblemContainer.AddComponent<Image>();
            emblemBorder.color = new Color(0.95f, 0.65f, 0.15f, 0.85f); // Golden amber glow

            // Emblem Inner Background
            var emblemInnerGo = new GameObject("EmblemInner");
            emblemInnerGo.transform.SetParent(_idleEmblemContainer.transform, false);
            var innerRect = emblemInnerGo.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.sizeDelta = new Vector2(-6f, -6f); // 3px border
            var innerBg = emblemInnerGo.AddComponent<Image>();
            innerBg.color = new Color(0.06f, 0.06f, 0.10f, 0.95f); // Deep dark metallic

            // Emblem Title
            var titleGo = new GameObject("EmblemTitle");
            titleGo.transform.SetParent(emblemInnerGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.28f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.sizeDelta = Vector2.zero;

            _idleTitleText = titleGo.AddComponent<TextMeshProUGUI>();
            _idleTitleText.alignment = TextAlignmentOptions.Center;
            _idleTitleText.fontSize = 30;
            _idleTitleText.fontStyle = FontStyles.Bold;
            _idleTitleText.text = "<b><color=#FFC83B>P A R T Y</color>   <color=#FFFFFF>P L A Y</color></b>";

            // Emblem Subtitle
            var subGo = new GameObject("EmblemSub");
            subGo.transform.SetParent(emblemInnerGo.transform, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0.38f);
            subRect.sizeDelta = Vector2.zero;

            _idleSubText = subGo.AddComponent<TextMeshProUGUI>();
            _idleSubText.alignment = TextAlignmentOptions.Center;
            _idleSubText.fontSize = 13;
            _idleSubText.fontStyle = FontStyles.Bold;
            _idleSubText.color = new Color(0.6f, 0.65f, 0.75f, 0.9f);
            _idleSubText.text = "• JUKEBOX SESSION •";

            // Subtle gentle pulse on the emblem
            _idleEmblemContainer.transform.DOScale(1.03f, 1.2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

            // ==========================================
            // 2. Idle Bottom Navigation Bar (Pill Prompts)
            // ==========================================
            _bottomBarContainer = new GameObject("BottomBarContainer");
            _bottomBarContainer.transform.SetParent(canvasGo.transform, false);
            var barRect = _bottomBarContainer.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 36f);
            barRect.sizeDelta = new Vector2(760f, 44f);

            var barLayout = _bottomBarContainer.AddComponent<HorizontalLayoutGroup>();
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.spacing = 16f;
            barLayout.childControlWidth = false;
            barLayout.childControlHeight = false;

            CreatePromptPill(_bottomBarContainer, "JoinPill", new Color(0.18f, 0.8f, 0.44f), "GREEN / START", "JOIN", 220f);
            CreatePromptPill(_bottomBarContainer, "MenuPill", new Color(0.9f, 0.28f, 0.24f), "RED / ESC", "MAIN MENU", 230f);
            CreatePromptPill(_bottomBarContainer, "SkipPill", new Color(0.95f, 0.77f, 0.05f), "YELLOW", "SKIP SONG", 210f);

            // ==========================================
            // 3. Active Mode Top-Center Join Banner
            // ==========================================
            _topJoinPromptContainer = new GameObject("TopJoinPromptContainer");
            _topJoinPromptContainer.transform.SetParent(canvasGo.transform, false);
            var promptRect = _topJoinPromptContainer.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 1f);
            promptRect.anchorMax = new Vector2(0.5f, 1f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -20f);
            promptRect.sizeDelta = new Vector2(620f, 48f);

            var promptBg = _topJoinPromptContainer.AddComponent<Image>();
            promptBg.color = new Color(0.06f, 0.06f, 0.10f, 0.85f);

            var promptTextGo = new GameObject("PromptText");
            promptTextGo.transform.SetParent(_topJoinPromptContainer.transform, false);
            var promptTextRect = promptTextGo.AddComponent<RectTransform>();
            promptTextRect.anchorMin = Vector2.zero;
            promptTextRect.anchorMax = Vector2.one;
            promptTextRect.sizeDelta = Vector2.zero;

            _topJoinPromptText = promptTextGo.AddComponent<TextMeshProUGUI>();
            _topJoinPromptText.alignment = TextAlignmentOptions.Center;
            _topJoinPromptText.fontSize = 22;
            _topJoinPromptText.color = Color.white;
            _topJoinPromptText.text = "🟢 <color=#FFC83B><b>PARTY PLAY</b></color> — Press <color=#2ECC71><b>[START]</b></color> to Join!";

            _topJoinPromptContainer.SetActive(false);

            // ==========================================
            // 4. Drop-In Modal Dialog
            // ==========================================
            _dropInDialog = new GameObject("DropInDialog");
            _dropInDialog.transform.SetParent(canvasGo.transform, false);
            var dialogRect = _dropInDialog.AddComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = Vector2.zero;
            dialogRect.sizeDelta = new Vector2(540f, 350f);

            // Modal Border
            var dialogBorder = _dropInDialog.AddComponent<Image>();
            dialogBorder.color = new Color(0.95f, 0.65f, 0.15f, 0.9f);

            // Modal Background
            var dialogInnerGo = new GameObject("DialogInner");
            dialogInnerGo.transform.SetParent(_dropInDialog.transform, false);
            var dialogInnerRect = dialogInnerGo.AddComponent<RectTransform>();
            dialogInnerRect.anchorMin = Vector2.zero;
            dialogInnerRect.anchorMax = Vector2.one;
            dialogInnerRect.sizeDelta = new Vector2(-6f, -6f);
            var dialogBg = dialogInnerGo.AddComponent<Image>();
            dialogBg.color = new Color(0.08f, 0.08f, 0.13f, 0.98f);

            // Title
            var dTitleGo = new GameObject("DialogTitle");
            dTitleGo.transform.SetParent(dialogInnerGo.transform, false);
            var dTitleRect = dTitleGo.AddComponent<RectTransform>();
            dTitleRect.anchorMin = new Vector2(0f, 0.78f);
            dTitleRect.anchorMax = new Vector2(1f, 1f);
            dTitleRect.sizeDelta = Vector2.zero;
            _dialogTitleText = dTitleGo.AddComponent<TextMeshProUGUI>();
            _dialogTitleText.alignment = TextAlignmentOptions.Center;
            _dialogTitleText.fontSize = 28;
            _dialogTitleText.fontStyle = FontStyles.Bold;
            _dialogTitleText.text = "<b><color=#FFC83B>DROP IN & PLAY</color></b>";

            // Profile Row
            var profGo = new GameObject("DialogProfile");
            profGo.transform.SetParent(dialogInnerGo.transform, false);
            var profRect = profGo.AddComponent<RectTransform>();
            profRect.anchorMin = new Vector2(0.08f, 0.58f);
            profRect.anchorMax = new Vector2(0.92f, 0.75f);
            profRect.sizeDelta = Vector2.zero;
            _dialogProfileText = profGo.AddComponent<TextMeshProUGUI>();
            _dialogProfileText.alignment = TextAlignmentOptions.Left;
            _dialogProfileText.fontSize = 22;
            _dialogProfileText.color = Color.white;

            // Instrument Row
            var instGo = new GameObject("DialogInstrument");
            instGo.transform.SetParent(dialogInnerGo.transform, false);
            var instRect = instGo.AddComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0.08f, 0.40f);
            instRect.anchorMax = new Vector2(0.92f, 0.57f);
            instRect.sizeDelta = Vector2.zero;
            _dialogInstrumentText = instGo.AddComponent<TextMeshProUGUI>();
            _dialogInstrumentText.alignment = TextAlignmentOptions.Left;
            _dialogInstrumentText.fontSize = 22;
            _dialogInstrumentText.color = Color.white;

            // Difficulty Row
            var diffGo = new GameObject("DialogDiff");
            diffGo.transform.SetParent(dialogInnerGo.transform, false);
            var diffRect = diffGo.AddComponent<RectTransform>();
            diffRect.anchorMin = new Vector2(0.08f, 0.22f);
            diffRect.anchorMax = new Vector2(0.92f, 0.39f);
            diffRect.sizeDelta = Vector2.zero;
            _dialogDifficultyText = diffGo.AddComponent<TextMeshProUGUI>();
            _dialogDifficultyText.alignment = TextAlignmentOptions.Left;
            _dialogDifficultyText.fontSize = 22;
            _dialogDifficultyText.color = Color.white;

            // Help Footer
            var helpGo = new GameObject("DialogHelp");
            helpGo.transform.SetParent(dialogInnerGo.transform, false);
            var helpRect = helpGo.AddComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(0f, 0f);
            helpRect.anchorMax = new Vector2(1f, 0.20f);
            helpRect.sizeDelta = Vector2.zero;
            _dialogHelpText = helpGo.AddComponent<TextMeshProUGUI>();
            _dialogHelpText.alignment = TextAlignmentOptions.Center;
            _dialogHelpText.fontSize = 17;
            _dialogHelpText.color = new Color(0.7f, 0.75f, 0.85f);
            _dialogHelpText.text = "[UP/DOWN] Select   [LEFT/RIGHT] Change   [GREEN/ENTER] Join   [RED/ESC] Cancel";

            _dropInDialog.SetActive(false);
        }

        private void CreatePromptPill(GameObject parent, string name, Color badgeColor, string badgeText, string labelText, float width)
        {
            var pillGo = new GameObject(name);
            pillGo.transform.SetParent(parent.transform, false);
            var pillRect = pillGo.AddComponent<RectTransform>();
            pillRect.sizeDelta = new Vector2(width, 42f);

            var pillBg = pillGo.AddComponent<Image>();
            pillBg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(pillGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            string hexColor = ColorUtility.ToHtmlStringRGB(badgeColor);
            tmp.text = $"<color=#{hexColor}>[{badgeText}]</color> <color=#FFFFFF>{labelText}</color>";
        }

        public void UpdateHUDState(int activePlayerCount, bool canAnyPlayerJoin, bool isSummaryActive)
        {
            if (_isDropInActive || isSummaryActive)
            {
                _idleEmblemContainer?.SetActive(false);
                _bottomBarContainer?.SetActive(false);
                _topJoinPromptContainer?.SetActive(false);
                return;
            }

            if (activePlayerCount == 0)
            {
                // Idle / Jukebox Mode (GH5/WoR Style with 0 players)
                _idleEmblemContainer?.SetActive(true);
                _bottomBarContainer?.SetActive(true);
                _topJoinPromptContainer?.SetActive(false);
            }
            else
            {
                // Active Gameplay (1+ players)
                _idleEmblemContainer?.SetActive(false);
                _bottomBarContainer?.SetActive(false);
                _topJoinPromptContainer?.SetActive(canAnyPlayerJoin);
            }
        }

        public void ShowDropInDialog(string profileName, Instrument instrument, Difficulty difficulty, int selectedRow)
        {
            _isDropInActive = true;
            _idleEmblemContainer?.SetActive(false);
            _bottomBarContainer?.SetActive(false);
            _topJoinPromptContainer?.SetActive(false);

            _dropInDialog.SetActive(true);

            string profPrefix = selectedRow == 0 ? "> " : "  ";
            string instPrefix = selectedRow == 1 ? "> " : "  ";
            string diffPrefix = selectedRow == 2 ? "> " : "  ";

            _dialogProfileText.text = $"{profPrefix}Profile: <b><color=#FFC83B>{profileName}</color></b>";
            _dialogInstrumentText.text = $"{instPrefix}Instrument: <b><color=#FFC83B>{instrument}</color></b>";
            _dialogDifficultyText.text = $"{diffPrefix}Difficulty: <b><color=#FFC83B>{difficulty}</color></b>";
        }

        public void HideDropInDialog()
        {
            _isDropInActive = false;
            if (_dropInDialog != null)
            {
                _dropInDialog.SetActive(false);
            }
        }

        public void FadeOutToBlack(float duration = 0.35f, Action onComplete = null)
        {
            if (_transitionFaderCanvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            _transitionFaderCanvasGroup.DOKill();
            _transitionFaderCanvasGroup.DOFade(1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void FadeInFromBlack(float duration = 0.65f)
        {
            if (_transitionFaderCanvasGroup == null) return;

            _transitionFaderCanvasGroup.DOKill();
            _transitionFaderCanvasGroup.DOFade(0f, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }
    }
}
