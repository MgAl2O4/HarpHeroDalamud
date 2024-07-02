using Dalamud;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowStatus : Window, IDisposable
    {
        private readonly MidiFileManager fileManager;
        private readonly TrackHealthCheck trackHealth;

        private Vector4 colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
        private Vector4 colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
        private Vector4 colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);
        private Vector4 colorDetail = new Vector4(0.2f, 0.9f, 0.9f, 1);

        public bool showConfigs = false;
        public Action<MidiTrackWrapper>? OnShowTrack;
        public Action<string>? OnImportFile;

        private const int MaxNameLen = 30;
        private string[]? cachedTrackNames;
        private string[]? cachedAssistNames;
        private int[]? cachedAssistIds;
        private FileDialogManager dlgManager = new();
        private bool isPerformanceActive = false;

        private string? locImportHint;
        private string? locSettingsHint;
        private string? locDebugSnapshot;
        private string? locPlayBPM;
        private string? locKeyPerSecond;
        private string? locWaitingForImport;
        private string? locTrainingMode;
        private string? locTrackHeader;
        private string? locSelectTrack;
        private string? locPreviewTrackSound;
        private string? locPreviewTrackNotes;
        private string? locTrackBPM;
        private string? locTrackMeasure;
        private string? locTrackBars;
        private string? locTrackOctaves;
        private string? locTrackSection;
        private string? locTrackSectionUnits;
        private string? locTrackSectionReset;
        private string? locTrackTooShortMedian;
        private string? locTrackTranspose;
        private string? locPlayMetronomeHint;
        private string? locPlayMetronome;
        private string? locPlayMetronomeNotVisible;
        private string? locPlayMetronomeNotVisibleHint;
        private string? locPlayMetronomeSyncError;
        private string? locPlayMetronomePlaying;
        private string? locPlayMetronomeStopped;
        private string? locTrainingWaits;
        private string? locStatusPlaying;
        private string? locStatusPaused;
        private string? locStatusPlayTime;
        private string? locStartPlayingHint;
        private string? locStatusPlayNotAvail;
        private string? locStatusPlayNotAvailDefault;
        private string? locStatusPlayNotAvailNeedsWideMode;
        private string? locStatusPlayNotAvailNeedsKeyboard;
        private string? locStatusPlayNotAvailNeedsAllBindings;
        private string? locStatusExtendedMode;
        private string? locStatusExtendedModeActive;
        private string? locStatusExtendedModeHint;
        private string? locStatusWideUIHint;
        private string? locStatusTrackTruncated;
        private string? locStatusTrackTruncatedHint;
        private string? locStatusTrackTruncatedHintNoExtended;
        private string? locStatusTooManyOctaves;
        private string? locStartPlayingExtHint;

        private string? locConfigSettingsTab;
        private string? locConfigAppearanceTab;
        private string? locConfigBack;
        private string? locConfigImport;
        private string? locConfigAutoBPM;
        private string? locConfigAutoBPMHelp;
        private string? locConfigAutoSection;
        private string? locConfigAutoSectionHelp;
        private string? locConfigTooShortFilter;
        private string? locConfigTooShortFilterHelp;
        private string? locConfigAllowExtendedMode;
        private string? locConfigAssist;
        private string? locConfigUseMetronome;
        private string? locConfigUseMetronomeHelp;
        private string? locConfigUsePlayback;
        private string? locConfigUsePlaybackHelp;
        private string? locConfigAutoResume;
        private string? locConfigAutoResumeHelp;
        private string? locConfigAssistMode;
        private string? locConfigShowScore;
        private string? locConfigAssistBindScaleKeyboard;
        private string? locConfigAssistBindScaleGamepad;
        private string? locConfigAssistNoteNumMarkers;
        private string? locConfigAssistBindNumMarkers;
        private string? locConfigAssistBindExtraHints;
        private string? locConfigAssistNoteWarnMs;
        private string? locConfigModeBind;
        private string? locConfigModeNote;
        private string? locConfigAppearanceBgAlpha;
        private string? locConfigAppearanceKeyAlias;
        private string? locConfigAppearanceAddAlias;
        private string? locConfigShowOverlay;
        private string? locConfigShowAllHints;
        private string? locConfigShowAllHintsHelp;

        public PluginWindowStatus(MidiFileManager fileManager, TrackHealthCheck trackHealth) : base("Harp Hero")
        {
            this.fileManager = fileManager;
            this.trackHealth = trackHealth;
            fileManager.OnImported += (_) => cachedTrackNames = null;

            IsOpen = false;

            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(0, 0), MaximumSize = new Vector2(400, 1000) };
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.AlwaysAutoResize;
            RespectCloseHotkey = false;

            if (Plugin.CurrentLocManager != null)
            {
                Plugin.CurrentLocManager.LocalizationChanged += (_) => CacheLocalization();
            }
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
            locTrackTooShortMedian = Localization.Localize("ST_TrackShortNoteMedian", "Duration filter median:");
            locTrackTranspose = Localization.Localize("ST_TrackTranspose", "Transpose");
            locPlayMetronomeHint = Localization.Localize("ST_PlayMetronomeHint", "Use game metronome for play controls");
            locPlayMetronome = Localization.Localize("ST_Metronome", "Metronome");
            locPlayMetronomeNotVisible = Localization.Localize("ST_MetronomeNotVis", "not visible");
            locPlayMetronomeNotVisibleHint = Localization.Localize("ST_MetronomeNotVisibleHint", "(open to sync)");
            locPlayMetronomeSyncError = Localization.Localize("ST_MetronomeSyncError", "sync err: {0:0.#}ms");
            locPlayMetronomePlaying = Localization.Localize("ST_MetronomePlaying", "Playing");
            locPlayMetronomeStopped = Localization.Localize("ST_MetronomeStopped", "Stopped");
            locTrainingWaits = Localization.Localize("ST_TrainingWaits", "Waiting for key press...");
            locStatusPlaying = Localization.Localize("ST_StatusPlaying", "Playing");
            locStatusPaused = Localization.Localize("ST_StatusPaused", "Paused");
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

            locConfigSettingsTab = Localization.Localize("CFG_TabSettings", "Settings");
            locConfigAppearanceTab = Localization.Localize("CFG_TabAppearance", "Appearance");

            locConfigBack = Localization.Localize("CFG_Back", "Back to status");
            locConfigImport = Localization.Localize("CFG_Import", "MIDI import");
            locConfigAutoBPM = Localization.Localize("CFG_AutoBPM", "Auto adjust BPM");
            locConfigAutoBPMHelp = Localization.Localize("CFG_AutoBPMHelp", "Lowers tempo to fit in desired key press speed");
            locConfigAutoSection = Localization.Localize("CFG_AutoSection", "Auto adjust end bar");
            locConfigAutoSectionHelp = Localization.Localize("CFG_AutoSectionHelp", "Shorten music track to fit in 3 octave range");
            locConfigTooShortFilter = Localization.Localize("CFG_MinDuration", "Min note duration (ms)");
            locConfigTooShortFilterHelp = Localization.Localize("CFG_MinDurationHelp", "All notes shorter than threshold will be removed during import");
            locConfigAllowExtendedMode = Localization.Localize("CFG_AllowExtended", "Allow extended, 5 octave mode");
            locConfigAssist = Localization.Localize("CFG_Assist", "Assist panel");
            locConfigUseMetronome = Localization.Localize("CFG_UseMetronome", "Use game metronome");
            locConfigUseMetronomeHelp = Localization.Localize("CFG_UseMetronomeHelp", "Gives control over music start/stop to game's metronome");
            locConfigUsePlayback = Localization.Localize("CFG_UsePlayback", "Use playback");
            locConfigUsePlaybackHelp = Localization.Localize("CFG_UsePlaybackHelp", "Play music track during performance, not available in training mode. This doesn't send any input to game, just makes hitting correct beats easier.");
            locConfigAutoResume = Localization.Localize("CFG_AutoResume", "Auto resume");
            locConfigAutoResumeHelp = Localization.Localize("CFG_AutoResumeHelp", "Automatically resume paused play when entering performance mode.");
            locConfigAssistMode = Localization.Localize("CFG_AssistMode", "Assist mode");
            locConfigShowScore = Localization.Localize("CFG_ShowScore", "Show score");
            locConfigAssistBindScaleKeyboard = Localization.Localize("CFG_BindScaleKeyboard", "Scale (keyboard)");
            locConfigAssistBindScaleGamepad = Localization.Localize("CFG_BindScaleGamepad", "Scale (gamepad)");
            locConfigAssistBindNumMarkers = Localization.Localize("CFG_BindNumMarkers", "Number of lines");
            locConfigAssistBindExtraHints = Localization.Localize("CFG_BindNumHintsAhead", "Number of extra hints");
            locConfigAssistNoteNumMarkers = Localization.Localize("CFG_NoteNumMarkers", "Number of markers");
            locConfigAssistNoteWarnMs = Localization.Localize("CFG_NoteWarnMs", "Warn time (ms)");
            locConfigModeBind = Localization.Localize("CFG_AssistBind", "Key binding");
            locConfigModeNote = Localization.Localize("CFG_AssistNote", "Note");
            locConfigShowOverlay = Localization.Localize("CFG_ShowOverlay", "Show overlay when performance mode is active");
            locConfigShowAllHints = Localization.Localize("CFG_ShowAllBinds", "Show all notes");
            locConfigShowAllHintsHelp = Localization.Localize("CFG_ShowAllBindsHelp", "Binding mode: All notes will be shown, binding description depends on number of hints.");

            locConfigAppearanceBgAlpha = Localization.Localize("CFG_BackgroundAlpha", "Background alpha");
            locConfigAppearanceKeyAlias = Localization.Localize("CFG_KeyAlias", "Key name alias");
            locConfigAppearanceAddAlias = Localization.Localize("CFG_KeyAliasAdd", "Add new alias");

            var sortedAssistNames = new List<Tuple<int, string>>
            {
                new Tuple<int, string>(0, Localization.Localize("CFG_AssistDisabled", "Disabled")),
                new Tuple<int, string>(1, locConfigModeNote),
                new Tuple<int, string>(2, locConfigModeBind),
                new Tuple<int, string>(3, Localization.Localize("CFG_AssistMixed", "Note (mixed)")),
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
                Service.logger.Error(ex, "file dialog died");
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
                dlgManager.OpenFileDialog(locImportHint ?? "", ".mid,.midi", (found, path) =>
                {
                    if (found)
                    {
                        MidiTrackWrapper.MinNoteDurationSeconds = Service.config.MinNoteDurationMs * 0.001f;
                        fileManager.ImportFile(path);
                    }
                });
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(locImportHint);
            }

            ImGui.SameLine();
            var trackIndentSize = ImGui.GetCursorPosX();
            var fileName = GetTrimmedName(System.IO.Path.GetFileName(fileManager.FilePath));
            ImGui.Text(string.IsNullOrEmpty(fileName) ? $"<< {locImportHint}" : fileName);

            var availWindowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
            ImGui.SameLine(availWindowWidth - (50 * ImGuiHelpers.GlobalScale));
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Bug))
            {
                Plugin.RequestDebugSnapshot();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(locDebugSnapshot);
            }

            ImGui.SameLine(availWindowWidth - (18 * ImGuiHelpers.GlobalScale));
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
            if (Service.trackAssistant.musicTrack != null)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locPlayBPM}:");
                ImGui.SameLine();
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);

                int targetBPM = Service.trackAssistant.TargetBPM;
                if (targetBPM == 0)
                {
                    targetBPM = Service.trackAssistant.musicTrack.stats.beatsPerMinute;
                }

                if (ImGui.InputInt("##targetBPM", ref targetBPM))
                {
                    targetBPM = Math.Max(10, Math.Min(200, targetBPM));
                    Service.trackAssistant.SetTargetBPM(targetBPM);
                }

                ImGui.PopItemWidth();
                ImGui.SameLine();

                var numKeysPerSecond = Service.trackAssistant.GetScaledKeysPerSecond();
                var colorKeysPerSecond = (numKeysPerSecond <= 1.0f) ? colorOk : (numKeysPerSecond <= 2.0f) ? colorYellow : colorErr;
                ImGui.TextColored(colorKeysPerSecond, $"( {numKeysPerSecond:0.#} {locKeyPerSecond} )");

                DrawTrackHealth();
            }
            ImGui.Unindent(trackIndentSize);

            ImGui.Separator();
            if (ImGui.Checkbox(locTrainingMode, ref Service.trackAssistant.useWaitingForInput))
            {
                Service.trackAssistant.OnTrainingModeChanged();
            }

            DrawPlayControls();
        }

        private void DrawTrackDetails(float indentSize)
        {
            if (Service.trackAssistant.musicTrack == null)
            {
                return;
            }

            if (cachedTrackNames == null)
            {
                UpdateCachedTrackNames();
            }

            if (ImGui.CollapsingHeader($"{locTrackHeader}: {GetTrimmedName(Service.trackAssistant.musicTrack.name)}"))
            {
                ImGui.Indent(indentSize);

                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locSelectTrack}:");
                ImGui.SameLine();

                if (cachedTrackNames != null)
                {
                    ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
                    int selectedIdx = fileManager.tracks.IndexOf(Service.trackAssistant.musicTrack);
                    if (ImGui.Combo("##trackCombo", ref selectedIdx, cachedTrackNames, cachedTrackNames.Length))
                    {
                        if (selectedIdx >= 0 && selectedIdx < fileManager.tracks.Count)
                        {
                            Service.trackAssistant.SetTrack(fileManager.tracks[selectedIdx]);
                        }
                    }
                }

                ImGui.SameLine();
                ImGuiHelpers.ScaledDummy(new Vector2(50, 0));

                var availRegionWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                ImGui.SameLine(availRegionWidth - (46 * ImGuiHelpers.GlobalScale));
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    OnShowTrack?.Invoke(Service.trackAssistant.musicTrack);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(locPreviewTrackNotes);
                }

                ImGui.SameLine(availRegionWidth - (18 * ImGuiHelpers.GlobalScale));
                if (Service.trackAssistant.IsPlayingPreview)
                {
                    if (IconButtonWithId(21, FontAwesomeIcon.Stop))
                    {
                        Service.trackAssistant.Stop();
                    }
                }
                else
                {
                    if (IconButtonWithId(20, FontAwesomeIcon.Play))
                    {
                        Service.trackAssistant.PlayPreview();
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(locPreviewTrackSound);
                    }
                }

                var statBlock = Service.trackAssistant.musicTrack.stats;
                var timeSigVarDesc = (statBlock.numTimeSignatures > 1) ? "*" : "";
                bool isRangeValid = Service.trackAssistant.CanPlay && (Service.trackAssistant.IsValidBasicMode || (Service.trackAssistant.IsValidExtendedMode && Service.config.UseExtendedMode));
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

                ImGui.Text(locTrackTooShortMedian);
                ImGui.SameLine();
                if (Service.trackAssistant.musicTrack.medianTooShortMs < 0.0f)
                {
                    ImGui.TextColored(colorDetail, "--");
                }
                else
                {
                    ImGui.TextColored(colorDetail, $"{Service.trackAssistant.musicTrack.medianTooShortMs} ms");
                }
                ImGuiComponents.HelpMarker(locConfigTooShortFilterHelp ?? "");

                int transposeOffset = Service.trackAssistant.musicTrack.TransposeOffset;
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locTrackTranspose);
                ImGui.SameLine();
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt("##transpose", ref transposeOffset))
                {
                    transposeOffset = Math.Clamp(transposeOffset, -24, 24);

                    int transposeDelta = transposeOffset - Service.trackAssistant.musicTrack.TransposeOffset;
                    Service.trackAssistant.musicTrack.TryTransposeNotes(transposeDelta, Service.config.UseExtendedMode);
                }
                ImGui.SameLine();

                Service.trackAssistant.musicTrack.stats.DescribeNoteRange(out string noteRangeMin, out string noteRangeMax);
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"({noteRangeMin} .. {noteRangeMax})");

                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locTrackSection}:");
                ImGui.SameLine();
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
                int[] sectionBars = { statBlock.startBar, statBlock.endBar };
                if (ImGui.InputInt2("##section", ref sectionBars[0]))
                {
                    sectionBars[0] = Math.Max(0, sectionBars[0]);
                    sectionBars[1] = Math.Min(Service.trackAssistant.musicTrack.statsOrg.numBarsTotal, sectionBars[1]);

                    // allow invalid values (0 length or start > end) here, most will be coming from in-between typing numbers
                    Service.trackAssistant.SetTrackSection(sectionBars[0], sectionBars[1]);
                }
                ImGui.PopItemWidth();

                ImGui.SameLine();
                ImGui.Text(locTrackSectionUnits);

                ImGui.SameLine();
                if (ImGui.Button(locTrackSectionReset))
                {
                    Service.trackAssistant.SetTrackSection(-1, -1);
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
                ImGui.TextColored(colorYellow, string.Format(locStatusTrackTruncated ?? "", lengthPct).Replace("%", "%%"));

                int numAvailableOctaves = Service.config.UseExtendedMode ? 5 : 3;
                string helpMsg = string.Format(locStatusTrackTruncatedHint ?? "", numAvailableOctaves);

                if (!Service.config.UseExtendedMode)
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

            if (Service.trackAssistant.IsValidExtendedMode)
            {
                if (needsSameLine) { ImGui.SameLine(); } else { ImGui.Spacing(); }

                if (Service.config.UseExtendedMode)
                {
                    ImGui.TextColored(colorDetail, locStatusExtendedModeActive);
                }
                else
                {
                    ImGui.TextColored(colorYellow, locStatusExtendedMode);
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(locStatusExtendedModeHint ?? "");

                needsSameLine = true;
            }

            // too many octaves, full length
            if (Service.config.UseExtendedMode && trackHealth.cachedStatus == TrackHealthCheck.Status.TooManyOctaves && !needsSameLine)
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

            bool showMetronomeLink = Service.trackAssistant.HasMetronomeLink && !Service.trackAssistant.useWaitingForInput;
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

                if (!Service.trackAssistant.metronomeLink.IsActive)
                {
                    ImGui.TextColored(colorErr, locPlayMetronomeNotVisible);
                    ImGui.SameLine();
                    ImGui.Text(locPlayMetronomeNotVisibleHint);
                    showPlayControls = true;
                }
                else if (Service.trackAssistant.metronomeLink.IsPlaying)
                {
                    ImGui.TextColored(colorOk, locPlayMetronomePlaying);

                    Service.trackAssistant.metronomeLink.GetCurrentTime(out int metronomeBar, out int metronomeBeat, out long metronomeTimeUs);
                    float metronomeScaledMs = metronomeTimeUs * Service.trackAssistant.timeScaling / 1000.0f;
                    float trackScaledMs = Service.trackAssistant.CurrentTime * 1000.0f;
                    float syncErrorMs = Math.Abs(metronomeScaledMs - trackScaledMs);

                    ImGui.SameLine();
                    ImGui.Text($"[{metronomeBar}:{metronomeBeat}]");

                    if (syncErrorMs > 100.0f && Service.trackAssistant.musicTrack != null && Service.trackAssistant.IsPlaying)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(colorYellow, string.Format(locPlayMetronomeSyncError ?? "", syncErrorMs));
                    }
                    else
                    {
                        float trackPct = Math.Max(0.0f, 1.0f * (Service.trackAssistant.CurrentTimeUs - Service.trackAssistant.TrackStartTimeUs) / (Service.trackAssistant.TrackEndTimeUs - Service.trackAssistant.TrackStartTimeUs));
                        ImGui.SameLine();
                        ImGui.Text($"( {trackPct.ToString("P0").Replace("%", "%%")} )");
                    }
                }
                else
                {
                    ImGui.TextColored(colorYellow, locPlayMetronomeStopped);
                    ImGui.SameLine();
                    ImGui.Text($"{locTrackBPM}:{Service.trackAssistant.metronomeLink.BPM}, {locTrackMeasure}:{Service.trackAssistant.metronomeLink.Measure}");
                }
            }

            if (showPlayControls)
            {
                if (Service.trackAssistant.IsPlaying)
                {
                    if (IconButtonWithId(12, FontAwesomeIcon.Stop))
                    {
                        Service.trackAssistant.Stop();
                    }

                    ImGui.SameLine();
                    if (Service.trackAssistant.IsPausedForUI)
                    {
                        if (isPerformanceActive)
                        {
                            if (IconButtonWithId(13, FontAwesomeIcon.Play))
                            {
                                Service.trackAssistant.Resume();
                            }
                        }
                        else
                        {
                            DisabledIconButtonWithId(FontAwesomeIcon.Play, 13);
                        }
                    }
                    else
                    {
                        if (IconButtonWithId(13, FontAwesomeIcon.Pause))
                        {
                            Service.trackAssistant.Pause();
                        }
                    }

                    ImGui.SameLine();
                    if (Service.trackAssistant.IsPausedForInput && !Service.trackAssistant.IsPausedForUI)
                    {
                        ImGui.TextColored(colorYellow, locTrainingWaits);
                    }
                    else
                    {
                        if (Service.trackAssistant.IsPausedForUI)
                        {
                            ImGui.TextColored(colorYellow, locStatusPaused);
                        }
                        else
                        {
                            ImGui.TextColored(colorOk, locStatusPlaying);
                        }

                        ImGui.SameLine();
                        ImGui.Text(string.Format(locStatusPlayTime ?? "", Service.trackAssistant.CurrentTime));

                        float trackPct = Math.Max(0.0f, 1.0f * (Service.trackAssistant.CurrentTimeUs - Service.trackAssistant.TrackStartTimeUs) / (Service.trackAssistant.TrackEndTimeUs - Service.trackAssistant.TrackStartTimeUs));
                        ImGui.SameLine();
                        ImGui.Text($"( {trackPct.ToString("P0").Replace("%", "%%")} )");
                    }
                }
                else
                {
                    if (trackHealth.cachedStatus == TrackHealthCheck.Status.CanPlayBasic ||
                        trackHealth.cachedStatus == TrackHealthCheck.Status.CanPlayExtended)
                    {
                        if (IconButtonWithId(10, FontAwesomeIcon.Play))
                        {
                            Service.trackAssistant.Start();
                        }

                        ImGui.SameLine();
                        ImGui.Text(trackHealth.cachedStatus == TrackHealthCheck.Status.CanPlayBasic ? locStartPlayingHint : locStartPlayingExtHint);
                    }
                    else
                    {
                        DisabledIconButtonWithId(FontAwesomeIcon.Play, 11);

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
                    DisabledIconButtonWithId(FontAwesomeIcon.Unlink, 15);
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
                    ImGuiComponents.HelpMarker(locStatusWideUIHint ?? "");
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

            if (ImGui.BeginTabBar("##settings"))
            {
                if (ImGui.BeginTabItem(locConfigSettingsTab))
                {
                    DrawSettingsBehavior();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(locConfigAppearanceTab))
                {
                    DrawSettingsAppearance();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawSettingsBehavior()
        {
            bool needsSave = false;
            bool hasChanges = false;
            bool autoAdjustEndBarCopy = Service.config.AutoAdjustEndBar;
            bool autoAdjustBPMCopy = Service.config.AutoAdjustBPM;
            bool useExtendedModeCopy = Service.config.UseExtendedMode;
            float autoAdjustSpeedThresholdCopy = Service.config.AutoAdjustSpeedThreshold;
            int minDurationCopy = Service.config.MinNoteDurationMs;

            ImGui.Text($"{locConfigImport}:");
            ImGui.Indent();

            hasChanges = ImGui.Checkbox(locConfigAutoBPM, ref autoAdjustBPMCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigAutoBPMHelp ?? "");

            if (autoAdjustBPMCopy)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                hasChanges = ImGui.InputFloat("##speedThr", ref autoAdjustSpeedThresholdCopy, 0.1f, 1.0f, "%.1f") || hasChanges;

                ImGui.SameLine();
                ImGui.Text(locKeyPerSecond);
            }

            hasChanges = ImGui.Checkbox(locConfigAutoSection, ref autoAdjustEndBarCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigAutoSectionHelp ?? "");

            hasChanges = ImGui.Checkbox(locConfigAllowExtendedMode, ref useExtendedModeCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locStatusExtendedModeHint ?? "");

            ImGui.AlignTextToFramePadding();
            ImGui.Text(locConfigTooShortFilter);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            hasChanges = ImGui.InputInt("##tooShortFilter", ref minDurationCopy, 1, 25) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigTooShortFilterHelp ?? "");

            if (hasChanges)
            {
                Service.config.AutoAdjustEndBar = autoAdjustEndBarCopy;
                Service.config.AutoAdjustBPM = autoAdjustBPMCopy;
                Service.config.UseExtendedMode = useExtendedModeCopy;
                Service.config.AutoAdjustSpeedThreshold = Math.Min(10.0f, Math.Max(0.1f, autoAdjustSpeedThresholdCopy));
                Service.config.MinNoteDurationMs = Math.Min(500, Math.Max(10, minDurationCopy));
                needsSave = true;
                hasChanges = false;
            }

            ImGui.Unindent();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"{locConfigAssist}:");
            ImGui.Indent();

            int assistModeIdx = (cachedAssistIds != null) ? Math.Max(0, Array.IndexOf(cachedAssistIds, Service.config.AssistMode)) : 0;
            bool useMetronomeLinkCopy = Service.config.UseMetronomeLink;
            bool usePlaybackCopy = Service.config.UsePlayback;
            bool autoResumeCopy = Service.config.AutoResume;
            bool showScoreCopy = Service.config.ShowScore;

            hasChanges = ImGui.Checkbox(locConfigUseMetronome, ref useMetronomeLinkCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigUseMetronomeHelp ?? "");

            hasChanges = ImGui.Checkbox(locConfigUsePlayback, ref usePlaybackCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigUsePlaybackHelp ?? "");

            hasChanges = ImGui.Checkbox(locConfigAutoResume, ref autoResumeCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigAutoResumeHelp ?? "");

            hasChanges = ImGui.Checkbox(locConfigShowScore, ref showScoreCopy) || hasChanges;

            if (cachedAssistNames != null)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{locConfigAssistMode}:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                hasChanges = ImGui.Combo("##assistMode", ref assistModeIdx, cachedAssistNames, cachedAssistNames.Length) || hasChanges;
            }

            if (hasChanges)
            {
                Service.config.AssistMode = (cachedAssistIds != null) ? cachedAssistIds[assistModeIdx] : 0;
                Service.config.UseMetronomeLink = useMetronomeLinkCopy;
                Service.config.UsePlayback = usePlaybackCopy;
                Service.config.AutoResume = autoResumeCopy;
                Service.config.ShowScore = showScoreCopy;
                needsSave = true;
                hasChanges = false;
            }

            ImGui.Unindent();
            ImGui.Spacing();
            ImGui.Separator();

            bool showOverlayCopy = Service.config.ShowOverlay;

            hasChanges = ImGui.Checkbox(locConfigShowOverlay, ref showOverlayCopy) || hasChanges;

            if (hasChanges)
            {
                Service.config.ShowOverlay = showOverlayCopy;
                needsSave = true;
                hasChanges = false;
            }

            if (needsSave)
            {
                Service.config.Save();
            }
        }

        private void DrawSettingsAppearance()
        {
            float alphaCopy = Service.config.AssistBgAlpha;
            bool hasChanges = false;
            bool needsSave = false;

            ImGui.AlignTextToFramePadding();
            ImGui.Text(locConfigAppearanceBgAlpha);
            ImGui.SameLine();
            hasChanges = ImGui.SliderFloat("##alpha", ref alphaCopy, 0.1f, 1.0f) || hasChanges;

            if (hasChanges)
            {
                Service.config.AssistBgAlpha = alphaCopy;
                needsSave = true;
            }

            int numMarkersCopy = Service.config.AssistNote2Markers;
            int warnTimeCopy = Service.config.AssistNote2WarnMs;
            int numBindMarkersCopy = Service.config.AssistBindRows;
            int numBindExtraHintsCopy = Service.config.AssistBindExtraHints;
            float scaleKeyboardCopy = Service.config.AssistBindScaleKeyboard;
            float scaleGamepadCopy = Service.config.AssistBindScaleGamepad;
            bool showAllNotesCopy = Service.config.AssistAllNotes;

            var textLineHeight = ImGui.GetTextLineHeightWithSpacing();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"{locConfigAssistMode}: {locConfigModeBind}");
            hasChanges = ImGui.Checkbox(locConfigShowAllHints, ref showAllNotesCopy) || hasChanges;
            ImGuiComponents.HelpMarker(locConfigShowAllHintsHelp ?? "");

            if (ImGui.BeginChild("##detailsAssistMode1", new Vector2(-1.0f, textLineHeight * 5.0f)))
            {
                ImGui.Columns(2);

                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistBindScaleKeyboard);
                ImGui.NextColumn();
                hasChanges = ImGui.SliderFloat("##scaleKeyboard", ref scaleKeyboardCopy, 1.0f, 2.5f, "%.2f") || hasChanges;

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistBindScaleGamepad);
                ImGui.NextColumn();
                hasChanges = ImGui.SliderFloat("##scaleGamepad", ref scaleGamepadCopy, 1.0f, 2.5f, "%.2f") || hasChanges;

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistBindNumMarkers);
                ImGui.NextColumn();
                hasChanges = ImGui.SliderInt("##numBindHints", ref numBindMarkersCopy, 1, 6) || hasChanges;

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(locConfigAssistBindExtraHints);
                ImGui.NextColumn();
                hasChanges = ImGui.SliderInt("##numBindHintsExtra", ref numBindExtraHintsCopy, 1, 20) || hasChanges;
            }
            ImGui.EndChild();

            ImGui.Text($"{locConfigAssistMode}: {locConfigModeNote}");
            if (ImGui.BeginChild("##detailsAssistMode2", new Vector2(-1.0f, textLineHeight * 3.0f)))
            {
                ImGui.Columns(2);

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
            ImGui.EndChild();

            if (hasChanges)
            {
                Service.config.AssistNote2Markers = numMarkersCopy;
                Service.config.AssistNote2WarnMs = warnTimeCopy;
                Service.config.AssistBindScaleKeyboard = scaleKeyboardCopy;
                Service.config.AssistBindScaleGamepad = scaleGamepadCopy;
                Service.config.AssistBindRows = numBindMarkersCopy;
                Service.config.AssistBindExtraHints = numBindExtraHintsCopy;
                Service.config.AssistAllNotes = showAllNotesCopy;
                Service.trackAssistant.UpdateViewerParams();
                needsSave = true;
                hasChanges = false;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text(locConfigAppearanceKeyAlias);
            if (Service.config.VKAlias.Count > 0)
            {
                var colorRed = new Vector4(0.9f, 0.0f, 0.0f, 1.0f);
                int removeIdx = -1;

                if (ImGui.BeginTable("##keyaliases", 3, ImGuiTableFlags.Sortable | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersH))
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 20.0f * ImGuiHelpers.GlobalScale);

                    for (int aliasIdx = 0; aliasIdx < Service.config.VKAlias.Count; aliasIdx++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        var aliasVK = (VirtualKey)Service.config.VKAlias[aliasIdx].Item1;
                        ushort newVK = (ushort)aliasVK;
                        string newDesc = Service.config.VKAlias[aliasIdx].Item2;
                        bool needsUpdate = false;

                        ImGui.SetNextItemWidth(-1.0f);
                        if (ImGui.BeginCombo($"##vkCombo{aliasIdx}", aliasVK.GetFancyName()))
                        {
                            foreach (var key in Enum.GetValues<VirtualKey>())
                            {
                                if (ImGui.Selectable(key.GetFancyName(), key == aliasVK))
                                {
                                    newVK = (ushort)key;
                                    needsUpdate = true;
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1.0f);
                        if (ImGui.InputText($"##vkText{aliasIdx}", ref newDesc, 16))
                        {
                            needsUpdate = true;
                        }

                        ImGui.TableNextColumn();
                        if (IconButtonWithId(100 + aliasIdx, FontAwesomeIcon.Times, colorRed))
                        {
                            removeIdx = aliasIdx;
                        }

                        if (needsUpdate)
                        {
                            Service.config.VKAlias[aliasIdx] = new Tuple<ushort, string>(newVK, newDesc);
                            needsSave = true;
                        }
                    }

                    ImGui.EndTable();
                }

                if (removeIdx >= 0)
                {
                    Service.config.VKAlias.RemoveAt(removeIdx);
                    needsSave = true;
                }
            }

            if (ImGui.Button(locConfigAppearanceAddAlias))
            {
                Service.config.VKAlias.Add(new Tuple<ushort, string>((ushort)VirtualKey.KEY_0, VirtualKey.KEY_0.GetFancyName()));
                needsSave = true;
            }

            if (needsSave)
            {
                Service.config.Save();
                Service.config.ApplyVKAliases();
            }
        }

        private string? GetTrimmedName(string? name)
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
                    cachedTrackNames[idx] = GetTrimmedName(fileManager.tracks[idx].name) ?? "";
                }
            }
        }

        public void OnPerformanceVisibilityChanged(bool isVisible)
        {
            isPerformanceActive = isVisible;
            if (isVisible)
            {
                IsOpen = Service.config.ShowOverlay;
            }
            else if (!Service.trackAssistant.IsPausedForUI)
            {
                IsOpen = false;
            }
        }

        private bool IconButtonWithId(int id, FontAwesomeIcon icon, Vector4? color = null)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var result = ImGuiComponents.IconButton($"{icon.ToIconString()}##{id}", color);
            ImGui.PopFont();

            return result;
        }

        private void DisabledIconButtonWithId(FontAwesomeIcon icon, int id)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiComponents.DisabledButton($"{icon.ToIconString()}##{id}");
            ImGui.PopFont();
        }
    }
}
