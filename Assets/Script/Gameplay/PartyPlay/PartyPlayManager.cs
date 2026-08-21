using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.Player;
using YARG.Input;
using YARG.Player;
using YARG.Song;

namespace YARG.Gameplay.PartyPlay
{
    public class PartyPlayManager : MonoBehaviour
    {
        public static PartyPlayManager Instance { get; private set; }

        private GameManager _gameManager;
        private PartyPlayHUD _hud;
        public PartyPlayHUD HUD => _hud;
        private PartyPlayScoreSummary _scoreSummary;

        private bool _isDropInActive;
        private bool _isSummaryActive;
        private bool _isTransitioning;

        private YargPlayer _currentDropInPlayer;
        private int _selectedRow; // 0 = Instrument, 1 = Difficulty
        private List<Instrument> _availableInstruments = new();
        private int _instrumentIndex;
        private readonly Difficulty[] _difficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert };
        private int _difficultyIndex = 2; // Default Hard

        public bool HasActivePlayers => _gameManager != null && _gameManager.TotalPlayers > 0;

        private void Awake()
        {
            if (!GlobalVariables.State.IsPartyPlay)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _gameManager = GetComponent<GameManager>();

            _hud = gameObject.AddComponent<PartyPlayHUD>();
            _scoreSummary = gameObject.AddComponent<PartyPlayScoreSummary>();
        }

        private void OnEnable()
        {
            InputManager.MenuInput += OnMenuInput;
        }

