using Dalamud;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowStatus : Window, IDisposable
    {
        private readonly TrackAssistant trackAssistant;
        private readonly MidiFileManager fileManager;
        private readonly Configuration config;
        private readonly TrackHealthCheck trackHealth;

        private Vector4 colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
        private Vector4 colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
        private Vector4 colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);
        private Vector4 colorDetail = new Vector4(0.2f, 0.9f, 0.9f, 1);

        public bool showConfigs = false;
        public Action<MidiTrackWrapper> OnShowTrack;
        public Action<string> OnImportFile;

        private const int MaxNameLen = 30;
        private string[] cachedTrackNames;
        private string[] cachedAssistNames;
        private int[] cachedAssistIds;
        private FileDialogManager dlgManager = new();

        private string locImportHint;
        private string locSettingsHint;
        private string locDebugSnapshot;
        private string locPlayBPM;
        private string locKeyPerSecond;
        private string locWaitingForImport;
        private string locTrainingMode;
        private string locTrackHeader;
        private string locSelectTrack;
        private string locPreviewTrackSound;
        private string locPreviewTrackNotes;
        private string locTrackBPM;
        private string locTrackMeasure;
        private string locTrackBars;
        private string locTrackOctaves;
        private string locTrackSection;
        private string locTrackSectionUnits;
        private string locTrackSectionReset;
        private string locPlayMetronomeHint;
        private string locPlayMetronome;
        private string locPlayMetronomeNotVisible;
        private string locPlayMetronomeNotVisibleHint;
        private string locPlayMetronomeSyncError;
        private string locPlayMetronomePlaying;
        private string locPlayMetronomeStopped;
        private string locTrainingWaits;
        private string locStatusPlaying;
        private string locStatusPlayTime;
        private string locStartPlayingHint;
        private string locStatusPlayNotAvail;
        private string locStatusPlayNotAvailDefault;
        private string locStatusPlayNotAvailNeedsWideMode;
        private string locStatusPlayNotAvailNeedsKeyboard;
        private string locStatusPlayNotAvailNeedsAllBindings;
        private string locStatusExtendedMode;
        private string locStatusExtendedModeActive;
        private string locStatusExtendedModeHint;
        private string locStatusWideUIHint;
        private string locStatusTrackTruncated;
        private string locStatusTrackTruncatedHint;
        private string locStatusTrackTruncatedHintNoExtended;
        private string locStatusTooManyOctaves;
        private string locStartPlayingExtHint;

        private string locConfigBack;
        private string locConfigImport;
        private string locConfigAutoBPM;
        private string locConfigAutoBPMHelp;
        private string locConfigAutoSection;
        private string locConfigAutoSectionHelp;
        private string locConfigAllowExtendedMode;
        private string locConfigAssist;
        private string locConfigUseMetronome;
        private string locConfigUseMetronomeHelp;
        private string locConfigUsePlayback;
        private string locConfigUsePlaybackHelp;
        private string locConfigAssistMode;
        private string locConfigShowScore;
        private string locConfigAssistBindScaleKeyboard;
        private string locConfigAssistBindScaleGamepad;
        private string locConfigAssistNoteNumMarkers;
        private string locConfigAssistNoteWarnMs;

        public PluginWindowStatus(TrackAssistant trackAssistant, MidiFileManager fileManager, Configuration config, TrackHealthCheck trackHealth) : base("Harp Hero")
        {
            this.trackAssistant = trackAssistant;
            this.fileManager = fileManager;
            this.config = config;
            this.trackHealth = trackHealth;
            fileManager.OnImported += (_) => cachedTrackNames = null;

            IsOpen = false;

            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(0, 0), MaximumSize = new Vector2(400, 1000) };
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.AlwaysAutoResize;
            RespectCloseHotkey = false;

            Plugin.CurrentLocManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
        }

        public void Dispose()
        {
            // meh
        }

        private void CacheLocalization()
        {
            locImportHint = Localization.Localize("ST_ImportHint", "Import midi file");
            locSettingsHint = Localization.Localize("ST_SettingsHint", "Open settings");
            locDebugSnapshot = Localization.Localize("ST_DebugSnapshot", "Log debug snapshot");
            locPlayBPM = Localization.Localize("ST_PlayBPM", "Play BPM");
            locKeyPerSecond = Localization.Localize("ST_KeyPerSecond", "key/s");
            locWaitingForImport = Localization.Localize("ST_WaitingForImport", "Waiting for music track import...");
            locTrainingMode = Localization.Localize("ST_TrainingMode", "Training mode");
            locTrackHeader = Localization.Localize("ST_TrackHeader", "Track");
            locSelectTrack = Localization.Localize("ST_SelectTrack", "Select");
            locPreviewTrackSound = Localization.Localize("ST_PreviewTrack", "Preview track");
            locPreviewTrackNotes = Localization.Localize("ST_ShowTrackNotes", "Show notes");
            locTrackBPM = Localization.Localize("ST_TrackBPM", "BPM");
            locTrackMeasure = Localization.Localize("ST_TrackMeasure", "Measure");
            locTrackBars = Localization.Localize("ST_TrackBars", "Bars");
            locTrackOctaves = Localization.Localize("ST_TrackOctaves", "Octaves");
            locTrackSection = Localization.Localize("ST_TrackSection", "Section");
            locTrackSectionUnits = Localization.Localize("ST_TrackSectionUnits", "bars");
            locTrackSectionReset = Localization.Localize("ST_TrackSectionReset", "Set full length");
            locPlayMetronomeHint = Localization.Localize("ST_PlayMetronomeHint", "Use game metronome for play controls");
            locPlayMetronome = Localization.Localize("ST_Metronome", "Metronome");
            locPlayMetronomeNotVisible = Localization.Localize("ST_MetronomeNotVis", "not visible");
            locPlayMetronomeNotVisibleHint = Localization.Localize("ST_MetronomeNotVisibleHint", "(open to sync)");
            locPlayMetronomeSyncError = Localization.Localize("ST_MetronomeSyncError", "sync err: {0:0.#}ms");
            locPlayMetronomePlaying = Localization.Localize("ST_MetronomePlaying", "Playing");
            locPlayMetronomeStopped = Localization.Localize("ST_MetronomeStopped", "Stopped");
            locTrainingWaits = Localization.Localize("ST_TrainingWaits", "Waiting for key press...");
            locStatusPlaying = Localization.Localize("ST_StatusPlaying", "Playing");
            locStatusPlayTime = Localization.Localize("ST_StatusPlayTime", "time: {0:0.00}s");
            locStartPlayingHint = Localization.Localize("ST_StartPlayingHint", "Start playing");
            locStatusPlayNotAvail = Localization.Localize("ST_StatusPlayNotAvail", "Can't play");
            locStatusPlayNotAvailDefault = Localization.Localize("ST_StatusPlayNotAvailDefault", "check track details");
            locStatusPlayNotAvailNeedsWideMode = Localization.Localize("ST_StatusPlayNotAvailNeedsWideMode", "3 octave mode required");
            locStatusPlayNotAvailNeedsKeyboard = Localization.Localize("ST_StatusPlayNotAvailNeedsKeyboard", "keyboard input required");
            locStatusPlayNotAvailNeedsAllBindings = Localization.Localize("ST_StatusPlayNotAvailNeedsAllBindings", "all bindings required");
            locStatusExtendedMode = Localization.Localize("ST_ExtendedMode", "(extended mode required)");
            locStatusExtendedModeActive = Localization.Localize("ST_ExtendedModeActive", "(extended mode active)");
            locStatusExtendedModeHint = Localization.Localize("ST_ExtendedModeHint", "Extended mode allows playing notes in 5 octave range.\n- not available for gamepads\n- performance UI must be in wide, 3 octave mode\n- all keybinds must be defined");
            locStatusWideUIHint = Localization.Localize("ST_ExtendedModeWideHint", "3 octave UI: Settings > Keyboard Control > Assign all notes");
            locStatusTrackTruncated = Localization.Localize("ST_TrackTruncated", "(track cut to {0:P0})");
            locStatusTrackTruncatedHint = Localization.Localize("ST_TrackTruncatedHint", "Only part of the selected track will be played, check Section in track details.\nOctave limit: {0}");
            locStatusTrackTruncatedHintNoExtended = Localization.Localize("ST_TrackTruncatedHintNoExt", "To increase octave limit, please enable extended mode in settings and reload midi file.");
            locStatusTooManyOctaves = Localization.Localize("ST_TooManyOctaves", "Game doesn't allow more than 5 octaves, sorry :<");
            locStartPlayingExtHint = Localization.Localize("ST_StartPlayingExtHint", "Start playing (extended mode)");

            locConfigBack = Localization.Localize("CFG_Back", "Back to status");
            locConfigImport = Localization.Localize("CFG_Import", "MIDI import");
            locConfigAutoBPM = Localization.Localize("CFG_AutoBPM", "Auto adjust BPM");
            locConfigAutoBPMHelp = Localization.Localize("CFG_AutoBPMHelp", "Lowers tempo to fit in desired key press speed");
            locConfigAutoSection = Localization.Localize("CFG_AutoSection", "Auto adjust end bar");
            locConfigAutoSectionHelp = Localization.Localize("CFG_AutoSectionHelp", "Shorten music track to fit in 3 octave range");
            locConfigAllowExtendedMode = Localization.Localize("CFG_AllowExtended", "Allow extended, 5 octave mode");
            locConfigAssist = Localization.Localize("CFG_Assist", "Assist panel");
            locConfigUseMetronome = Localization.Localize("CFG_UseMetronome", "Use game metronome");
            locConfigUseMetronomeHelp = Localization.Localize("CFG_UseMetronomeHelp", "Gives control over music start/stop to game's metronome");
            locConfigUsePlayback = Localization.Localize("CFG_UsePlayback", "Use playback");
            locConfigUsePlaybackHelp = Localization.Localize("CFG_UsePlaybackHelp", "Play music track during performance, not available in training mode. This doesn't send any input to game, just makes hitting correct beats easier.");
            locConfigAssistMode = Localization.Localize("CFG_AssistMode", "Assist mode");
            locConfigShowScore = Localization.Localize("CFG_ShowScore", "Show score");
            locConfigAssistBindScaleKeyboard = Localization.Localize("CFG_BindScaleKeyboard", "Scale (keyboard)");
            locConfigAssistBindScaleGamepad = Localization.Localize("CFG_BindScaleGamepad", "Scale (gamepad)");
            locConfigAssistNoteNumMarkers = Localization.Localize("CFG_NoteNumMarkers", "Number of markers");
            locConfigAssistNoteWarnMs = Localization.Localize("CFG_NoteWarnMs", "Warn time (ms)");

            var sortedAssistNames = new List<Tuple<int, string>>
            {
                new Tuple<int, string>(0, Localization.Localize("CFG_AssistDisabled", "Disabled")),
                new Tuple<int, string>(1, Localization.Localize("CFG_AssistNote", "Note")),
                new Tuple<int, string>(2, Localization.Localize("CFG_AssistBind", "Key binding")),
            };
            sortedAssistNames.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            cachedAssistNames = new string[sortedAssistNames.Count];
            cachedAssistIds = new int[sortedAssistNames.Count];
            for (int idx = 0; idx < sortedAssistNames.Count; idx++)
            {
                cachedAssistNames[idx] = sortedAssistNames[idx].Item2;
                cachedAssistIds[idx] = sortedAssistNames[idx].Item1;
            }
        }

        public override void OnClose()
        {
            dlgManager.Reset();
        }

        public override void Draw()
        {
            if (showConfigs)
            {
                DrawSettings();
            }
            else
            {
                DrawStatus();
            }

            try
            {
                dlgManager.Draw();
            }
            catch (Exception ex)
            {
                PluginLog.LogError(ex, "file dialog died");
                dlgManager.Reset();
            }
        }

        private void DrawStatus()
        {
            trackHealth.UpdatePlayStatus(ImGui.GetIO().DeltaTime);

            // import & settings
            ImGui.AlignTextToFramePadding();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
            {
                dlgManager.OpenFileDialog(locImportHint, ".mid,.midi", (found, path) => { if (found) { fileManager.ImportFile(path); } });
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(locImportHint);
            }

            ImGui.SameLine();
            var trackIndentSize = ImGui.GetCursorPosX();
            var fileName = GetTrimmedName(System.IO.Path.GetFileName(fileManager.FilePath));
            ImGui.Text(string.IsNullOrEmpty(fileName) ? $"<< {locImportHint}" : fileName);

            ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - (50 * ImGuiHelpers.GlobalScale));
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Bug))
            {
                Plugin.RequestDebugSnapshot();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(locDebugSnapshot);
            }

            ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - (18 * ImGuiHelpers.GlobalScale));
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            {
                showConfigs = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(locSettingsHint);
            }

            DrawTrackDetails(trackIndentSize);

            // tempo overrides
            ImGui.Indent(trackIndentSize);
            if (trackAssistant.musicTrack != null)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locPlayBPM}:");
                ImGui.SameLine();
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);

                int targetBPM = trackAssistant.TargetBPM;
                if (targetBPM == 0)
                {
                    targetBPM = trackAssistant.musicTrack.stats.beatsPerMinute;
                }

                if (ImGui.InputInt("##targetBPM", ref targetBPM))
                {
                    targetBPM = Math.Max(10, Math.Min(200, targetBPM));
                    trackAssistant.SetTargetBPM(targetBPM);
                }

                ImGui.PopItemWidth();
                ImGui.SameLine();

                var numKeysPerSecond = trackAssistant.GetScaledKeysPerSecond();
                var colorKeysPerSecond = (numKeysPerSecond <= 1.0f) ? colorOk : (numKeysPerSecond <= 2.0f) ? colorYellow : colorErr;
                ImGui.TextColored(colorKeysPerSecond, $"( {numKeysPerSecond:0.#} {locKeyPerSecond} )");

                DrawTrackHealth();
            }
            ImGui.Unindent(trackIndentSize);

            ImGui.Separator();
            if (ImGui.Checkbox(locTrainingMode, ref trackAssistant.useWaitingForInput))
            {
                trackAssistant.OnTrainingModeChanged();
            }

            DrawPlayControls();
        }

        private void DrawTrackDetails(float indentSize)
        {
            if (trackAssistant.musicTrack == null)
            {
                return;
            }

            if (cachedTrackNames == null)
            {
                UpdateCachedTrackNames();
            }

            if (ImGui.CollapsingHeader($"{locTrackHeader}: {GetTrimmedName(trackAssistant.musicTrack.name)}"))
            {
                ImGui.Indent(indentSize);

                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locSelectTrack}:");
                ImGui.SameLine();

                ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
                int selectedIdx = fileManager.tracks.IndexOf(trackAssistant.musicTrack);
                if (ImGui.Combo("##trackCombo", ref selectedIdx, cachedTrackNames, cachedTrackNames.Length))
                {
                    if (selectedIdx >= 0 && selectedIdx < fileManager.tracks.Count)
                    {
                        trackAssistant.SetTrack(fileManager.tracks[selectedIdx]);
                    }
                }

                ImGui.SameLine();
                ImGuiHelpers.ScaledDummy(new Vector2(50, 0));
                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - (46 * ImGuiHelpers.GlobalScale));
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    OnShowTrack?.Invoke(trackAssistant.musicTrack);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(locPreviewTrackNotes);
                }

                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - (18 * ImGuiHelpers.GlobalScale));
                if (trackAssistant.IsPlayingPreview)
                {
                    if (ImGuiComponents.IconButton(21, FontAwesomeIcon.Stop))
                    {
                        trackAssistant.Stop();
                    }
                }
                else
                {
                    if (ImGuiComponents.IconButton(20, FontAwesomeIcon.Play))
                    {
                        trackAssistant.PlayPreview();
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(locPreviewTrackSound);
                    }
                }

                var statBlock = trackAssistant.musicTrack.stats;
                var timeSigVarDesc = (statBlock.numTimeSignatures > 1) ? "*" : "";
                bool isRangeValid = trackAssistant.CanPlay && (trackAssistant.IsValidBasicMode || (trackAssistant.IsValidExtendedMode && config.UseExtendedMode));
                bool isSectionValid = statBlock.numBarsTotal > 0;

                ImGui.Text($"{locTrackBPM}:");
                ImGui.SameLine();
                ImGui.TextColored(colorDetail, statBlock.beatsPerMinute.ToString());
                ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
                ImGui.Text($"{locTrackMeasure}:");
                ImGui.SameLine();
                ImGui.TextColored(colorDetail, $"{statBlock.timeSignature?.Numerator ?? 4}{timeSigVarDesc}");

                ImGui.Text($"{locTrackBars}:");
                ImGui.SameLine();
                ImGui.TextColored(isSectionValid ? colorDetail : colorErr, statBlock.numBarsTotal.ToString());
                ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
                ImGui.Text($"{locTrackOctaves}:");
                ImGui.SameLine();
                if (!isSectionValid)
                {
                    ImGui.TextColored(colorDetail, "--");
                }
                else
                {
                    ImGui.TextColored(isRangeValid ? colorDetail : colorErr, statBlock.GetOctaveRange().ToString());
                }

                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locTrackSection}:");
                ImGui.SameLine();
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
                int[] sectionBars = { statBlock.startBar, statBlock.endBar };
                if (ImGui.InputInt2("##section", ref sectionBars[0]))
                {
                    sectionBars[0] = Math.Max(0, sectionBars[0]);
                    sectionBars[1] = Math.Min(trackAssistant.musicTrack.statsOrg.numBarsTotal, sectionBars[1]);

                    // allow invalid values (0 length or start > end) here, most will be coming from in-between typing numbers
                    trackAssistant.SetTrackSection(sectionBars[0], sectionBars[1]);
                }
                ImGui.PopItemWidth();

                ImGui.SameLine();
                ImGui.Text(locTrackSectionUnits);

                ImGui.SameLine();
                if (ImGui.Button(locTrackSectionReset))
                {
                    trackAssistant.SetTrackSection(-1, -1);
                }

                ImGui.Unindent(indentSize);
                ImGui.Spacing();
            }
        }

        private void DrawTrackHealth()
        {
            bool needsSameLine = false;

            // show additional info when track is getting cut to fit in limits
            float lengthPct = trackHealth.GetTrackLengthPct();
            if (lengthPct < 1.0f)
            {
                ImGui.Spacing();
                ImGui.TextColored(colorYellow, string.Format(locStatusTrackTruncated, lengthPct).Replace("%", "%%"));

                int numAvailableOctaves = config.UseExtendedMode ? 5 : 3;
                string helpMsg = string.Format(locStatusTrackTruncatedHint, numAvailableOctaves);

                if (!config.UseExtendedMode)
                {
                    helpMsg += "\n\n" + locStatusTrackTruncatedHintNoExtended;
                }
                else
                {
                    if (trackHealth.cachedStatus == TrackHealthCheck.Status.TooManyOctaves)
                    {
                        helpMsg += "\n\n" + locStatusTooManyOctaves;
                    }
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(helpMsg);
                needsSameLine = true;
            }

            if (trackAssistant.IsValidExtendedMode)
            {
                if (needsSameLine) { ImGui.SameLine(); } else { ImGui.Spacing(); }

                if (config.UseExtendedMode)
                {
                    ImGui.TextColored(colorDetail, locStatusExtendedModeActive);
                }
                else
                {
                    ImGui.TextColored(colorYellow, locStatusExtendedMode);
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(locStatusExtendedModeHint);

                needsSameLine = true;
            }

            // too many octaves, full length
            if (config.UseExtendedMode && trackHealth.cachedStatus == TrackHealthCheck.Status.TooManyOctaves && !needsSameLine)
            {
                ImGui.TextColored(colorErr, locStatusTooManyOctaves);
            }
        }

        private void DrawPlayControls()
        {
            // play is context sensitive:
            // - training mode: no metronome support (game doesn't do pause/resume)
            // - metronome link: use when available
            // - otherwise just regular play/stop + al various reasons why it may fail

            bool showMetronomeLink = trackAssistant.HasMetronomeLink && !trackAssistant.useWaitingForInput;
            bool showPlayControls = !showMetronomeLink;

            if (showMetronomeLink)
            {
                ImGui.AlignTextToFramePadding();
                ImGuiComponents.DisabledButton(FontAwesomeIcon.Link);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(locPlayMetronomeHint);
                }

                ImGui.SameLine();
                ImGui.Text($"{locPlayMetronome}:");
                ImGui.SameLine();

                if (!trackAssistant.metronomeLink.IsActive)
                {
                    ImGui.TextColored(colorErr, locPlayMetronomeNotVisible);
                    ImGui.SameLine();
                    ImGui.Text(locPlayMetronomeNotVisibleHint);
                    showPlayControls = true;
                }
                else if (trackAssistant.metronomeLink.IsPlaying)
                {
                    ImGui.TextColored(colorOk, locPlayMetronomePlaying);

                    trackAssistant.metronomeLink.GetCurrentTime(out int metronomeBar, out int metronomeBeat, out long metronomeTimeUs);
                    float metronomeScaledMs = metronomeTimeUs * trackAssistant.timeScaling / 1000.0f;
                    float trackScaledMs = trackAssistant.CurrentTime * 1000.0f;
                    float syncErrorMs = Math.Abs(metronomeScaledMs - trackScaledMs);

                    ImGui.SameLine();
                    ImGui.Text($"[{metronomeBar}:{metronomeBeat}]");

                    if (syncErrorMs > 100.0f && trackAssistant.musicTrack != null && trackAssistant.IsPlaying)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(colorYellow, string.Format(locPlayMetronomeSyncError, syncErrorMs));
                    }
                    else
                    {
                        float trackPct = Math.Max(0.0f, 1.0f * (trackAssistant.CurrentTimeUs - trackAssistant.TrackStartTimeUs) / (trackAssistant.TrackEndTimeUs - trackAssistant.TrackStartTimeUs));
                        ImGui.SameLine();
                        ImGui.Text($"( {trackPct.ToString("P0").Replace("%", "%%")} )");
                    }
                }
                else
                {
                    ImGui.TextColored(colorYellow, locPlayMetronomeStopped);
                    ImGui.SameLine();
                    ImGui.Text($"{locTrackBPM}:{trackAssistant.metronomeLink.BPM}, {locTrackMeasure}:{trackAssistant.metronomeLink.Measure}");
                }
            }

            if (showPlayControls)
            {
                if (trackAssistant.IsPlaying)
                {
                    if (ImGuiComponents.IconButton(12, FontAwesomeIcon.Stop))
                    {
                        trackAssistant.Stop();
                    }

                    ImGui.SameLine();
                    if (trackAssistant.IsPausedForInput)
                    {
                        ImGui.TextColored(colorYellow, locTrainingWaits);
                    }
                    else
                    {
                        ImGui.TextColored(colorOk, locStatusPlaying);
                        ImGui.SameLine();
                        ImGui.Text(string.Format(locStatusPlayTime, trackAssistant.CurrentTime));

                        float trackPct = Math.Max(0.0f, 1.0f * (trackAssistant.CurrentTimeUs - trackAssistant.TrackStartTimeUs) / (trackAssistant.TrackEndTimeUs - trackAssistant.TrackStartTimeUs));
                        ImGui.SameLine();
                        ImGui.Text($"( {trackPct.ToString("P0").Replace("%", "%%")} )");
                    }
                }
                else
                {
                    if (trackHealth.cachedStatus == TrackHealthCheck.Status.CanPlayBasic ||
                        trackHealth.cachedStatus == TrackHealthCheck.Status.CanPlayExtended)
                    {
                        if (ImGuiComponents.IconButton(10, FontAwesomeIcon.Play))
                        {
                            trackAssistant.Start();
                        }

                        ImGui.SameLine();
                        ImGui.Text(trackHealth.cachedStatus == TrackHealthCheck.Status.CanPlayBasic ? locStartPlayingHint : locStartPlayingExtHint);
                    }
                    else
                    {
                        ImGuiComponents.DisabledButton(FontAwesomeIcon.Play, 11);

                        if (trackHealth.cachedStatus == TrackHealthCheck.Status.NoTrack)
                        {
                            ImGui.SameLine();
                            ImGui.Text(locWaitingForImport);
                        }
                    }
                }
            }

            bool showCantPlay =
                (trackHealth.cachedStatus != TrackHealthCheck.Status.CanPlayBasic) &&
                (trackHealth.cachedStatus != TrackHealthCheck.Status.CanPlayExtended) &&
                (trackHealth.cachedStatus != TrackHealthCheck.Status.NoTrack);

            if (showCantPlay)
            {
                if (!showPlayControls)
                {
                    ImGuiComponents.DisabledButton(FontAwesomeIcon.Unlink, 15);
                }

                ImGui.SameLine();
                ImGui.Text(locStatusPlayNotAvail);
                ImGui.SameLine();

                ImGui.TextColored(colorErr,
                    trackHealth.cachedStatus == TrackHealthCheck.Status.MissingWideMode ? locStatusPlayNotAvailNeedsWideMode :
                    trackHealth.cachedStatus == TrackHealthCheck.Status.MissingKeyboardMode ? locStatusPlayNotAvailNeedsKeyboard :
                    trackHealth.cachedStatus == TrackHealthCheck.Status.MissingBindings ? locStatusPlayNotAvailNeedsAllBindings :
                    locStatusPlayNotAvailDefault);

                if (trackHealth.cachedStatus == TrackHealthCheck.Status.MissingWideMode)
                {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker(locStatusWideUIHint);
                }
            }
        }

        private void DrawSettings()
        {
            ImGui.AlignTextToFramePadding();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Backward))
            {
                showConfigs = false;
            }
            ImGui.SameLine();
            ImGui.Text(locConfigBack);

            bool needsSave = false;
            bool hasChanges = false;
            bool autoAdjustEndBarCopy = config.AutoAdjustEndBar;
            bool autoAdjustBPMCopy = config.AutoAdjustBPM;
            bool useExtendedModeCopy = config.UseExtendedMode;
            float autoAdjustSpeedThresholdCopy = config.AutoAdjustSpeedThreshold;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"{locConfigImport}:");
            ImGui.Indent();

            hasChanges = ImGui.Checkbox(locConfigAutoBPM, ref autoAdjustBPMCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigAutoBPMHelp);

            if (autoAdjustBPMCopy)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                hasChanges = ImGui.InputFloat("##speedThr", ref autoAdjustSpeedThresholdCopy, 0.1f, 1.0f, "%.1f") || hasChanges;

                ImGui.SameLine();
                ImGui.Text(locKeyPerSecond);
            }

            hasChanges = ImGui.Checkbox(locConfigAutoSection, ref autoAdjustEndBarCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigAutoSectionHelp);

            hasChanges = ImGui.Checkbox(locConfigAllowExtendedMode, ref useExtendedModeCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locStatusExtendedModeHint);

            if (hasChanges)
            {
                config.AutoAdjustEndBar = autoAdjustEndBarCopy;
                config.AutoAdjustBPM = autoAdjustBPMCopy;
                config.UseExtendedMode = useExtendedModeCopy;
                config.AutoAdjustSpeedThreshold = Math.Min(10.0f, Math.Max(0.1f, autoAdjustSpeedThresholdCopy));
                needsSave = true;
                hasChanges = false;
            }

            ImGui.Unindent();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"{locConfigAssist}:");
            ImGui.Indent();

            int assistModeIdx = Math.Max(0, Array.IndexOf(cachedAssistIds, config.AssistMode));
            bool useMetronomeLinkCopy = config.UseMetronomeLink;
            bool usePlaybackCopy = config.UsePlayback;
            bool showScoreCopy = config.ShowScore;

            hasChanges = ImGui.Checkbox(locConfigUseMetronome, ref useMetronomeLinkCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigUseMetronomeHelp);

            hasChanges = ImGui.Checkbox(locConfigUsePlayback, ref usePlaybackCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigUsePlaybackHelp);

            hasChanges = ImGui.Checkbox(locConfigShowScore, ref showScoreCopy) || hasChanges;

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{locConfigAssistMode}:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            hasChanges = ImGui.Combo("##assistMode", ref assistModeIdx, cachedAssistNames, cachedAssistNames.Length) || hasChanges;

            if (hasChanges)
            {
                config.AssistMode = cachedAssistIds[assistModeIdx];
                config.UseMetronomeLink = useMetronomeLinkCopy;
                config.UsePlayback = usePlaybackCopy;
                config.ShowScore = showScoreCopy;
                needsSave = true;
                hasChanges = false;
            }

            int numMarkersCopy = config.AssistNote2Markers;
            int warnTimeCopy = config.AssistNote2WarnMs;
            float scaleKeyboardCopy = config.AssistBindScaleKeyboard;
            float scaleGamepadCopy = config.AssistBindScaleGamepad;

            ImGui.Indent();
            ImGui.Columns(2);
            if (config.UseAssistBind())
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistBindScaleKeyboard);
                ImGui.NextColumn();
                hasChanges = ImGui.SliderFloat("##scaleKeyboard", ref scaleKeyboardCopy, 1.0f, 2.5f, "%.2f") || hasChanges;

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistBindScaleGamepad);
                ImGui.NextColumn();
                hasChanges = ImGui.SliderFloat("##scaleGamepad", ref scaleGamepadCopy, 1.0f, 2.5f, "%.2f") || hasChanges;
            }
            else if (config.UseAssistNoteA())
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistNoteNumMarkers);
                ImGui.NextColumn();
                if (ImGui.InputInt("##noteMarkers", ref numMarkersCopy))
                {
                    numMarkersCopy = Math.Min(4, Math.Max(0, numMarkersCopy));
                    hasChanges = true;
                }

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistNoteWarnMs);
                ImGui.NextColumn();
                if (ImGui.InputInt("##noteWarnTime", ref warnTimeCopy))
                {
                    warnTimeCopy = Math.Min(1000, Math.Max(1, warnTimeCopy));
                    hasChanges = true;
                }
            }
            ImGui.Unindent();

            if (hasChanges)
            {
                config.AssistNote2Markers = numMarkersCopy;
                config.AssistNote2WarnMs = warnTimeCopy;
                config.AssistBindScaleKeyboard = scaleKeyboardCopy;
                config.AssistBindScaleGamepad = scaleGamepadCopy;
                needsSave = true;
                hasChanges = false;
            }

            ImGui.Unindent();
            if (needsSave)
            {
                config.Save();
            }
        }

        private string GetTrimmedName(string name)
        {
            if (name != null && name.Length > MaxNameLen)
            {
                return name.Substring(0, MaxNameLen) + "...";
            }

            return name;
        }

        private void UpdateCachedTrackNames()
        {
            if (fileManager.tracks != null && fileManager.tracks.Count > 0)
            {
                cachedTrackNames = new string[fileManager.tracks.Count];
                for (int idx = 0; idx < fileManager.tracks.Count; idx++)
                {
                    cachedTrackNames[idx] = GetTrimmedName(fileManager.tracks[idx].name);
                }
            }
        }
    }
}
