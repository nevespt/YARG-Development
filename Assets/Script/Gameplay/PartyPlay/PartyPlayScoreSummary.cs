using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Song;
using YARG.Gameplay.Player;
using YARG.Song;

namespace YARG.Gameplay.PartyPlay
{
    public class PartyPlayScoreSummary : MonoBehaviour
    {
        [SerializeField]
        private GameObject _container;
        [SerializeField]
        private TextMeshProUGUI _songTitleText;
        [SerializeField]
        private TextMeshProUGUI _bandScoreText;
        [SerializeField]
        private TextMeshProUGUI _playerStatsText;
        [SerializeField]
        private TextMeshProUGUI _countdownText;

        private float _countdown = 7f;
        private bool _isShowing;
        private Action _onCountdownFinished;

        private void Awake()
        {
            if (_container == null)
            {
                CreateDefaultUI();
            }
        }

        private void CreateDefaultUI()
        {
            var canvasGo = new GameObject("PartyPlayScoreCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            _container = new GameObject("SummaryContainer");
            _container.transform.SetParent(canvasGo.transform, false);
            var rect = _container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800f, 500f);

            var bg = _container.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            // Title
            var titleGo = new GameObject("SongTitle");
            titleGo.transform.SetParent(_container.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.8f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.sizeDelta = Vector2.zero;
            _songTitleText = titleGo.AddComponent<TextMeshProUGUI>();
            _songTitleText.alignment = TextAlignmentOptions.Center;
            _songTitleText.fontSize = 32;
            _songTitleText.fontStyle = FontStyles.Bold;
            _songTitleText.color = Color.white;

            // Band Score
            var scoreGo = new GameObject("BandScore");
            scoreGo.transform.SetParent(_container.transform, false);
            var scoreRect = scoreGo.AddComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.05f, 0.65f);
            scoreRect.anchorMax = new Vector2(0.95f, 0.8f);
            scoreRect.sizeDelta = Vector2.zero;
            _bandScoreText = scoreGo.AddComponent<TextMeshProUGUI>();
            _bandScoreText.alignment = TextAlignmentOptions.Center;
            _bandScoreText.fontSize = 28;
            _bandScoreText.color = new Color(1f, 0.85f, 0.2f);

            // Player Stats
            var statsGo = new GameObject("PlayerStats");
            statsGo.transform.SetParent(_container.transform, false);
            var statsRect = statsGo.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.08f, 0.18f);
            statsRect.anchorMax = new Vector2(0.92f, 0.65f);
            statsRect.sizeDelta = Vector2.zero;
            _playerStatsText = statsGo.AddComponent<TextMeshProUGUI>();
            _playerStatsText.alignment = TextAlignmentOptions.TopLeft;
            _playerStatsText.fontSize = 22;
            _playerStatsText.color = Color.white;

            // Countdown
            var cdGo = new GameObject("Countdown");
            cdGo.transform.SetParent(_container.transform, false);
            var cdRect = cdGo.AddComponent<RectTransform>();
            cdRect.anchorMin = new Vector2(0.05f, 0.03f);
            cdRect.anchorMax = new Vector2(0.95f, 0.16f);
            cdRect.sizeDelta = Vector2.zero;
            _countdownText = cdGo.AddComponent<TextMeshProUGUI>();
            _countdownText.alignment = TextAlignmentOptions.Center;
            _countdownText.fontSize = 20;
            _countdownText.color = new Color(0.7f, 0.7f, 0.7f);

            _container.SetActive(false);
        }

        public void Show(SongEntry song, int bandScore, float bandStars, IReadOnlyList<BasePlayer> players, Action onFinished)
        {
            _isShowing = true;
            _countdown = 7f;
            _onCountdownFinished = onFinished;
            _container.SetActive(true);

            _songTitleText.text = $"{song.Name} - {song.Artist}";
            _bandScoreText.text = $"Band Score: {bandScore:N0}   Stars: {bandStars:F1}★";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>PLAYERS:</b>");
            foreach (var p in players)
            {
                var stats = p.BaseStats;
                double percent = (stats.NotesHit + stats.NotesMissed) > 0
                    ? (double) stats.NotesHit / (stats.NotesHit + stats.NotesMissed) * 100.0
                    : 100.0;
                sb.AppendLine($"• {p.Player.Profile.Name} ({p.Player.Profile.CurrentInstrument} - {p.Player.Profile.CurrentDifficulty}): <b>{stats.TotalScore:N0} pts</b> | <b>{percent:F0}% hit</b> ({stats.NotesHit}/{stats.NotesHit + stats.NotesMissed})");
            }
            _playerStatsText.text = sb.ToString();
        }

        private void Update()
        {
            if (!_isShowing) return;

            _countdown -= Time.unscaledDeltaTime;
            if (_countdownText != null)
            {
                _countdownText.text = $"Next song starting in {Mathf.CeilToInt(Mathf.Max(0, _countdown))}s... [Press START/GREEN to skip]";
            }

            if (_countdown <= 0f)
            {
                _isShowing = false;
                _container.SetActive(false);
                _onCountdownFinished?.Invoke();
            }
        }

        public void SkipNow()
        {
            if (!_isShowing) return;
            _isShowing = false;
            _container.SetActive(false);
            _onCountdownFinished?.Invoke();
        }
    }
}
