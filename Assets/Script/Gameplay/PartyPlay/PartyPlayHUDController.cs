using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Gameplay.HUD;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Helpers.UI;
using YARG.Integration;
using YARG.Menu.Persistent;
using YARG.Settings;
using YARG.Song;
using PlasticBand.Haptics;

namespace YARG.Gameplay.PartyPlay
{
    public static class PartyPlayHUDController
    {
        private static ScoreBox _cachedScoreBox;
        private static SongInfoText _cachedSongInfoText;
        private static UnisonDisplay _cachedUnisonDisplay;
        private static List<GameplayBehaviour> _cachedVenueBehaviours;

        public static void RefreshSceneGameplayBehaviours(
            SongChart chart,
            SongEntry song,
            double songLength,
            GameManager gameManager,
            PauseMenuManager pauseMenu,
            LyricBar lyricBar)
        {
            // 1. Reset ScoreBox (using cached reference)
            if (_cachedScoreBox == null || _cachedScoreBox.gameObject == null)
            {
                _cachedScoreBox = UnityEngine.Object.FindFirstObjectByType<ScoreBox>(FindObjectsInactive.Include);
            }
            ResetScoreBox(_cachedScoreBox, songLength, gameManager);

            // 2. Reset Lyrics
            ResetLyrics(lyricBar, chart);

            // 3. Update Pause Menu
            UpdatePauseMenuSongInfo(pauseMenu, song);

            // 4. Update Song Info Text Card (using cached reference)
            if (_cachedSongInfoText == null || _cachedSongInfoText.gameObject == null)
            {
                _cachedSongInfoText = UnityEngine.Object.FindFirstObjectByType<SongInfoText>(FindObjectsInactive.Include);
            }
            ShowSongInfoCard(_cachedSongInfoText, song);

            // 5. Reset Unison Display (using cached reference)
            if (_cachedUnisonDisplay == null || _cachedUnisonDisplay.gameObject == null)
            {
                _cachedUnisonDisplay = UnityEngine.Object.FindFirstObjectByType<UnisonDisplay>(FindObjectsInactive.Include);
            }
            ResetUnisonDisplay(_cachedUnisonDisplay, chart);

            // 6. Explicitly reset and refresh Venue Managers, Lighting, Character Lipsync/Animations, and Monitors
            double visualTime = gameManager != null ? gameManager.VisualTime : 0.0;

            // Reset Master Lighting controller and monitor state
            MasterLightingController.CurrentLightingCue = null;
            MasterLightingController.CurrentFogState = MasterLightingController.FogState.Off;
            MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Off;
            MasterLightingController.CurrentPostProcessing = new PostProcessingEvent(PostProcessingType.Default, 0, 0);

            // Set static properties on MasterLightingGameplayMonitor and DataStreamGameplayMonitor
            typeof(MasterLightingGameplayMonitor).GetProperty("Venue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, chart.VenueTrack);
            typeof(MasterLightingGameplayMonitor).GetProperty("LightingIndex", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, 0);

            typeof(DataStreamGameplayMonitor).GetProperty("Venue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, chart.VenueTrack);
            typeof(DataStreamGameplayMonitor).GetProperty("LightingIndex", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, 0);

            // Safely reset all monitor instances without throwing IndexOutOfRangeException on missing instruments/vocals
            var mlMonitors = UnityEngine.Object.FindObjectsByType<MasterLightingGameplayMonitor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var monitor in mlMonitors)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                typeof(MasterLightingGameplayMonitor).GetField("_sync", flags)?.SetValue(monitor, chart.SyncTrack);
                typeof(MasterLightingGameplayMonitor).GetField("_vocals", flags)?.SetValue(monitor, chart.Vocals?.Parts != null && chart.Vocals.Parts.Count > 0 ? chart.Vocals.Parts[0].NotePhrases : null);
                typeof(MasterLightingGameplayMonitor).GetField("_drums", flags)?.SetValue(monitor, chart.ProDrums?.GetDifficulty(YARG.Core.Difficulty.Expert));
                typeof(MasterLightingGameplayMonitor).GetField("_guitar", flags)?.SetValue(monitor, chart.FiveFretGuitar?.GetDifficulty(YARG.Core.Difficulty.Expert));
                typeof(MasterLightingGameplayMonitor).GetField("_bass", flags)?.SetValue(monitor, chart.FiveFretBass?.GetDifficulty(YARG.Core.Difficulty.Expert));
                typeof(MasterLightingGameplayMonitor).GetField("_keys", flags)?.SetValue(monitor, chart.Keys?.GetDifficulty(YARG.Core.Difficulty.Expert));

                typeof(MasterLightingGameplayMonitor).GetField("_stageIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_syncIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_vocalsIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_guitarIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_bassIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_drumIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_postProcessingIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_keysIndex", flags)?.SetValue(monitor, 0);
                typeof(MasterLightingGameplayMonitor).GetField("_guitarEndCheckIndex", flags)?.SetValue(monitor, -1);
                typeof(MasterLightingGameplayMonitor).GetField("_bassEndCheckIndex", flags)?.SetValue(monitor, -1);
                typeof(MasterLightingGameplayMonitor).GetField("_drumEndCheckIndex", flags)?.SetValue(monitor, -1);
                typeof(MasterLightingGameplayMonitor).GetField("_keysEndCheckIndex", flags)?.SetValue(monitor, -1);
            }

            var dsMonitors = UnityEngine.Object.FindObjectsByType<DataStreamGameplayMonitor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var monitor in dsMonitors)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                typeof(DataStreamGameplayMonitor).GetField("_sync", flags)?.SetValue(monitor, chart.SyncTrack);
                typeof(DataStreamGameplayMonitor).GetField("_vocalsIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_harmony0Index", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_harmony1Index", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_harmony2Index", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_keysIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_syncIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_bpmIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_drumIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_guitarIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_bassIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_stageIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_performerStartIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_performerEndIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_postProcessingIndex", flags)?.SetValue(monitor, 0);
                typeof(DataStreamGameplayMonitor).GetField("_cameraCutIndex", flags)?.SetValue(monitor, 0);
            }

