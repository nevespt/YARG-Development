using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Venue;
using YARG.Gameplay.HUD;
using YARG.Gameplay.PartyPlay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Integration;
using YARG.Menu.Persistent;
using YARG.Playback;
using YARG.Player;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;
using YARG.Venue;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private readonly HashSet<YargPlayer> _partyPlayActiveRoster = new();

        public BasePlayer SpawnPartyPlayer(YargPlayer player, bool refreshHud = true, bool isTransition = false)
        {
            if (player == null || Chart == null) return null;

            _partyPlayActiveRoster.Add(player);

            player.IsScoreValid = true;
            if (!player.IsReplay)
            {
                foreach (var mic in player.Bindings.Microphones)
                {
                    mic.Reset();
                }
                player.RefreshPresets();
            }

            int highwayIndex = _players.Count(p => p is TrackPlayer);
            var lastHighScore = ScoreContainer.GetHighScore(Song.Hash, player.Profile.Id, player.Profile.CurrentInstrument, false)?.Score;

            BasePlayer newPlayer = null;

            if (player.Profile.GameMode != GameMode.Vocals)
            {
                var prefab = player.Profile.GameMode switch
                {
                    GameMode.FiveFretGuitar => _fiveFretGuitarPrefab,
                    GameMode.SixFretGuitar  => _sixFretGuitarPrefab,
                    GameMode.FourLaneDrums  => _fourLaneDrumsPrefab,
                    GameMode.FiveLaneDrums  => _fiveLaneDrumsPrefab,
                    GameMode.EliteDrums     => Song.HasInstrument(Instrument.FiveLaneDrums) ? _fiveLaneDrumsPrefab : _fourLaneDrumsPrefab,
                    GameMode.ProKeys        => player.Profile.CurrentInstrument is Instrument.ProKeys ? _proKeysPrefab : _fiveLaneKeysPrefab,
                    GameMode.ProGuitar      => _proGuitarPrefab,
                    _                       => null
                };

                if (prefab == null) return null;

                var playerObject = Instantiate(prefab, new Vector3(highwayIndex * TRACK_SPACING_X, 100f, 0f), prefab.transform.rotation);
                var trackPlayer = playerObject.GetComponent<TrackPlayer>();
                var trackView = _trackViewManager.CreateTrackView();
                trackPlayer.Initialize(highwayIndex, player, Chart, trackView, _mixer, lastHighScore);

                _players.Add(trackPlayer);
                _trackViewManager.AddTrackPlayer(trackPlayer);

                trackPlayer.PlayEnterAnimation();

                newPlayer = trackPlayer;
            }
            else
            {
                if (!VocalTrack.gameObject.activeSelf)
                {
                    VocalTrack.gameObject.SetActive(true);
                    VocalTrack.transform.position = new Vector3(highwayIndex * TRACK_SPACING_X, 100, 0);
                    _trackViewManager.CreateVocalTrackView(highwayIndex);
                    var chart = player.Profile.CurrentInstrument == Instrument.Vocals
                        ? Chart.Vocals
                        : Chart.Harmony;
                    VocalTrack.Initialize(chart, player, Song.VocalScrollSpeedScalingFactor);

                    if (SettingsManager.Settings.KeepLyricBar.Value &&
                        SettingsManager.Settings.LyricDisplay.Value != LyricDisplayMode.Disabled)
                    {
                        _lyricBar.SetVocalPlayerLayout();
                    }
                    else
                    {
                        _lyricBar.gameObject.SetActive(false);
                    }
                }

                var vocalsPlayer = VocalTrack.CreatePlayer();
                var playerHud = _trackViewManager.CreateVocalsPlayerHUD();
                var percussionTrack = VocalTrack.CreatePercussionTrack();
                percussionTrack.TrackSpeed = VocalTrack.TrackSpeed;
                vocalsPlayer.Initialize(_players.Count, _players.Count(p => p is VocalsPlayer), player, Chart, playerHud, percussionTrack, lastHighScore, VocalTrack.TrackSpeed);

                _players.Add(vocalsPlayer);
                newPlayer = vocalsPlayer;
            }

            foreach (var stem in player.Profile.CurrentInstrument.ToSongStems())
            {
                var transformedStem = stem;
                if (stem == SongStem.Bass && !_stemStates.ContainsKey(SongStem.Bass))
                {
                    transformedStem = SongStem.Rhythm;
                }
                if (transformedStem != _backgroundStem && _stemStates.TryGetValue(transformedStem, out var state))
                {
                    ++state.Total;
                    ++state.Audible;
                }
            }

            if (refreshHud)
            {
                EngineManager.StarScoreThresholds = EngineManager.GetStarScoreCutoffs(_players.ConvertAll(p => p.BaseEngine.StarScoreThresholds));
                EngineManager.InitializeHappiness(true);
                _failMeter.RefreshPlayers();
                PartyPlayHUDController.ResetUnisonDisplay(_unisonDisplay, Chart);

                var scoreBox = UnityEngine.Object.FindFirstObjectByType<ScoreBox>(FindObjectsInactive.Include);
                if (scoreBox != null)
                {
                    scoreBox.gameObject.SetActive(_players.Count > 0);
                }

                _lyricBar?.UpdateBackgroundDisplay();
            }

            return newPlayer;
        }

        public BasePlayer ReplacePartyPlayer(BasePlayer oldPlayer, YargPlayer profilePlayer)
        {
            if (oldPlayer == null || !_players.Contains(oldPlayer)) return null;

            if (oldPlayer.Player != null)
            {
                _partyPlayActiveRoster.Remove(oldPlayer.Player);
            }
            if (profilePlayer != null)
            {
                _partyPlayActiveRoster.Add(profilePlayer);
            }

            int playerIndex = _players.IndexOf(oldPlayer);
            int highwayIndex = (oldPlayer is TrackPlayer tp) ? tp.HighwayIndex : 0;

            var oldEngine = oldPlayer.GetEngineContainer();
            if (oldEngine != null)
            {
                EngineManager.Unregister(oldEngine);
            }

            foreach (var stem in oldPlayer.Player.Profile.CurrentInstrument.ToSongStems())
            {
                var transformedStem = stem;
                if (stem == SongStem.Bass && !_stemStates.ContainsKey(SongStem.Bass))
                {
                    transformedStem = SongStem.Rhythm;
                }
                if (transformedStem != _backgroundStem && _stemStates.TryGetValue(transformedStem, out var state))
                {
                    state.Total = Math.Max(0, state.Total - 1);
                    state.Audible = Math.Max(0, state.Audible - 1);
                }
            }

            BasePlayer newPlayer = null;
            if (profilePlayer.Profile.GameMode != GameMode.Vocals)
            {
                var prefab = profilePlayer.Profile.GameMode switch
                {
                    GameMode.FiveFretGuitar => _fiveFretGuitarPrefab,
                    GameMode.SixFretGuitar  => _sixFretGuitarPrefab,
                    GameMode.FourLaneDrums  => _fourLaneDrumsPrefab,
                    GameMode.FiveLaneDrums  => _fiveLaneDrumsPrefab,
                    GameMode.EliteDrums     => Song.HasInstrument(Instrument.FiveLaneDrums) ? _fiveLaneDrumsPrefab : _fourLaneDrumsPrefab,
                    GameMode.ProKeys        => profilePlayer.Profile.CurrentInstrument is Instrument.ProKeys ? _proKeysPrefab : _fiveLaneKeysPrefab,
                    GameMode.ProGuitar      => _proGuitarPrefab,
                    _                       => null
                };

                if (prefab == null) return null;

                var playerObject = Instantiate(prefab, new Vector3(highwayIndex * TRACK_SPACING_X, 100f, 0f), prefab.transform.rotation);
                var newTrackPlayer = playerObject.GetComponent<TrackPlayer>();
                var trackView = _trackViewManager.CreateTrackView();
                newTrackPlayer.Initialize(highwayIndex, profilePlayer, Chart, trackView, _mixer, null);
                newTrackPlayer.GetComponentInChildren<CameraPositioner>()?.SetRaisedImmediate();

                if (oldPlayer is TrackPlayer oldTrackPlayer)
                {
                    _trackViewManager.ReplaceTrackPlayer(oldTrackPlayer, newTrackPlayer);
                }
                else
                {
                    _trackViewManager.AddTrackPlayer(newTrackPlayer);
                }

                _players[playerIndex] = newTrackPlayer;
                newPlayer = newTrackPlayer;
            }
            else
            {
                if (oldPlayer is TrackPlayer oldTrackPlayer)
                {
                    _trackViewManager.RemoveTrackPlayer(oldTrackPlayer);
                    var oldView = oldTrackPlayer.GetTrackView();
                    if (oldView != null)
                    {
                        _trackViewManager.RemoveTrackView(oldView);
                    }
                }

                if (!VocalTrack.gameObject.activeSelf)
                {
                    VocalTrack.gameObject.SetActive(true);
                    VocalTrack.transform.position = new Vector3(highwayIndex * TRACK_SPACING_X, 100, 0);
                    _trackViewManager.CreateVocalTrackView(highwayIndex);
                    var chart = profilePlayer.Profile.CurrentInstrument == Instrument.Vocals
                        ? Chart.Vocals
                        : Chart.Harmony;
                    VocalTrack.Initialize(chart, profilePlayer, Song.VocalScrollSpeedScalingFactor);

                    if (SettingsManager.Settings.KeepLyricBar.Value &&
                        SettingsManager.Settings.LyricDisplay.Value != LyricDisplayMode.Disabled)
                    {
                        _lyricBar.SetVocalPlayerLayout();
                    }
                    else
                    {
                        _lyricBar.gameObject.SetActive(false);
                    }
                }

                var vocalsPlayer = VocalTrack.CreatePlayer();
                var playerHud = _trackViewManager.CreateVocalsPlayerHUD();
                var percussionTrack = VocalTrack.CreatePercussionTrack();
                percussionTrack.TrackSpeed = VocalTrack.TrackSpeed;
                vocalsPlayer.Initialize(playerIndex, _players.Count(p => p is VocalsPlayer), profilePlayer, Chart, playerHud, percussionTrack, null, VocalTrack.TrackSpeed);
                _players[playerIndex] = vocalsPlayer;
                newPlayer = vocalsPlayer;
            }

            if (oldPlayer != null && oldPlayer.gameObject != null)
            {
                Destroy(oldPlayer.gameObject);
            }

            foreach (var stem in profilePlayer.Profile.CurrentInstrument.ToSongStems())
            {
                var transformedStem = stem;
                if (stem == SongStem.Bass && !_stemStates.ContainsKey(SongStem.Bass))
                {
                    transformedStem = SongStem.Rhythm;
                }
                if (transformedStem != _backgroundStem && _stemStates.TryGetValue(transformedStem, out var state))
                {
                    state.Total++;
                    state.Audible++;
                }
            }

            EngineManager.StarScoreThresholds = EngineManager.GetStarScoreCutoffs(_players.ConvertAll(p => p.BaseEngine.StarScoreThresholds));
            EngineManager.InitializeHappiness(true);
            _failMeter.RefreshPlayers();
            PartyPlayHUDController.ResetUnisonDisplay(_unisonDisplay, Chart);

            return newPlayer;
        }

        public bool RemovePartyPlayer(BasePlayer player, bool immediate = false)
        {
            if (player == null || !_players.Contains(player)) return false;

            if (player.Player != null)
            {
                _partyPlayActiveRoster.Remove(player.Player);
            }

            _players.Remove(player);

            var eng = player.GetEngineContainer();
            if (eng != null)
            {
                EngineManager.Unregister(eng);
            }

            if (player is TrackPlayer trackPlayer)
            {
                var view = trackPlayer.GetTrackView();
                if (immediate)
                {
                    _trackViewManager.RemoveTrackPlayer(trackPlayer);
                    if (view != null)
                    {
                        _trackViewManager.RemoveTrackView(view);
                    }
                    if (trackPlayer != null && trackPlayer.gameObject != null)
                    {
                        Destroy(trackPlayer.gameObject);
                    }

                    int trackIndex = 0;
                    foreach (var p in _players)
                    {
                        if (p is TrackPlayer tp)
                        {
                            tp.SetHighwayIndex(trackIndex);
                            trackIndex++;
                        }
                    }
                }
                else
                {
                    trackPlayer.PlayExitAnimation(() =>
                    {
                        _trackViewManager.RemoveTrackPlayer(trackPlayer);
                        if (view != null)
                        {
                            _trackViewManager.RemoveTrackView(view);
                        }
                        if (trackPlayer != null && trackPlayer.gameObject != null)
                        {
                            Destroy(trackPlayer.gameObject);
                        }

                        int reindex = 0;
                        foreach (var p in _players)
                        {
                            if (p is TrackPlayer tp)
                            {
                                tp.SetHighwayIndex(reindex);
                                reindex++;
                            }
                        }
                    });
                }
            }
            else if (player is VocalsPlayer vocalsPlayer)
            {
                Destroy(vocalsPlayer.gameObject);
                if (!_players.Any(p => p is VocalsPlayer))
                {
                    _trackViewManager.RemoveVocalTrack();
                    VocalTrack.ResetPracticeSection();
                    VocalTrack.gameObject.SetActive(false);
                }
            }

            foreach (var stem in player.Player.Profile.CurrentInstrument.ToSongStems())
            {
                var transformedStem = stem;
                if (stem == SongStem.Bass && !_stemStates.ContainsKey(SongStem.Bass))
                {
                    transformedStem = SongStem.Rhythm;
                }
                if (transformedStem != _backgroundStem && _stemStates.TryGetValue(transformedStem, out var state))
                {
                    state.Total = Math.Max(0, state.Total - 1);
                    state.Audible = Math.Max(0, state.Audible - 1);
                    if (state.Total == 0)
                    {
                        GlobalAudioHandler.SetVolumeSetting(transformedStem, state.Volume);
                    }
                }
            }

            EngineManager.StarScoreThresholds = EngineManager.GetStarScoreCutoffs(_players.ConvertAll(p => p.BaseEngine.StarScoreThresholds));
            if (_players.Count > 0)
            {
                EngineManager.InitializeHappiness(true);
            }
            _failMeter.RefreshPlayers();
            PartyPlayHUDController.ResetUnisonDisplay(_unisonDisplay, Chart);

            var scoreBox = UnityEngine.Object.FindFirstObjectByType<ScoreBox>(FindObjectsInactive.Include);
            if (scoreBox != null)
            {
                scoreBox.gameObject.SetActive(_players.Count > 0);
            }

            _lyricBar?.UpdateBackgroundDisplay();

            return true;
        }

        private static Instrument GetFirstAvailablePartyInstrument(SongEntry song)
        {
            if (song == null) return Instrument.FiveFretGuitar;

            foreach (Instrument inst in Enum.GetValues(typeof(Instrument)))
            {
                if (inst != Instrument.Band && song.HasInstrument(inst))
                {
                    return inst;
                }
            }

            return Instrument.FiveFretGuitar;
        }

        private bool _isTransitioningSong = false;

        public async UniTask TransitionPartyPlaySong(SongEntry nextSong)
        {
            if (nextSong == null || _isTransitioningSong) return;
            _isTransitioningSong = true;

            try
            {
                YargLogger.LogFormatInfo("Seamless Party Play background transition to song {0} - {1}", nextSong.Name, nextSong.Artist);

                // 1. Snapshot the joined party player roster before background loading
                var activeRoster = _partyPlayActiveRoster.ToList();

                // 2. Load chart, venue events, beatlines, background and audio on a worker thread
                // Current gameplay and venue camera continue running fluidly with zero freeze!
                SongChart nextChart = null;
                StemMixer nextMixer = null;
                BackgroundResult nextBackground = null;
                double nextSongLength = 0;
                double nextFirstNoteTime = 0;
                double nextLastNoteTime = 0;

                await UniTask.RunOnThreadPool(() =>
                {
                    try
                    {
                        nextChart = nextSong.LoadChart();
                        if (nextChart != null)
                        {
                            var isReplay = GlobalVariables.State.IsReplay || GlobalVariables.State.PlayingWithReplay;
                            if ((isReplay && ReplayInfo!.CensorshipEnabled) ||
                                (!isReplay && SettingsManager.Settings.CensorMatureContent.Value))
                            {
                                nextChart.ApplyCensorship();
                            }
                            if (nextChart.VenueTrack.IsEmpty)
                            {
                                SongChart.LoadVenueFromMilo(nextChart, nextSong);
                            }
                            if (File.Exists(VenueAutoGenerationPreset.DefaultPath))
                            {
                                var preset = new VenueAutoGenerationPreset(VenueAutoGenerationPreset.DefaultPath);
                                if (!preset.ChartHasFog(nextChart))
                                {
                                    nextChart = preset.GenerateFogEvents(nextChart);
                                }
                                if (nextChart.VenueTrack.Lighting.Count == 0)
                                {
                                    nextChart = preset.GenerateLightingEvents(nextChart);
                                }
                            }
                            SongChart.LoadLipsync(nextChart, nextSong);
                        }

                        bool censorAudio = (!GlobalVariables.State.IsReplay && SettingsManager.Settings.CensorMatureContent.Value);
                        nextMixer = nextSong.LoadAudio(GlobalVariables.State.SongSpeed, DEFAULT_VOLUME, censorAudio);

                        if (nextChart != null && nextMixer != null)
                        {
                            double audioLen = nextMixer.Length;
                            double chartLen = nextChart.GetEndTime();
                            double endT = nextChart.GetEndEvent()?.Time ?? -1;

                            if (audioLen <= chartLen) nextSongLength = chartLen;
                            else if (endT <= chartLen || audioLen <= endT) nextSongLength = audioLen;
                            else nextSongLength = endT;

                            nextFirstNoteTime = nextChart.GetFirstNoteStartTime();
                            nextLastNoteTime = nextChart.GetLastNoteEndTime();

                            nextChart.SyncTrack.GenerateBeatlines(nextSongLength + SONG_END_DELAY, true);
                        }

                        if (!SettingsManager.Settings.DisablePerSongBackgrounds.Value)
                        {
                            nextBackground = nextSong.LoadBackground();
                        }
                    }
                    catch (Exception ex)
                    {
                        YargLogger.LogException(ex, "Failed to background load song during Party Play transition");
                    }
                });

                if (nextChart == null || nextMixer == null)
                {
                    YargLogger.LogError("Failed to load song during Party Play transition, skipping to another song.");
                    var fallbackSong = SongContainer.GetRandomSong();
                    if (fallbackSong != null && fallbackSong != nextSong)
                    {
                        _isTransitioningSong = false;
                        _ = TransitionPartyPlaySong(fallbackSong);
                    }
                    return;
                }

                // 3. Seamless organic main-thread swap (no black screen fade)
                var oldRunner = _songRunner;
                var oldMetronome = _metronomeScheduler;
                var oldClap = _crowdClapScheduler;
                var oldCrowd = CrowdEventHandler;
                var oldMixer = _mixer;

                _songRunner = null;
                _metronomeScheduler = null;
                _crowdClapScheduler = null;
                CrowdEventHandler = null;
                _mixer = null;

                try
                {
                    oldRunner?.Dispose();
                    oldMetronome?.Dispose();
                    oldClap?.Dispose();
                    oldCrowd?.Dispose();
                    oldMixer?.Dispose();
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Exception disposing old playback runner");
                }

                // Assign new references
                Song = nextSong;
                Chart = nextChart;
                _mixer = nextMixer;
                SongLength = nextSongLength;
                FirstNoteTime = nextFirstNoteTime;
                LastNoteTime = nextLastNoteTime;
                GlobalVariables.State.CurrentSong = nextSong;
                GameStateFetcher.SetSongEntry(nextSong);

                _stemStates.Clear();
                foreach (var channel in _mixer.Channels)
                {
                    _stemStates.TryAdd(channel.Stem, new StemState(channel.Stem));
                }
                _backgroundStem = _stemStates.Count > 1 ? SongStem.Song : _stemStates.First().Key;

                BeatEventHandler = new BeatEventHandler(Chart.SyncTrack);
                CrowdEventHandler = new CrowdEventHandler(Chart, this);

                // Initialize fresh song runner and schedulers with standard SONG_START_DELAY pre-roll
                _songRunner = new SongRunner(
                    _mixer,
                    startTime: 0,
                    startDelay: SONG_START_DELAY,
                    GlobalVariables.State.SongSpeed,
                    Song.SongOffsetSeconds);

                _metronomeScheduler = new MetronomeScheduler(_mixer);
                _metronomeScheduler.Schedule(_songRunner, Chart.SyncTrack, SongLength);

                _crowdClapScheduler = new CrowdClapScheduler(_mixer);
                _crowdClapScheduler.Schedule(_songRunner, Chart.SyncTrack, Chart.CrowdEvents,
                    FirstNoteTime, LastNoteTime, SongLength);
                CrowdEventHandler.SetClapScheduler(_crowdClapScheduler);

                _chartLoaded?.Invoke(Chart);
                _songLoaded?.Invoke();

                // Setup crowd stem and background stem
                if (_stemStates.TryGetValue(SongStem.Crowd, out var crowdState))
                {
                    crowdState.Total = 1;
                    crowdState.Audible = 1;
                }

                if (_stemStates.TryGetValue(_backgroundStem, out var bgState))
                {
                    bgState.Total = 2;
                    bgState.Audible = 2;
                }

                // Create fresh EngineManager for the new chart
                EngineManager = new EngineManager();
                EngineManager.OnCodaStart += StartCoda;
                EngineManager.OnCodaEnd += EndCoda;
                EngineManager.OnUnisonPhraseSuccess += OnUnisonPhraseSuccess;
                BandScore = 0;
                BandCombo = 0;

                // 4. In-place reload active players or cleanly recreate if instrument changed
                var existingPlayers = _players.ToList();
                _players.Clear();

                if (VocalTrack != null && VocalTrack.gameObject.activeSelf)
                {
                    _trackViewManager.RemoveVocalTrack();
                    VocalTrack.ResetPracticeSection();
                    VocalTrack.gameObject.SetActive(false);
                }

                foreach (var player in activeRoster)
                {
                    // If the new song does not have this player's chosen instrument, they temporarily sit out for this song
                    // and will automatically drop back in when a future song has their instrument
                    if (!Song.HasInstrument(player.Profile.CurrentInstrument))
                    {
                        continue;
                    }

                    var lastHighScore = ScoreContainer.GetHighScore(Song.Hash, player.Profile.Id, player.Profile.CurrentInstrument, false)?.Score;
                    var existingTrackPlayer = existingPlayers.OfType<TrackPlayer>()
                        .FirstOrDefault(p => p.Player == player && p.MatchesInstrument(player.Profile.CurrentInstrument));

                    if (existingTrackPlayer != null && existingTrackPlayer.ReloadTrackPlayer(Chart, _mixer, lastHighScore, this))
                    {
                        _players.Add(existingTrackPlayer);
                        existingPlayers.Remove(existingTrackPlayer);

                        int highwayIndex = _players.Count(p => p is TrackPlayer) - 1;
                        existingTrackPlayer.SetHighwayIndex(highwayIndex);

                        var hasStem = false;
                        foreach (var stem in player.Profile.CurrentInstrument.ToSongStems())
                        {
                            var transformedStem = stem;
                            if (stem == SongStem.Bass && !_stemStates.ContainsKey(SongStem.Bass))
                            {
                                transformedStem = SongStem.Rhythm;
                            }
                            if (transformedStem != _backgroundStem && _stemStates.TryGetValue(transformedStem, out var state))
                            {
                                hasStem = true;
                                ++state.Total;
                                ++state.Audible;
                            }
                        }

                        if (!hasStem && _stemStates.TryGetValue(_backgroundStem, out var fallbackBgState))
                        {
                            fallbackBgState.Total += 2;
                            fallbackBgState.Audible += 2;
                        }
                    }
                    else
                    {
                        SpawnPartyPlayer(player, refreshHud: false, isTransition: true);
                    }
                }

                // Reset any lingering volume tweens and reset all stem settings in GlobalAudioHandler
                var fiVolTween = typeof(GameManager).GetField("_volumeTween", BindingFlags.Instance | BindingFlags.NonPublic);
                (fiVolTween?.GetValue(this) as Tweener)?.Kill();
                fiVolTween?.SetValue(this, null);

                foreach (var (stem, state) in _stemStates)
                {
                    GlobalAudioHandler.SetVolumeSetting(stem, state.Volume);
                    GlobalAudioHandler.SetReverbSetting(stem, false);
                    GlobalAudioHandler.SetWhammyPitchSetting(stem, 0f);
                }

                // Animate exit for any remaining old players (sitting out this song)
                foreach (var old in existingPlayers)
                {
                    if (old is TrackPlayer tp)
                    {
                        _trackViewManager.RemoveTrackPlayer(tp);
                        tp.PlayExitAnimation(() =>
                        {
                            var tv = tp.GetTrackView();
                            if (tv != null) _trackViewManager.RemoveTrackView(tv);
                            if (tp != null && tp.gameObject != null) Destroy(tp.gameObject);
                        });
                    }
                    else if (old != null && old.gameObject != null)
                    {
                        Destroy(old.gameObject);
                    }
                }

                // Reset star thresholds & happiness
                if (_players.Count > 0)
                {
                    EngineManager.StarScoreThresholds = EngineManager.GetStarScoreCutoffs(_players.ConvertAll(p => p.BaseEngine.StarScoreThresholds));
                    EngineManager.InitializeHappiness(true);
                }
                else
                {
                    EngineManager.StarScoreThresholds = Array.Empty<int>();
                }

                // Explicitly ensure all tracks are not flashing red
                foreach (var p in _players)
                {
                    if (p is TrackPlayer tp)
                    {
                        var piTrackMat = typeof(TrackPlayer).GetProperty("TrackMaterial", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (piTrackMat?.GetValue(tp) is TrackMaterial trackMat)
                        {
                            trackMat.FailState = 0f;
                        }

                        var fiPlayerFailed = typeof(TrackPlayer).GetField("PlayerHasFailed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        fiPlayerFailed?.SetValue(tp, false);
                    }
                }

                _failMeter.RefreshPlayers();

                BackgroundManager?.ApplyLoadedBackground(nextBackground);
                BackgroundManager?.SetTime(Song.SongOffsetSeconds);

                // Update Scene GameplayBehaviours, HUD, Lyrics, ScoreBox, Song Info, Pause Menu, Lighting, Cues & Post-Processing
                PartyPlayHUDController.RefreshSceneGameplayBehaviours(Chart, Song, SongLength, this, _pauseMenu, _lyricBar);

                enabled = true;
                IsSongStarted = true;
                _songStarted?.Invoke();
            }
            finally
            {
                _isTransitioningSong = false;
            }
        }

        private Vector2 _partyPlayStatusScroll;
        private Vector2 _partyPlayRosterScroll;
        private readonly Difficulty[] _debugPartyDifficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert };

        // Action queue to execute IMGUI interactions safely in LateUpdate without layout flickering
        private Action _pendingPartyAction;

        private void LateUpdate()
        {
            if (!GlobalVariables.State.IsPartyPlay) return;

            // Execute any queued GUI actions safely outside of the IMGUI draw pass
            if (_pendingPartyAction != null)
            {
                var action = _pendingPartyAction;
                _pendingPartyAction = null;
                action();
            }

            if (GetComponent<PartyPlayManager>() == null)
            {
                gameObject.AddComponent<PartyPlayManager>();
            }

            if (_debugMenus != null && !_debugMenus.Any(m => m.title == "Party Play"))
            {
                int closeIndex = _debugMenus.FindIndex(m => m.title == "Close");
                if (closeIndex >= 0)
                {
                    _debugMenus.Insert(closeIndex, ("Party Play", PartyPlayDebug));
                }
                else
                {
                    _debugMenus.Add(("Party Play", PartyPlayDebug));
                }
                _debugMenuTitles = _debugMenus.Select(m => m.title).ToArray();
            }

            // Keep native player debug list synchronized with party play drop-ins
            if (_debugPlayers == null || _debugPlayers.Length != _players.Count)
            {
                var debugPlayers = new List<string>();
                for (int i = 0; i < _players.Count; i++)
                {
                    var p = _players[i];
                    debugPlayers.Add($"{i + 1}: {p.Player.Profile.Name}");
                }
                _debugPlayers = debugPlayers.ToArray();
            }
        }

        private void PartyPlayDebug()
        {
            // 1. Session & Song Status
            using (DebugScrollView.Begin("Session Status", VerticalGroupStyle, ref _partyPlayStatusScroll, GUILayout.Height(135 * _debugGuiScale)))
            {
                GUILayout.Label($"Song: <b>{Song?.Name ?? "Unknown"}</b> by <i>{Song?.Artist ?? "Unknown"}</i>");
                int totalPlayers = PlayerContainer.Players.Count;
                GUILayout.Label($"Active Players: {_players.Count}/{totalPlayers} | Continuous Party Play");

                double currentTime = _songRunner != null ? _songRunner.SongTime : 0;
                GUILayout.Label($"Time: {currentTime:0.0}s / {SongLength:0.0}s ({((SongLength > 0) ? (currentTime / SongLength * 100) : 0):0.0}%)");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⏪ -30s", GUILayout.Height(22)))
                {
                    _pendingPartyAction = () => { if (_songRunner != null) _songRunner.SetSongTime(Math.Max(0, currentTime - 30), 0); };
                }
                if (GUILayout.Button("⏪ -10s", GUILayout.Height(22)))
                {
                    _pendingPartyAction = () => { if (_songRunner != null) _songRunner.SetSongTime(Math.Max(0, currentTime - 10), 0); };
                }
                if (GUILayout.Button("⏩ +10s", GUILayout.Height(22)))
                {
                    _pendingPartyAction = () => { if (_songRunner != null) _songRunner.SetSongTime(Math.Min(SongLength - 1, currentTime + 10), 0); };
                }
                if (GUILayout.Button("⏩ +30s", GUILayout.Height(22)))
                {
                    _pendingPartyAction = () => { if (_songRunner != null) _songRunner.SetSongTime(Math.Min(SongLength - 1, currentTime + 30), 0); };
                }
                if (GUILayout.Button("🏁 Near End", GUILayout.Height(22)))
                {
                    _pendingPartyAction = () => { if (_songRunner != null) _songRunner.SetSongTime(Math.Max(0, SongLength - 5), 0); };
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⏭ Next Song (Skip)", GUILayout.Height(24)))
                {
                    _pendingPartyAction = () =>
                    {
                        var next = SongContainer.GetRandomSong();
                        if (next != null) _ = TransitionPartyPlaySong(next);
                    };
                }
                if (GUILayout.Button("🔄 Restart Song", GUILayout.Height(24)))
                {
                    _pendingPartyAction = () =>
                    {
                        if (Song != null) _ = TransitionPartyPlaySong(Song);
                    };
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                PartyPlayManager.ShowOverlayButtons = GUILayout.Toggle(
                    PartyPlayManager.ShowOverlayButtons,
                    " Show Quick Overlay Buttons (⏭ Skip & 🏁 Near End)"
                );
            }

            GUILayout.Space(4);

            // 2. Band & Performance Cheats
            GUILayout.BeginVertical("Band & Cheats", VerticalGroupStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💚 Max Happiness", GUILayout.Height(22)))
            {
                _pendingPartyAction = () => EngineManager?.InitializeHappiness(true);
            }
            if (GUILayout.Button("🌟 Full Band SP", GUILayout.Height(22)))
            {
                _pendingPartyAction = () =>
                {
                    foreach (var p in _players)
                    {
                        var eng = p.BaseEngine;
                        if (eng != null) eng.BaseStats.StarPowerTickAmount = eng.TicksPerFullSpBar;
                    }
                };
            }
            if (GUILayout.Button("⚡ Trigger SP", GUILayout.Height(22)))
            {
                _pendingPartyAction = () =>
                {
                    var mi = typeof(BaseEngine).GetMethod("ActivateStarPower", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    foreach (var p in _players)
                    {
                        if (p.BaseEngine != null)
                        {
                            p.BaseEngine.BaseStats.StarPowerTickAmount = p.BaseEngine.TicksPerFullSpBar;
                            mi?.Invoke(p.BaseEngine, null);
                        }
                    }
                };
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // 3. Complete Player Roster
            var allYargPlayers = PlayerContainer.Players.ToList();
            using (DebugScrollView.Begin($"Player Roster ({_players.Count}/{allYargPlayers.Count})", VerticalGroupStyle, ref _partyPlayRosterScroll, GUILayout.Height(170 * _debugGuiScale)))
            {
                if (allYargPlayers.Count == 0)
                {
                    GUILayout.Label("No players connected.");
                }
                else
                {
                    var allowedInstruments = new List<Instrument>();
                    if (Song != null)
                    {
                        foreach (Instrument inst in Enum.GetValues(typeof(Instrument)))
                        {
                            if (Song.HasInstrument(inst)) allowedInstruments.Add(inst);
                        }
                    }
                    if (allowedInstruments.Count == 0) allowedInstruments.Add(Instrument.FiveFretGuitar);

                    for (int i = 0; i < allYargPlayers.Count; i++)
                    {
                        var yp = allYargPlayers[i];
                        var prof = yp.Profile;
                        var activePlayer = _players.FirstOrDefault(p => p.Player == yp);
                        bool isPlaying = activePlayer != null;

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.BeginHorizontal();

                        GUI.color = isPlaying ? Color.green : Color.gray;
                        GUILayout.Label(isPlaying ? "●" : "○", GUILayout.Width(14));
                        GUI.color = Color.white;

                        GUILayout.Label($"<b>{prof.Name}</b>", GUILayout.Width(100));

                        if (!allowedInstruments.Contains(prof.CurrentInstrument))
                        {
                            prof.CurrentInstrument = allowedInstruments[0];
                        }
                        int instIdx = allowedInstruments.IndexOf(prof.CurrentInstrument);
                        if (GUILayout.Button($"🎸 {prof.CurrentInstrument}", GUILayout.Width(120), GUILayout.Height(20)))
                        {
                            var targetPlayer = activePlayer;
                            var capturedYP = yp;
                            _pendingPartyAction = () =>
                            {
                                int nextIdx = (instIdx + 1) % allowedInstruments.Count;
                                prof.CurrentInstrument = allowedInstruments[nextIdx];
                                prof.GameMode = prof.CurrentInstrument.ToNativeGameMode();
                                if (targetPlayer != null) ReplacePartyPlayer(targetPlayer, capturedYP);
                            };
                        }

                        foreach (var diff in _debugPartyDifficulties)
                        {
                            GUI.backgroundColor = prof.CurrentDifficulty == diff ? Color.green : Color.white;
                            if (GUILayout.Button(diff.ToString().Substring(0, 1), GUILayout.Width(22), GUILayout.Height(20)))
                            {
                                var targetPlayer = activePlayer;
                                var capturedYP = yp;
                                var targetDiff = diff;
                                _pendingPartyAction = () =>
                                {
                                    prof.CurrentDifficulty = targetDiff;
                                    if (targetPlayer != null) ReplacePartyPlayer(targetPlayer, capturedYP);
                                };
                            }
                        }
                        GUI.backgroundColor = Color.white;

                        if (isPlaying)
                        {
                            var targetPlayer = activePlayer;
                            if (GUILayout.Button("🚪 Drop Out", GUILayout.Width(75), GUILayout.Height(20)))
                            {
                                _pendingPartyAction = () => RemovePartyPlayer(targetPlayer, false);
                            }
                        }
                        else
                        {
                            var capturedYP = yp;
                            GUI.backgroundColor = Color.cyan;
                            if (GUILayout.Button("➕ Drop In", GUILayout.Width(75), GUILayout.Height(20)))
                            {
                                _pendingPartyAction = () =>
                                {
                                    prof.GameMode = prof.CurrentInstrument.ToNativeGameMode();
                                    SpawnPartyPlayer(capturedYP);
                                };
                            }
                            GUI.backgroundColor = Color.white;
                        }

                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }
                }
            }
        }
    }
}
