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

        public PluginWindowStatus(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, MidiFileManager fileManager) : base("Harp Hero")
        {
            this.uiReader = uiReader;
            this.trackAssistant = trackAssistant;
            this.fileManager = fileManager;

            IsOpen = false;

            Size = new Vector2(350, ImGui.GetTextLineHeight() * 11.0f);
            SizeConstraints = new WindowSizeConstraints() { MinimumSize = this.Size.Value, MaximumSize = new Vector2(3000, 3000) };
            SizeCondition = ImGuiCond.FirstUseEver;

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
            // debug stuff: file selection & stats
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
            ImGui.Text(string.IsNullOrEmpty(fileManager.FilePath) ? "<< import midi file" : fileManager.FilePath);

            if (trackAssistant.musicTrack != null)
            {
                ImGui.Text($"track: {trackAssistant.musicTrack.name}");

                var statBlock = trackAssistant.musicTrack.stats;
                var timeSigVarDesc = (statBlock.numTimeSignatures > 1) ? "*" : "";
                ImGui.Text($"BPM: {statBlock.beatsPerMinute}, measure:{statBlock.timeSignature.Numerator}{timeSigVarDesc}, valid:{trackAssistant.CanPlay}");

                if (trackAssistant.TargetBPM > 0)
                {
                    ImGui.Text($"target BPM:{trackAssistant.TargetBPM}");
                    ImGui.SameLine();
                }

                var numKeysPerSecond = trackAssistant.GetScaledKeysPerSecond();
                var colorKeysPerSecond = (numKeysPerSecond <= 1.0f) ? colorOk : (numKeysPerSecond <= 2.0f) ? colorYellow : colorErr;
                ImGui.TextColored(colorKeysPerSecond, $"{numKeysPerSecond:0.#} keys/s");
            }

            // debug stuff: play & more stats
            ImGui.AlignTextToFramePadding();
            if (trackAssistant.isPlaying)
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
                {
                    trackAssistant.Stop();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Stop");
                }

                ImGui.SameLine();
                ImGui.Text($"Playing stuff, time: {trackAssistant.currentTimeUs / 1000000.0f:0.00}s");
            }
            else if (trackAssistant.CanPlay)
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
                {
                    trackAssistant.Start();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Play");
                }
            }

            if (ImGui.Checkbox("Mode: note assistant", ref trackAssistant.isNoteAssistant))
            {
                trackAssistant.OnAssistModeChanged();
            }
        }
    }
}
