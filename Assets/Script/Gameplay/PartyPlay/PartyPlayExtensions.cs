using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Engine;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Playback;
using YARG.Player;

namespace YARG.Gameplay.PartyPlay
{
    public static class PartyPlayExtensions
    {
        public static int GetPlayerIndex(this BasePlayer player)
        {
            if (player == null) return 0;
            var fi = typeof(BasePlayer).GetField("Index", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? typeof(BasePlayer).GetField("<Index>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            return (int)(fi?.GetValue(player) ?? 0);
        }

        public static EngineManager.EngineContainer GetEngineContainer(this BasePlayer player)
        {
            if (player == null) return null;
            var fi = typeof(BasePlayer).GetField("EngineContainer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return fi?.GetValue(player) as EngineManager.EngineContainer;
        }

        public static TrackView GetTrackView(this TrackPlayer player)
        {
            if (player == null) return null;
            var fi = typeof(TrackPlayer).GetProperty("TrackView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return fi?.GetValue(player) as TrackView;
        }

        public static VocalsPlayerHUD GetPlayerHud(this VocalsPlayer player)
        {
            if (player == null) return null;
            var fi = typeof(VocalsPlayer).GetField("_hud", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return fi?.GetValue(player) as VocalsPlayerHUD;
        }

        public static VocalPercussionTrack GetPercussionTrack(this VocalsPlayer player)
        {
            if (player == null) return null;
            var fi = typeof(VocalsPlayer).GetField("_percussionTrack", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return fi?.GetValue(player) as VocalPercussionTrack;
        }

        public static void RefreshPlayers(this FailMeter failMeter)
        {
            if (failMeter == null) return;

            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var gm = typeof(FailMeter).GetField("_gameManager", flags)?.GetValue(failMeter) as GameManager;
            if (gm == null)
            {
                gm = UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            }
            if (gm == null || gm.EngineManager == null) return;

            // Kill all previous tweens on players and meter elements
            var prevPlayerTweens = typeof(FailMeter).GetField("_playerHappinessTweeners", flags)?.GetValue(failMeter) as Tweener[];
            if (prevPlayerTweens != null)
            {
                foreach (var t in prevPlayerTweens) t?.Kill();
            }
            var prevNeedleTweens = typeof(FailMeter).GetField("_needleHappinessTweeners", flags)?.GetValue(failMeter) as Tweener[];
            if (prevNeedleTweens != null)
            {
                foreach (var t in prevNeedleTweens) t?.Kill();
            }
            var prevXPosTweens = typeof(FailMeter).GetField("_xposTweeners", flags)?.GetValue(failMeter) as Tweener[];
            if (prevXPosTweens != null)
            {
                foreach (var t in prevXPosTweens) t?.Kill();
            }

            (typeof(FailMeter).GetField("_meterRedTweener", flags)?.GetValue(failMeter) as Tweener)?.Kill();
            (typeof(FailMeter).GetField("_meterYellowTweener", flags)?.GetValue(failMeter) as Tweener)?.Kill();
            (typeof(FailMeter).GetField("_meterGreenTweener", flags)?.GetValue(failMeter) as Tweener)?.Kill();
            (typeof(FailMeter).GetField("_bandFillTweener", flags)?.GetValue(failMeter) as Tweener)?.Kill();
            (typeof(FailMeter).GetField("_meterPositionTweener", flags)?.GetValue(failMeter) as Tweener)?.Kill();
            (typeof(FailMeter).GetField("_meterGlowFillTweener", flags)?.GetValue(failMeter) as Tweener)?.Kill();
            (typeof(FailMeter).GetField("_meterGlowPulseSequence", flags)?.GetValue(failMeter) as Sequence)?.Kill();

            // Destroy existing player slider GameObjects (leaving prefabs intact)
            var prevPlayerSliders = typeof(FailMeter).GetField("_playerSliders", flags)?.GetValue(failMeter) as Slider[];
            if (prevPlayerSliders != null)
            {
                foreach (var s in prevPlayerSliders)
                {
                    if (s != null && s.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(s.gameObject);
                    }
                }
            }
            var prevNeedleSliders = typeof(FailMeter).GetField("_needleSliders", flags)?.GetValue(failMeter) as Slider[];
            if (prevNeedleSliders != null)
            {
                foreach (var s in prevNeedleSliders)
                {
                    if (s != null && s.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(s.gameObject);
                    }
                }
            }

            // Reset meter container position if needed
            var meterContainer = typeof(FailMeter).GetField("_meterContainer", flags)?.GetValue(failMeter) as GameObject;
            if (meterContainer != null)
            {
                meterContainer.transform.localPosition = new Vector3(meterContainer.transform.localPosition.x, 0f, meterContainer.transform.localPosition.z);
            }

            // Clear the private _players list
            var playersList = typeof(FailMeter).GetField("_players", flags)?.GetValue(failMeter) as List<EngineManager.EngineContainer>;
            playersList?.Clear();

            bool hasPlayers = gm.Players != null && gm.Players.Count > 0;
            if (GlobalVariables.State.IsPartyPlay && !hasPlayers)
            {
                failMeter.gameObject.SetActive(false);
                return;
            }

            failMeter.gameObject.SetActive(true);
            failMeter.enabled = true;

            // Re-initialize cleanly
            failMeter.Initialize(gm.EngineManager, gm);
            failMeter.SetActive(true);

            // Invalidate cached happiness so meter fill and player needles immediately update on frame 1
            typeof(FailMeter).GetField("_previousBandHappiness", flags)?.SetValue(failMeter, -1f);

            // Force initial visual update of meter fill and position
            var miUpdateMeterFill = typeof(FailMeter).GetMethod("UpdateMeterFill", flags);
            miUpdateMeterFill?.Invoke(failMeter, null);
        }



        public static void SetHighwayIndex(this TrackPlayer player, int index)
        {
            if (player == null) return;

            var fi = typeof(TrackPlayer).GetField("HighwayIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            fi?.SetValue(player, index);

            player.transform.position = new Vector3(index * 100f, 100f, 0f);
        }

        public static void PlayExitAnimation(this TrackPlayer player, Action onComplete)
        {
            if (player == null)
            {
                onComplete?.Invoke();
                return;
            }

            var trackView = player.GetTrackView();
            if (trackView != null)
            {
                trackView.gameObject.SetActive(false);
            }

            var fiCombo = typeof(TrackPlayer).GetField("ComboMeter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fiCombo?.GetValue(player) is MonoBehaviour combo) combo.gameObject.SetActive(false);

            var fiStar = typeof(TrackPlayer).GetField("StarpowerBar", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fiStar?.GetValue(player) is MonoBehaviour star) star.gameObject.SetActive(false);

            var cameraPos = player.GetComponentInChildren<YARG.Gameplay.Visuals.CameraPositioner>();
            if (cameraPos != null)
            {
                cameraPos.Lower(false);
            }

            DOVirtual.DelayedCall(0.55f, () => onComplete?.Invoke(), false);
        }

        public static void PlayEnterAnimation(this TrackPlayer player)
        {
            if (player == null) return;
            var cameraPos = player.GetComponentInChildren<YARG.Gameplay.Visuals.CameraPositioner>();
            if (cameraPos != null)
            {
                cameraPos.Raise(false);
            }
        }

        public static bool MatchesInstrument(this BasePlayer player, Instrument instrument)
        {
            if (player == null) return false;
            return player switch
            {
                FiveFretGuitarPlayer => instrument is Instrument.FiveFretGuitar or Instrument.FiveFretBass or Instrument.FiveFretRhythm or Instrument.FiveFretCoopGuitar or Instrument.Keys,
                DrumsPlayer => instrument is Instrument.FourLaneDrums or Instrument.FiveLaneDrums or Instrument.ProDrums or Instrument.EliteDrums,
                ProKeysPlayer => instrument is Instrument.ProKeys,
                YARG.Assets.Script.Gameplay.Player.FiveLaneKeysPlayer => instrument is Instrument.Keys,
                VocalsPlayer => instrument is Instrument.Vocals or Instrument.Harmony,
                _ => false
            };
        }

        public static bool ReloadTrackPlayer(this TrackPlayer player, YARG.Core.Chart.SongChart chart, YARG.Core.Audio.StemMixer mixer, int? highScore, GameManager gm)
        {
            if (player == null || chart == null) return false;
            if (!player.MatchesInstrument(player.Player.Profile.CurrentInstrument)) return false;

            try
            {
                var tpType = player.GetType();
                while (tpType != null && (!tpType.IsGenericType || tpType.GetGenericTypeDefinition() != typeof(TrackPlayer<,>)))
                {
                    tpType = tpType.BaseType;
                }

                if (tpType == null) return false;

                // 1. Reset Visuals (returns all spawned notes, beatlines, effects to their pools)
                var miResetVisuals = typeof(TrackPlayer).GetMethod("ResetVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
                miResetVisuals?.Invoke(player, null);

                // 2. Update references on base TrackPlayer
                var fiChart = tpType.GetField("Chart", BindingFlags.Instance | BindingFlags.NonPublic);
                fiChart?.SetValue(player, chart);

                var fiMixer = typeof(BasePlayer).GetField("Mixer", BindingFlags.Instance | BindingFlags.NonPublic);
                fiMixer?.SetValue(player, mixer);

                var fiHighScore = typeof(BasePlayer).GetField("LastHighScore", BindingFlags.Instance | BindingFlags.NonPublic);
                fiHighScore?.SetValue(player, highScore);

                var fiNewHighScoreShown = tpType.GetField("_newHighScoreShown", BindingFlags.Instance | BindingFlags.NonPublic);
                fiNewHighScoreShown?.SetValue(player, false);

                // 3. Get new notes from chart
                var miGetNotes = tpType.GetMethod("GetNotes", BindingFlags.Instance | BindingFlags.NonPublic);
                var originalNoteTrack = miGetNotes?.Invoke(player, new object[] { chart });

                if (originalNoteTrack == null) return false;

                // Apply modifiers
                player.Player.Profile.ApplyModifiers(originalNoteTrack as dynamic, chart.SyncTrack);

                var piOriginalNoteTrack = tpType.GetProperty("OriginalNoteTrack", BindingFlags.Instance | BindingFlags.NonPublic);
                piOriginalNoteTrack?.SetValue(player, originalNoteTrack);

                var piNoteTrack = tpType.GetProperty("NoteTrack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                piNoteTrack?.SetValue(player, originalNoteTrack);

                var piNotes = tpType.GetProperty("Notes", BindingFlags.Instance | BindingFlags.NonPublic);
                var fiNotesList = originalNoteTrack.GetType().GetProperty("Notes");
                var notesVal = fiNotesList?.GetValue(originalNoteTrack);
                piNotes?.SetValue(player, notesVal);

                // 4. Create new Engine
                var miCreateEngine = tpType.GetMethod("CreateEngine", BindingFlags.Instance | BindingFlags.NonPublic);
                var newEngine = miCreateEngine?.Invoke(player, null) as BaseEngine;
                if (newEngine == null) return false;

                var piEngine = tpType.GetProperty("Engine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                piEngine?.SetValue(player, newEngine);

                var fiComboMeter = typeof(TrackPlayer).GetField("ComboMeter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var comboMeter = fiComboMeter?.GetValue(player) as ComboMeter;
                comboMeter?.Initialize(player.Player.EnginePreset, newEngine.BaseParameters.MaxMultiplier, gm.Players.Count > 1);

                var miOnComboInc = typeof(BasePlayer).GetMethod("OnComboIncrement", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var miOnComboReset = typeof(BasePlayer).GetMethod("OnComboReset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (miOnComboInc != null)
                {
                    var delInc = (BaseEngine.ComboIncrementEvent)Delegate.CreateDelegate(typeof(BaseEngine.ComboIncrementEvent), player, miOnComboInc);
                    newEngine.OnComboIncrement += delInc;
                }

                if (miOnComboReset != null)
                {
                    var delReset = (BaseEngine.ComboResetEvent)Delegate.CreateDelegate(typeof(BaseEngine.ComboResetEvent), player, miOnComboReset);
                    newEngine.OnComboReset += delReset;
                }

                var fiSunburst = typeof(TrackPlayer).GetField("SunburstEffects", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fiSunburst?.GetValue(player) is SunburstEffects sunburst && gm.BeatEventHandler?.Visual != null)
                {
                    gm.BeatEventHandler.Visual.Subscribe(sunburst.PulseSunburst, BeatEventType.StrongBeat);
                }

                var miOnFailed = typeof(BasePlayer).GetMethod("OnPlayerFailed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var miOnRevived = typeof(BasePlayer).GetMethod("OnPlayerRevived", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (miOnFailed != null && gm.EngineManager != null)
                {
                    var delFailed = (EngineManager.PlayerFailed)Delegate.CreateDelegate(typeof(EngineManager.PlayerFailed), player, miOnFailed);
                    gm.EngineManager.OnPlayerFailed += delFailed;
                }
                if (miOnRevived != null && gm.EngineManager != null)
                {
                    var delRevived = (EngineManager.PlayerRevived)Delegate.CreateDelegate(typeof(EngineManager.PlayerRevived), player, miOnRevived);
                    gm.EngineManager.OnPlayerRevived += delRevived;
                }

                newEngine.SetSpeed(gm.SongSpeed);

                // 5. Reset track effects, coda, unison, and note counters
                var fiTrackEffects = tpType.GetField("_trackEffects", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fiTrackEffects?.GetValue(player) is System.Collections.IList trackEffectsList) trackEffectsList.Clear();

                var fiUpcomingEffects = tpType.GetField("_upcomingEffects", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fiUpcomingEffects?.GetValue(player) is System.Collections.ICollection upcomingEffects)
                {
                    var miClear = upcomingEffects.GetType().GetMethod("Clear");
                    miClear?.Invoke(upcomingEffects, null);
                }

                var fiBrePhrases = tpType.GetField("_brePhrases", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fiBrePhrases?.GetValue(player) is System.Collections.IList brePhrases) brePhrases.Clear();

                tpType.GetMethod("InitializeTrackEffects", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, null);
                tpType.GetMethod("InitializeCodaEvents", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, null);
                tpType.GetMethod("InitializeUnisonEvents", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, null);
                tpType.GetMethod("ResetNoteCounters", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, null);
                tpType.GetMethod("FinalizeTrackEffects", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, null);

                var fiSongLength = typeof(BasePlayer).GetField("SongLength", BindingFlags.Instance | BindingFlags.NonPublic);
                fiSongLength?.SetValue(player, (float)chart.GetEndTime());

                var piTrackMat = typeof(TrackPlayer).GetProperty("TrackMaterial", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (piTrackMat?.GetValue(player) is TrackMaterial trackMat)
                {
                    trackMat.FailState = 0f;
                }

                var fiPlayerFailed = typeof(TrackPlayer).GetField("PlayerHasFailed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                fiPlayerFailed?.SetValue(player, false);

                var cameraPos = player.GetComponentInChildren<YARG.Gameplay.Visuals.CameraPositioner>();
                if (cameraPos != null)
                {
                    var fiLower = typeof(YARG.Gameplay.Visuals.CameraPositioner).GetField("_lower", BindingFlags.Instance | BindingFlags.NonPublic);
                    (fiLower?.GetValue(cameraPos) as Tweener)?.Kill();
                    cameraPos.Raise(false);
                }

                // 6. Reset stem bindings and mute states for the new song
                var fiStem = player.GetType().GetField("_stem", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fiStem != null)
                {
                    var stem = player.Player.Profile.CurrentInstrument.ToSongStems().First();
                    if (stem == SongStem.Bass && mixer[SongStem.Bass] == null)
                    {
                        stem = SongStem.Rhythm;
                    }
                    fiStem.SetValue(player, stem);
                }

                if (player is DrumsPlayer drumsPlayer)
                {
                    var miGetLastStem = typeof(DrumsPlayer).GetMethod("GetLastAvailableDrumStem", BindingFlags.Static | BindingFlags.NonPublic);
                    var lastStem = miGetLastStem?.Invoke(null, new object[] { mixer }) ?? SongStem.Drums1;
                    typeof(DrumsPlayer).GetField("_lastStem", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(drumsPlayer, lastStem);

                    var fiStemMuteStates = typeof(DrumsPlayer).GetField("_stemMuteStates", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fiStemMuteStates?.GetValue(drumsPlayer) is Dictionary<SongStem, bool> drumMuteDict)
                    {
                        drumMuteDict[SongStem.Drums1] = false;
                        drumMuteDict[SongStem.Drums2] = false;
                        drumMuteDict[SongStem.Drums3] = false;
                        drumMuteDict[SongStem.Drums4] = false;
                    }
                }

                typeof(BasePlayer).GetField("IsStemMuted", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(player, false);
                player.SetStemMuteState(false);

                return true;
            }
            catch (Exception ex)
            {
                YARG.Core.Logging.YargLogger.LogException(ex, "Failed to reload TrackPlayer in-place");
                return false;
            }
        }
    }
}