            var miChart = typeof(GameplayBehaviour).GetMethod("OnChartLoaded", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var miSong = typeof(GameplayBehaviour).GetMethod("OnSongLoaded", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var miStart = typeof(GameplayBehaviour).GetMethod("OnSongStarted", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var venueManagers = new Type[]
            {
                typeof(YARG.Venue.VenueCamera.CameraManager),
                typeof(YARG.Venue.LightManager),
                typeof(YARG.Venue.Characters.CharacterManager),
                typeof(YARG.Venue.Stage.StageManager),
                typeof(YARG.Venue.VenueAnimator)
            };

            var freshBehaviours = new List<GameplayBehaviour>();
            foreach (var managerType in venueManagers)
            {
                var instances = UnityEngine.Object.FindObjectsByType(managerType, FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var inst in instances)
                {
                    if (inst is GameplayBehaviour gb)
                    {
                        freshBehaviours.Add(gb);
                    }
                }
            }
            _cachedVenueBehaviours = freshBehaviours;

            foreach (var behaviour in _cachedVenueBehaviours)
            {
                if (behaviour == null || behaviour.gameObject == null) continue;

                try
                {
                    if (behaviour is YARG.Venue.VenueAnimator venueAnim)
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                        typeof(YARG.Venue.VenueAnimator).GetField("_queue", flags)?.SetValue(venueAnim, new YARG.Venue.AnimatorCommandQueue());
                        var pendingList = typeof(YARG.Venue.VenueAnimator).GetField("_pendingBoolOffs", flags)?.GetValue(venueAnim) as System.Collections.IList;
                        pendingList?.Clear();
                    }
                    else if (behaviour is YARG.Venue.VenueCamera.CameraManager camBefore)
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                        typeof(YARG.Venue.VenueCamera.CameraManager).GetField("_currentCutIndex", flags)?.SetValue(camBefore, 0);
                        typeof(YARG.Venue.VenueCamera.CameraManager).GetField("_currentEventIndex", flags)?.SetValue(camBefore, 0);
                    }

                    miChart?.Invoke(behaviour, new object[] { chart });
                    miSong?.Invoke(behaviour, null);
                    miStart?.Invoke(behaviour, null);

                    if (behaviour is YARG.Venue.VenueCamera.CameraManager cam)
                    {
                        cam.ResetAllEffectsToDefault();
                        cam.ResetTime(visualTime);
                    }
                    else if (behaviour is YARG.Venue.Characters.CharacterManager charMgr)
                    {
                        charMgr.ResetTime(visualTime);
                    }
                }
                catch (Exception ex)
                {
                    YARG.Core.Logging.YargLogger.LogException(ex, $"Failed to refresh Venue Manager {behaviour.GetType().Name}");
                }
            }

            // Ensure all VenueLights and NeonLightManagers are linked to the active LightManager
            var activeLightMgr = UnityEngine.Object.FindFirstObjectByType<YARG.Venue.LightManager>(FindObjectsInactive.Include);
            if (activeLightMgr != null)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var venueLights = UnityEngine.Object.FindObjectsByType<YARG.Venue.VenueLight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var fiLightMgr = typeof(YARG.Venue.VenueLight).GetField("_lightManager", flags);
                foreach (var vl in venueLights)
                {
                    fiLightMgr?.SetValue(vl, activeLightMgr);
                }

                var neonManagers = UnityEngine.Object.FindObjectsByType<YARG.Venue.NeonLightManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var fiNeonLightMgr = typeof(YARG.Venue.NeonLightManager).GetField("_lightManager", flags);
                foreach (var nlm in neonManagers)
                {
                    fiNeonLightMgr?.SetValue(nlm, activeLightMgr);
                }
            }

