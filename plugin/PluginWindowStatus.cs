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
        private string locConfigBack;
        private string locConfigImport;
        private string locConfigAutoBPM;
        private string locConfigAutoBPMHelp;
        private string locConfigAutoSection;
        private string locConfigAutoSectionHelp;
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

        public PluginWindowStatus(TrackAssistant trackAssistant, MidiFileManager fileManager, Configuration config) : base("Harp Hero")
        {
            this.trackAssistant = trackAssistant;
            this.fileManager = fileManager;
            this.config = config;
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
            locTrackSectionReset = Localization.Localize("ST_TrackSectionReset", "Reset");
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
            locStatusPlayNotAvail = Localization.Localize("ST_StatusPlayNotAvail", "Can't play track, see details");

            locConfigBack = Localization.Localize("CFG_Back", "Back to status");
            locConfigImport = Localization.Localize("CFG_Import", "MIDI import");
            locConfigAutoBPM = Localization.Localize("CFG_AutoBPM", "Auto adjust BPM");
            locConfigAutoBPMHelp = Localization.Localize("CFG_AutoBPMHelp", "Lowers tempo to fit in desired key press speed");
            locConfigAutoSection = Localization.Localize("CFG_AutoSection", "Auto adjust end bar");
            locConfigAutoSectionHelp = Localization.Localize("CFG_AutoSectionHelp", "Shorten music track to fit in 3 octave range");
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
            }
            else
            {
                ImGui.Text(locWaitingForImport);
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
                bool isRangeValid = trackAssistant.CanPlay;
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
                ImGui.SameLine();
                ImGui.Text(locTrackSectionUnits);

                ImGui.PopItemWidth();

                float trackLengthPct = (statBlock.numBarsTotal > 0) ? 1.0f * statBlock.numBars / statBlock.numBarsTotal : 0.0f;
                var trackLengthDesc = $"({trackLengthPct:P0})".Replace("%", "%%");
                var trackLengthColor =
                    !isSectionValid ? colorErr :
                    (trackLengthPct < 0.5f) ? colorErr :
                    (trackLengthPct < 0.75f) ? colorYellow :
                    colorOk;

                ImGui.SameLine();
                ImGui.TextColored(trackLengthColor, trackLengthDesc);

                ImGui.SameLine();
                if (ImGui.Button(locTrackSectionReset))
                {
                    trackAssistant.SetTrackSection(-1, -1);
                }

                ImGui.Unindent(indentSize);
                ImGui.Spacing();
            }
        }

        private void DrawPlayControls()
        {
            // play is context sensitive:
            // - training mode: no metronome support (game doesn't do pause/resume)
            // - metronome link: use when available
            // - otherwise just regular play/stop

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

                    if (syncErrorMs > 100.0f && trackAssistant.musicTrack != null)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(colorYellow, string.Format(locPlayMetronomeSyncError, syncErrorMs));
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
                    }
                }
                else if (trackAssistant.CanPlay)
                {
                    if (ImGuiComponents.IconButton(10, FontAwesomeIcon.Play))
                    {
                        trackAssistant.Start();
                    }

                    ImGui.SameLine();
                    ImGui.Text(locStartPlayingHint);
                }
                else
                {
                    ImGuiComponents.DisabledButton(FontAwesomeIcon.Play, 11);
                    ImGui.SameLine();

                    bool isWaiting = string.IsNullOrEmpty(fileManager.FilePath);
                    if (isWaiting)
                    {
                        ImGui.Text(locWaitingForImport);
                    }
                    else
                    {
                        ImGui.TextColored(colorErr, locStatusPlayNotAvail);
                    }
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

            if (hasChanges)
            {
                config.AutoAdjustEndBar = autoAdjustEndBarCopy;
                config.AutoAdjustBPM = autoAdjustBPMCopy;
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
