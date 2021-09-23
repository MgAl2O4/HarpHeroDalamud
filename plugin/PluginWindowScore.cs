using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowScore : Window, IDisposable
    {
        private readonly UIReaderBardPerformance uiReader;
        private readonly TrackAssistant trackAssistant;

        private int lastShownRankMajor;
        private Vector2[] cachedRankMinorPos;

        private uint colorMinorEmpty = 0xff5c5b4c;
        private uint[] colorMinor = { 0xff09edbc, 0xff40cbf9, 0xff5b71ff };

        public PluginWindowScore(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, Configuration config) : base("Track score")
        {
            this.uiReader = uiReader;
            this.trackAssistant = trackAssistant;

            trackAssistant.OnTrackChanged += (_) => IsOpen = false;
            trackAssistant.OnPlayChanged += (active) => IsOpen = (uiReader.IsVisible && config.ShowScore);
            uiReader.OnVisibilityChanged += (_) => IsOpen = false;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            RespectCloseHotkey = false;

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

            var rankText = trackAssistant.scoreTracker.RankName;
            var rankTextSize = ImGui.CalcTextSize(rankText);
            ImGui.SetCursorScreenPos(centerPos - (rankTextSize * 0.5f));
            ImGui.Text(rankText);

            UpdateRankMinorSlots(drawSize * 0.4f);
            if (cachedRankMinorPos != null)
            {
                int thr1 = cachedRankMinorPos.Length * 4 / 10;
                int numThr2 = Math.Max(2, cachedRankMinorPos.Length * 3 / 10); ;
                int thr2 = cachedRankMinorPos.Length - numThr2;

                int numToShow = trackAssistant.scoreTracker.RankMinor;
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
            lastShownRankMajor = trackAssistant.scoreTracker.RankMajor;
        }

        private void UpdateRankMinorSlots(float radius)
        {
            if (cachedRankMinorPos == null ||
                trackAssistant.scoreTracker.RankMinorMax != cachedRankMinorPos.Length)
            {
                if (trackAssistant.scoreTracker.RankMinorMax > 0)
                {
                    cachedRankMinorPos = new Vector2[trackAssistant.scoreTracker.RankMinorMax];

                    float angle = (float)(-Math.PI / 2);
                    float angleInc = (float)(Math.PI * 2 / trackAssistant.scoreTracker.RankMinorMax);

                    for (int idx = 0; idx < trackAssistant.scoreTracker.RankMinorMax; idx++)
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