        private void OnDisable()
        {
            InputManager.MenuInput -= OnMenuInput;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private bool CanAnyPlayerJoin
        {
            get
            {
                if (_gameManager == null) return false;

                // Check if any profile in PlayerContainer is not currently in active gameplay
                foreach (var profile in PlayerContainer.Profiles)
                {
                    if (!_gameManager.Players.Any(bp => bp.Player != null && bp.Player.Profile == profile))
                    {
                        return true;
                    }
                }

                // Also check if any player in PlayerContainer.Players is not currently playing
                foreach (var player in PlayerContainer.Players)
                {
                    if (!_gameManager.Players.Any(bp => bp.Player == player))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Update()
        {
            if (!GlobalVariables.State.IsPartyPlay || _gameManager == null)
            {
                return;
            }

            // Detect natural end of song to display summary & transition
            if (_gameManager.IsSongStarted && _gameManager.SongLength > 0 && !_isSummaryActive && !_isTransitioning)
            {
                if (_gameManager.SongTime >= _gameManager.SongLength)
                {
                    _isSummaryActive = true;
                    ShowEndSongSummary();
                }
            }

            // Update join prompt visibility - only show if someone can actually join!
            bool canJoin = CanAnyPlayerJoin;
            _hud.SetJoinPromptVisible(!_isDropInActive && !_isSummaryActive && canJoin);

            // Fallback keyboard / gamepad detection for unassigned players (only if a profile is free)
            if (!_isDropInActive && !_isSummaryActive && canJoin)
            {
                CheckForFallbackJoinInput();
            }
        }

        private void OnMenuInput(YargPlayer player, ref GameInput input)
        {
            if (!GlobalVariables.State.IsPartyPlay || _gameManager == null)
            {
                return;
            }

            if (!input.Button)
            {
                return;
            }

            var action = (MenuAction)input.Action;

            // If summary card is showing, allow Start / Green to skip countdown immediately
            if (_isSummaryActive)
            {
                if (action is MenuAction.Start or MenuAction.Green or MenuAction.Select)
                {
                    _scoreSummary.SkipNow();
                }
                return;
            }

            // If player is null, try to find an existing free profile
            if (player == null)
            {
                player = GetFreePlayer();
                if (player == null)
                {
                    // No free profiles available in the game! Ignore join attempt.
                    return;
                }
            }

            bool isSpawned = _gameManager.Players.Any(p => p.Player == player);

            if (!isSpawned)
            {
                // Player is not currently on track
                if (!_isDropInActive)
                {
                    StartDropInForPlayer(player);
                }
                else if (_currentDropInPlayer == player)
                {
                    HandleMenuAction(action);
                }
            }
            else
            {
                // Player is already playing - pressing Start drops out
                if (action == MenuAction.Start)
                {
                    var basePlayer = _gameManager.Players.FirstOrDefault(p => p.Player == player);
                    if (basePlayer != null)
                    {
                        DropOut(basePlayer);
                    }
                }
            }
        }

        private YargPlayer GetFreePlayer()
        {
            if (_gameManager == null) return null;

            // 1. Check existing active players in PlayerContainer that aren't spawned
            foreach (var p in PlayerContainer.Players)
            {
                if (!_gameManager.Players.Any(bp => bp.Player == p))
                {
                    return p;
                }
            }

            // 2. Next check untaken profiles created in the game
            foreach (var profile in PlayerContainer.Profiles)
            {
                if (!PlayerContainer.IsProfileTaken(profile))
                {
                    return PlayerContainer.CreatePlayerFromProfile(profile, true);
                }
            }

            // 3. Fallback: Check any profile not currently in _gameManager.Players
            foreach (var profile in PlayerContainer.Profiles)
            {
                if (!_gameManager.Players.Any(bp => bp.Player != null && bp.Player.Profile == profile))
                {
                    var player = PlayerContainer.Players.FirstOrDefault(p => p.Profile == profile);
                    if (player != null)
                    {
                        return player;
                    }
                    return PlayerContainer.CreatePlayerFromProfile(profile, true);
                }
            }

            return null;
        }

        private void CheckForFallbackJoinInput()
        {
            bool joinPressed = false;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                {
                    joinPressed = true;
                }
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.startButton.wasPressedThisFrame ||
                    Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    joinPressed = true;
                }
            }

            if (joinPressed)
            {
                var player = GetFreePlayer();
                if (player != null)
                {
                    StartDropInForPlayer(player);
                }
            }
        }

        private void StartDropInForPlayer(YargPlayer player)
        {
            _currentDropInPlayer = player;
            UpdateAvailableInstruments();

            if (_availableInstruments.Count == 0)
            {
                return;
            }

            // Initialize instrument & difficulty from player's profile if available
            _instrumentIndex = _availableInstruments.IndexOf(player.Profile.CurrentInstrument);
            if (_instrumentIndex < 0) _instrumentIndex = 0;

            _difficultyIndex = Array.IndexOf(_difficulties, player.Profile.CurrentDifficulty);
            if (_difficultyIndex < 0) _difficultyIndex = 2; // Default Hard

            _isDropInActive = true;
            _selectedRow = 0; // Default to instrument selection
            UpdateDialogDisplay();
        }

        private void UpdateAvailableInstruments()
        {
            _availableInstruments.Clear();
            if (_gameManager.Song != null)
            {
                foreach (Instrument inst in Enum.GetValues(typeof(Instrument)))
                {
                    if (inst != Instrument.Band && _gameManager.Song.HasInstrument(inst))
                    {
                        _availableInstruments.Add(inst);
                    }
                }
            }

            if (_availableInstruments.Count == 0)
            {
                _availableInstruments.Add(Instrument.FiveFretGuitar);
            }

            _instrumentIndex = Mathf.Clamp(_instrumentIndex, 0, _availableInstruments.Count - 1);
        }

        private void HandleMenuAction(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.Up:
                    _selectedRow = Mathf.Max(0, _selectedRow - 1);
                    UpdateDialogDisplay();
                    break;

                case MenuAction.Down:
                    _selectedRow = Mathf.Min(1, _selectedRow + 1);
                    UpdateDialogDisplay();
                    break;

                case MenuAction.Left:
                    ChangeCurrentRowValue(-1);
                    UpdateDialogDisplay();
                    break;

                case MenuAction.Right:
                    ChangeCurrentRowValue(1);
                    UpdateDialogDisplay();
                    break;

                case MenuAction.Green:
                case MenuAction.Select:
                case MenuAction.Start:
                    ConfirmDropIn();
                    break;

                case MenuAction.Red:
                    CancelDropIn();
                    break;
            }
        }

        private void ChangeCurrentRowValue(int delta)
        {
            switch (_selectedRow)
            {
                case 0: // Instrument
                    if (_availableInstruments.Count > 0)
                    {
                        _instrumentIndex = (_instrumentIndex + delta + _availableInstruments.Count) % _availableInstruments.Count;
                    }
                    break;

                case 1: // Difficulty
                    _difficultyIndex = Mathf.Clamp(_difficultyIndex + delta, 0, _difficulties.Length - 1);
                    break;
            }
        }

        private void UpdateDialogDisplay()
        {
            if (_currentDropInPlayer == null) return;

            string profName = _currentDropInPlayer.Profile.Name;
            Instrument inst = _availableInstruments.Count > 0 ? _availableInstruments[_instrumentIndex] : Instrument.FiveFretGuitar;
            Difficulty diff = _difficulties[_difficultyIndex];

            _hud.ShowDropInDialog(profName, inst, diff, _selectedRow + 1); // Row 1 = Instrument, Row 2 = Difficulty
        }

        private void ConfirmDropIn()
        {
            if (_currentDropInPlayer == null || _availableInstruments.Count == 0)
            {
                CancelDropIn();
                return;
            }

            var profile = _currentDropInPlayer.Profile;
            var instrument = _availableInstruments[_instrumentIndex];
            var difficulty = _difficulties[_difficultyIndex];

            profile.CurrentInstrument = instrument;
            profile.CurrentDifficulty = difficulty;
            profile.GameMode = instrument.ToNativeGameMode();

            _gameManager.SpawnPartyPlayer(_currentDropInPlayer);
            YargLogger.LogFormatInfo("Player {0} dropped into Party Play on {1} ({2})", profile.Name, instrument, difficulty);

            _isDropInActive = false;
            _currentDropInPlayer = null;
            _hud.HideDropInDialog();
        }

        private void CancelDropIn()
        {
            _isDropInActive = false;
            _currentDropInPlayer = null;
            _hud.HideDropInDialog();
        }

        public void DropOut(BasePlayer player)
        {
            if (player == null) return;

            _gameManager.RemovePartyPlayer(player);
            YargLogger.LogFormatInfo("Player {0} dropped out of Party Play.", player.Player?.Profile?.Name ?? "Unknown");
        }

        public void ShowEndSongSummary()
        {
            _scoreSummary.Show(_gameManager.Song, _gameManager.BandScore, _gameManager.BandStars, _gameManager.Players, () =>
            {
                AdvanceToNextSong();
            });
        }

        public static bool ShowOverlayButtons { get; set; } = false;

        private void OnGUI()
        {
            if (!GlobalVariables.State.IsPartyPlay || !ShowOverlayButtons || _gameManager == null)
            {
                return;
            }

            float btnSize = 52f;
            float margin = 20f;
            float spacing = 10f;
            float x = Screen.width - margin - (btnSize * 2 + spacing);
            float y = Screen.height - margin - btnSize;

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.14f, 0.9f);

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = new Color(1f, 0.85f, 0.2f);
            buttonStyle.active.textColor = Color.cyan;

            // Near End button (🏁)
            if (GUI.Button(new Rect(x, y, btnSize, btnSize), new GUIContent("🏁", "Near End (-5s)"), buttonStyle))
            {
                SeekNearEnd();
            }

            // Skip button (⏭)
            if (GUI.Button(new Rect(x + btnSize + spacing, y, btnSize, btnSize), new GUIContent("⏭", "Skip Song"), buttonStyle))
            {
                SkipSong();
            }

            GUI.backgroundColor = oldColor;
        }

        public void SeekNearEnd()
        {
            if (_gameManager != null && _gameManager.SongLength > 0)
            {
                double targetTime = Math.Max(0, _gameManager.SongLength - 5.0);
                _gameManager.SetSongTime(targetTime, 0);
            }
        }

        public void SkipSong()
        {
            AdvanceToNextSong();
        }

        private void AdvanceToNextSong()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            _isSummaryActive = false;

            var nextSong = SongContainer.GetRandomSong();
            if (nextSong == null)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            GlobalVariables.State.CurrentSong = nextSong;
            GlobalVariables.State.CurrentReplay = null;
            GlobalVariables.State.IsPractice = false;
            GlobalVariables.State.PlayingAShow = false;
            GlobalVariables.State.IsPartyPlay = true;

            UniTask.Void(async () =>
            {
                try
                {
                    await _gameManager.TransitionPartyPlaySong(nextSong);
                }
                finally
                {
                    _isTransitioning = false;
                }
            });
        }
    }
}