            if (chart.VenueTrack?.Lighting != null && chart.VenueTrack.Lighting.Count > 0)
            {
                MasterLightingController.CurrentLightingCue = chart.VenueTrack.Lighting[0];
            }
        }

        public static void ResetScoreBox(ScoreBox scoreBox, double songLength, GameManager gameManager)
        {
            if (scoreBox == null) return;

            var fiBandScore = typeof(ScoreBox).GetField("_bandScore", BindingFlags.Instance | BindingFlags.NonPublic);
            fiBandScore?.SetValue(scoreBox, 0);

            var fiBandCombo = typeof(ScoreBox).GetField("_bandCombo", BindingFlags.Instance | BindingFlags.NonPublic);
            fiBandCombo?.SetValue(scoreBox, 0);

            var fiScoreText = typeof(ScoreBox).GetField("_scoreText", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiScoreText?.GetValue(scoreBox) is TextMeshProUGUI scoreText)
            {
                scoreText.text = "<mspace=0.538em>0";
            }

            var fiComboText = typeof(ScoreBox).GetField("_bandComboText", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiComboText?.GetValue(scoreBox) is TextMeshProUGUI comboText)
            {
                comboText.text = "<mspace=0.538em>0";
            }

            var fiProgressBar = typeof(ScoreBox).GetField("_songProgressBar", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiProgressBar?.GetValue(scoreBox) is ProgressBarFadedEdge progressBar)
            {
                progressBar.SetProgress(0f);
            }

            var fiTimer = typeof(ScoreBox).GetField("_songTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiTimer?.GetValue(scoreBox) is TextMeshProUGUI timerText)
            {
                timerText.text = string.Empty;
            }

            var fiCountUp = typeof(ScoreBox).GetField("_displayedCountUpSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
            fiCountUp?.SetValue(scoreBox, -1);

            var fiCountDown = typeof(ScoreBox).GetField("_displayedCountDownSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
            fiCountDown?.SetValue(scoreBox, -1);

            var fiStarScore = typeof(ScoreBox).GetField("_starScoreDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiStarScore?.GetValue(scoreBox) is StarScoreDisplay starScore)
            {
                starScore.SetStars(0f);
            }

            // Trigger OnSongStarted to recalculate song length string & time format
            var miOnSongStarted = typeof(ScoreBox).GetMethod("OnSongStarted", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            miOnSongStarted?.Invoke(scoreBox, null);

            if (GlobalVariables.State.IsPartyPlay)
            {
                scoreBox.gameObject.SetActive(gameManager != null && gameManager.Players.Count > 0);
            }
        }

        public static void ResetLyrics(LyricBar lyricBar, SongChart chart)
        {
            if (lyricBar == null || chart == null) return;

            var fiCanvas = typeof(LyricBar).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiCanvas?.GetValue(lyricBar) is CanvasGroup canvasGroup)
            {
                for (int i = canvasGroup.transform.childCount - 1; i >= 0; i--)
                {
                    var child = canvasGroup.transform.GetChild(i);
                    if (child != null && child.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(child.gameObject);
                    }
                }
                canvasGroup.alpha = 0f;
            }

            var fiTimings = typeof(LyricBar).GetField("_fadeStartTimings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiTimings?.GetValue(lyricBar) is System.Collections.IList list)
            {
                list.Clear();
            }

            var fiFadeIndex = typeof(LyricBar).GetField("_fadeIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            fiFadeIndex?.SetValue(lyricBar, 0);

            var fiLyricObjects = typeof(LyricBar).GetField("_lyricTextObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            fiLyricObjects?.SetValue(lyricBar, null);

            if (chart.Lyrics.Phrases.Count > 0 && SettingsManager.Settings.LyricDisplay.Value != LyricDisplayMode.Disabled)
            {
                lyricBar.gameObject.SetActive(true);
                var miOnChart = typeof(LyricBar).GetMethod("OnChartLoaded", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                miOnChart?.Invoke(lyricBar, new object[] { chart });
            }
            else
            {
                lyricBar.gameObject.SetActive(false);
            }
        }

        public static void ResetUnisonDisplay(UnisonDisplay unisonDisplay, SongChart chart)
        {
            if (unisonDisplay == null) return;

            var fiPhrases = typeof(UnisonDisplay).GetField("_phrases", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiPhrases?.GetValue(unisonDisplay) is System.Collections.IList phraseList)
            {
                phraseList.Clear();
            }

            var fiUnisonState = typeof(UnisonDisplay).GetField("_unisonState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiUnisonState?.GetValue(unisonDisplay) is System.Collections.IDictionary stateDict)
            {
                stateDict.Clear();
            }

            var fiPhraseIndex = typeof(UnisonDisplay).GetField("_currentPhraseIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            fiPhraseIndex?.SetValue(unisonDisplay, 0);

            var fiVisualTime = typeof(UnisonDisplay).GetField("_lastVisualTime", BindingFlags.Instance | BindingFlags.NonPublic);
            fiVisualTime?.SetValue(unisonDisplay, 0.0);

            var miOnSongStarted = typeof(UnisonDisplay).GetMethod("OnSongStarted", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            miOnSongStarted?.Invoke(unisonDisplay, null);
        }

        public static void ShowSongInfoCard(SongInfoText songInfoText, SongEntry song)
        {
            if (songInfoText == null || song == null) return;

            songInfoText.gameObject.SetActive(true);
            songInfoText.StopAllCoroutines();

            var fiText = typeof(SongInfoText).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic);
            var fiGroup = typeof(SongInfoText).GetField("_canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic);

            if (fiText?.GetValue(songInfoText) is TextMeshProUGUI textComp &&
                fiGroup?.GetValue(songInfoText) is CanvasGroup canvasGroup)
            {
                var lines = SongToText.ToStyled(SongToText.FORMAT_LONG, song);
                string finalText = "";
                foreach (var line in lines)
                {
                    string alpha = line.Style switch
                    {
                        SongToText.Style.Header => "",
                        SongToText.Style.SubHeader => "<alpha=#90>",
                        _ => "<alpha=#66>"
                    };

                    string text = line.Text;
                    if (!string.IsNullOrEmpty(alpha))
                    {
                        text = text.Replace("</color>", $"</color>{alpha}");
                    }

                    finalText += line.Style switch
                    {
                        SongToText.Style.Header => $"<size=100%>{alpha}<font-weight=800>{text}</font-weight></size>",
                        SongToText.Style.SubHeader => $"<size=90%>{alpha}<i><font-weight=600>{text}</font-weight></i></size>",
                        _ => $"<size=80%>{alpha}<i><font-weight=600>{text}</font-weight></i></size>"
                    } + "\n";
                }

                textComp.text = finalText;
                canvasGroup.DOKill();
                canvasGroup.alpha = 1f;

                if (!SettingsManager.Settings.KeepSongInfoVisible.Value)
                {
                    canvasGroup.DOFade(0f, 1f).SetDelay(8f).SetUpdate(true);
                }
            }
        }

        public static void UpdatePauseMenuSongInfo(PauseMenuManager pauseMenu, SongEntry song)
        {
            if (song == null) return;
            if (pauseMenu == null)
            {
                pauseMenu = UnityEngine.Object.FindFirstObjectByType<PauseMenuManager>(FindObjectsInactive.Include);
            }
            if (pauseMenu == null) return;

            var fiAlbum = typeof(PauseMenuManager).GetField("_albumText", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiAlbum?.GetValue(pauseMenu) is TextMeshProUGUI albumText) albumText.text = song.Album;

            var fiSong = typeof(PauseMenuManager).GetField("_songText", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiSong?.GetValue(pauseMenu) is TextMeshProUGUI songText) songText.text = song.Name;

            var fiArtist = typeof(PauseMenuManager).GetField("_artistText", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiArtist?.GetValue(pauseMenu) is TextMeshProUGUI artistText) artistText.text = song.Artist;

            var fiSourceText = typeof(PauseMenuManager).GetField("_sourceText", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiSourceText?.GetValue(pauseMenu) is TextMeshProUGUI sourceText) sourceText.text = SongSources.SourceToGameName(song.Source);

            var fiSourceIcon = typeof(PauseMenuManager).GetField("_sourceIcon", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiSourceIcon?.GetValue(pauseMenu) is Image sourceIcon) sourceIcon.sprite = SongSources.SourceToIcon(song.Source);

            var fiAlbumCover = typeof(PauseMenuManager).GetField("_albumCover", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiAlbumCover?.GetValue(pauseMenu) is RawImage albumCover)
            {
                albumCover.LoadAlbumCover(song, System.Threading.CancellationToken.None);
            }
        }
    }
}
