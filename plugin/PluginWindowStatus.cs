using Dalamud;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class PluginWindowStatus : Window, IDisposable
    {
        private readonly UIReaderBardPerformance uiReader;

        private Vector4 colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
        private Vector4 colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
        private Vector4 colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);

        private string locStatus;
        private string locStatusNotActive;
        private string locStatusActive;

        public PluginWindowStatus(UIReaderBardPerformance uiReader) : base("Harp Hero")
        {
            this.uiReader = uiReader;

            IsOpen = true;

            Size = new Vector2(350, ImGui.GetTextLineHeight() * 8);
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
            locStatus = Localization.Localize("ST_Status", "Status:");
            locStatusNotActive = Localization.Localize("ST_StatusNotActive", "Not active");
            locStatusActive = Localization.Localize("ST_StatusActive", "Active");
        }

        public override void Draw()
        {
            ImGui.Text(locStatus);
            ImGui.SameLine();

            var statusDesc =
                uiReader.HasErrors ? uiReader.status.ToString() :
                !uiReader.IsVisible ? locStatusNotActive :
                locStatusActive;

            var statusColor = uiReader.HasErrors ? colorErr : colorOk;

            ImGui.TextColored(statusColor, statusDesc);
        }
    }
}
