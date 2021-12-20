using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowScore : Window, IDisposable
    {
        private readonly UIReaderBardPerformance uiReader;

        private int lastShownRankMajor;
        private Vector2[] cachedRankMinorPos;

        private uint colorMinorEmpty = UIColors.colorGray33;
        private uint[] colorMinor = { UIColors.colorGreen, UIColors.colorYellow, UIColors.colorRed };

        public PluginWindowScore(UIReaderBardPerformance uiReader) : base("Track score")
        {
            this.uiReader = uiReader;

            Service.trackAssistant.OnTrackChanged += (_) => IsOpen = false;
            Service.trackAssistant.OnPlayChanged += (active) => IsOpen = (uiReader.IsVisible && Service.config.ShowScore);
            uiReader.OnVisibilityChanged += (_) => IsOpen = false;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            RespectCloseHotkey = false;
            ForceMainWindow = true;

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;
        }

        public void Dispose()
        {
            // meh
        }

        public override void OnOpen()
        {
            cachedRankMinorPos = null;
        }

        public override void PreDraw()
        {
            if (uiReader.cachedState != null && uiReader.IsVisible)
            {
                const int offsetX = 10;

                var size = uiReader.cachedState.keysSize.Y;
                if (size > uiReader.cachedState.keysPos.X - offsetX)
                {
                    size = Math.Min(100, uiReader.cachedState.keysPos.X - offsetX);
                }

                Position = new Vector2(uiReader.cachedState.keysPos.X - offsetX - size, uiReader.cachedState.keysPos.Y);

                size /= ImGuiHelpers.GlobalScale;
                Size = new Vector2(size, size);
            }
        }

        public override void Draw()
        {
            var drawList = ImGui.GetWindowDrawList();
            var contentMin = ImGui.GetWindowContentRegionMin();
            var contentMax = ImGui.GetWindowContentRegionMax();
            var centerPos = ImGui.GetWindowPos() + (contentMin + contentMax) * 0.5f;
            var drawSize = (contentMax.X - contentMin.X);

            ImGui.SetWindowFontScale(3.0f);

            var rankText = Service.trackAssistant.scoreTracker.RankName;
            var rankTextSize = ImGui.CalcTextSize(rankText);
            ImGui.SetCursorScreenPos(centerPos - (rankTextSize * 0.5f));
            ImGui.Text(rankText);

            UpdateRankMinorSlots(drawSize * 0.4f);
            if (cachedRankMinorPos != null)
            {
                int thr1 = cachedRankMinorPos.Length * 4 / 10;
                int numThr2 = Math.Max(2, cachedRankMinorPos.Length * 3 / 10); ;
                int thr2 = cachedRankMinorPos.Length - numThr2;

                int numToShow = Service.trackAssistant.scoreTracker.RankMinor;
                var markerRadius = 5.0f;

                for (int idx = 0; idx < numToShow; idx++)
                {
                    int colorIdx = (idx >= thr2) ? 2 : (idx >= thr1) ? 1 : 0;
                    drawList.AddCircleFilled(centerPos + cachedRankMinorPos[idx], markerRadius, colorMinor[colorIdx]);
                }
                for (int idx = numToShow; idx < cachedRankMinorPos.Length; idx++)
                {
                    drawList.AddCircle(centerPos + cachedRankMinorPos[idx], markerRadius, colorMinorEmpty);
                }
            }

            // TODO: fx for rank changes?
            lastShownRankMajor = Service.trackAssistant.scoreTracker.RankMajor;
        }

        private void UpdateRankMinorSlots(float radius)
        {
            if (cachedRankMinorPos == null ||
                Service.trackAssistant.scoreTracker.RankMinorMax != cachedRankMinorPos.Length)
            {
                if (Service.trackAssistant.scoreTracker.RankMinorMax > 0)
                {
                    cachedRankMinorPos = new Vector2[Service.trackAssistant.scoreTracker.RankMinorMax];

                    float angle = (float)(-Math.PI / 2);
                    float angleInc = (float)(Math.PI * 2 / Service.trackAssistant.scoreTracker.RankMinorMax);

                    for (int idx = 0; idx < Service.trackAssistant.scoreTracker.RankMinorMax; idx++)
                    {
                        cachedRankMinorPos[idx] = new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
                        angle += angleInc;
                    }
                }
                else
                {
                    cachedRankMinorPos = null;
                }
            }
        }
    }
}
