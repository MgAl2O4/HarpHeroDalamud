using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowStatus : Window, IDisposable
    {
        private readonly UIReaderBardPerformance uiReader;
        private readonly TrackAssistant trackAssistant;
        private readonly MidiFileManager fileManager;

        private Vector4 colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
        private Vector4 colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
        private Vector4 colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);
        private Vector4 colorDetail = new Vector4(0.2f, 0.9f, 0.9f, 1);

        private const int MaxNameLen = 30;
        private bool isShowingSettings = false;
        private string[] cachedTrackNames;

        public PluginWindowStatus(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, MidiFileManager fileManager) : base("Harp Hero")
        {
            this.uiReader = uiReader;
            this.trackAssistant = trackAssistant;
            this.fileManager = fileManager;
            fileManager.OnImported += (_) => cachedTrackNames = null;

            IsOpen = false;

            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(0, 0), MaximumSize = new Vector2(400, 1000) };
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.AlwaysAutoResize;

            Plugin.CurrentLocManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
        }

        public void Dispose()
        {
            // meh
        }

        private void CacheLocalization()
        {
            // TODO
        }

        public override void Draw()
        {
            if (isShowingSettings)
            {
                DrawSettings();
            }
            else
            {
                DrawStatus();
            }
        }

        private void DrawStatus()
        {
            // import & settings
            ImGui.AlignTextToFramePadding();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
            {
                fileManager.ShowImportDialog();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Import midi file");
            }

            ImGui.SameLine();
            var trackIndentSize = ImGui.GetCursorPosX();
            var fileName = GetTrimmedName(System.IO.Path.GetFileName(fileManager.FilePath));
            ImGui.Text(string.IsNullOrEmpty(fileName) ? "<< Import midi file" : fileName);
            ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - 18);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            {
                isShowingSettings = true;
            }

            DrawTrackDetails(trackIndentSize);

            // tempo overrides
            ImGui.Indent(trackIndentSize);
            if (trackAssistant.musicTrack != null)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Play BPM:");
                ImGui.SameLine();
                ImGui.PushItemWidth(100);

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
                ImGui.TextColored(colorKeysPerSecond, $"( {numKeysPerSecond:0.#} key/s )");
            }
            else
            {
                ImGui.Text("Waiting for music track import...");
            }
            ImGui.Unindent(trackIndentSize);

            ImGui.Separator();
            if (ImGui.Checkbox("Training mode", ref trackAssistant.useWaitingForInput))
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

            if (ImGui.CollapsingHeader($"Track: {GetTrimmedName(trackAssistant.musicTrack.name)}"))
            {
                ImGui.Indent(indentSize);

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Select:");
                ImGui.SameLine();

                ImGui.SetNextItemWidth(200);
                int selectedIdx = fileManager.tracks.IndexOf(trackAssistant.musicTrack);
                if (ImGui.Combo("##trackCombo", ref selectedIdx, cachedTrackNames, cachedTrackNames.Length))
                {
                    if (selectedIdx >= 0 && selectedIdx < fileManager.tracks.Count)
                    {
                        trackAssistant.SetTrack(fileManager.tracks[selectedIdx]);
                    }
                }

                ImGui.SameLine();
                ImGui.Dummy(new Vector2(25, 0));
                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - 18);
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
                        ImGui.SetTooltip("Preview track");
                    }
                }

                var statBlock = trackAssistant.musicTrack.stats;
                var timeSigVarDesc = (statBlock.numTimeSignatures > 1) ? "*" : "";
                bool isRangeValid = trackAssistant.CanPlay;

                ImGui.Text("BPM:");
                ImGui.SameLine();
                ImGui.TextColored(colorDetail, statBlock.beatsPerMinute.ToString());
                ImGui.SameLine(120);
                ImGui.Text("Measure:");
                ImGui.SameLine();
                ImGui.TextColored(colorDetail, $"{statBlock.timeSignature?.Numerator ?? 4}{timeSigVarDesc}");

                ImGui.Text("Bars:");
                ImGui.SameLine();
                ImGui.TextColored(colorDetail, statBlock.numBarsTotal.ToString());
                ImGui.SameLine(120);
                ImGui.Text("Octaves:");
                ImGui.SameLine();
                ImGui.TextColored(isRangeValid ? colorDetail : colorErr, statBlock.GetOctaveRange().ToString());

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Section:");
                ImGui.SameLine();
                ImGui.PushItemWidth(100);
                int[] sectionBars = { statBlock.startBar, statBlock.endBar };
                if (ImGui.InputInt2("bars", ref sectionBars[0]))
                {
                    sectionBars[0] = Math.Max(0, sectionBars[0]);
                    sectionBars[1] = Math.Min(statBlock.endBar, sectionBars[1]);
                    if ((sectionBars[0] >= sectionBars[1]) || (sectionBars[0] == 0 && sectionBars[1] == statBlock.endBar))
                    {
                        trackAssistant.SetTrackSection(-1, -1);
                    }
                    else
                    {
                        trackAssistant.SetTrackSection(sectionBars[0], sectionBars[1]);
                    }
                }

                ImGui.PopItemWidth();
                ImGui.SameLine();
                if (ImGui.Button("Reset"))
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
                    ImGui.SetTooltip("Use game metronome for play controls");
                }

                ImGui.SameLine();
                ImGui.Text("Metronome:");
                ImGui.SameLine();

                if (!trackAssistant.metronomeLink.IsActive)
                {
                    ImGui.TextColored(colorErr, "not visible");
                    ImGui.SameLine();
                    ImGui.Text("(open to sync)");
                    showPlayControls = true;
                }
                else if (trackAssistant.metronomeLink.IsPlaying)
                {
                    ImGui.TextColored(colorOk, "playing");

                    trackAssistant.metronomeLink.GetCurrentTime(out int metronomeBar, out int metronomeBeat, out long metronomeTimeUs);
                    float metronomeScaledMs = metronomeTimeUs * trackAssistant.timeScaling / 1000.0f;
                    float trackScaledMs = trackAssistant.CurrentTime * 1000.0f;
                    float syncErrorMs = Math.Abs(metronomeScaledMs - trackScaledMs);

                    ImGui.SameLine();
                    ImGui.Text($"[{metronomeBar}:{metronomeBeat}]");

                    if (syncErrorMs > 100.0f)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(colorYellow, $"(err: {syncErrorMs:0.#}ms)");
                    }
                }
                else
                {
                    ImGui.TextColored(colorYellow, "stopped");
                    ImGui.SameLine();
                    ImGui.Text($"BPM:{trackAssistant.metronomeLink.BPM}, Measure:{trackAssistant.metronomeLink.Measure}");
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
                        ImGui.TextColored(colorYellow, "Waiting fo key press...");
                    }
                    else
                    {
                        ImGui.TextColored(colorOk, "Playing");
                        ImGui.SameLine();
                        ImGui.Text($"time: {trackAssistant.CurrentTime:0.00}s");
                    }
                }
                else if (trackAssistant.CanPlay)
                {
                    if (ImGuiComponents.IconButton(10, FontAwesomeIcon.Play))
                    {
                        trackAssistant.Start();
                    }

                    ImGui.SameLine();
                    ImGui.Text("Start playing");
                }
                else
                {
                    ImGuiComponents.DisabledButton(FontAwesomeIcon.Play, 11);

                    var errDesc = string.IsNullOrEmpty(fileManager.FilePath) ? "Waiting for music track import..." : "Can't play track, see details";
                    ImGui.SameLine();
                    ImGui.Text(errDesc);
                }
            }
        }

        private void DrawSettings()
        {
            ImGui.AlignTextToFramePadding();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Backward))
            {
                isShowingSettings = false;
            }
            ImGui.SameLine();
            ImGui.Text("Back to status");
            ImGui.Spacing();

            // TODO: everything here is just temp
            ImGui.Text("Look at this! It's a settings screen! woooooooo....");

            if (ImGui.Checkbox("mode note assistant", ref trackAssistant.useNoteAssistant))
            {
                trackAssistant.OnAssistModeChanged();
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
