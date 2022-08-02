using Dalamud;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowTrackView : Window, IDisposable
    {
        private MidiTrackWrapper shownTrack;
        private MidiTrackViewer[] trackViewers;
        private int shownSecond;
        private int maxSeconds;

        private int minValidNoteNumber;
        private int maxValidNoteNumber;
        private int shownTransposeOffset;

        private const uint colorTimeLineBeat = UIColors.colorGrayYellowDark;
        private const uint colorTimeLineBar = UIColors.colorGrayYellow;
        private const uint colorNoteOrg = UIColors.colorGray25;
        private const uint colorNote = UIColors.colorGreenDark;
        private const uint colorNoteInvalid = UIColors.colorRed;

        private string locDetailsHint;
        private string locDetailsHeader;

        public PluginWindowTrackView() : base("Track View")
        {
            IsOpen = false;

            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(600, 400), MaximumSize = new Vector2(2000, 1000) };
            SizeCondition = ImGuiCond.FirstUseEver;
            BgAlpha = 1.0f;
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
            locDetailsHeader = Localization.Localize("TW_StandaloneHeader", "Note transform preview. For more in depth inspection, please use converter tool from plugin's git repository.");
            locDetailsHint = Localization.Localize("TW_StandaloneHint", "Plugin list > Harp Hero > Visit plugin URL");
        }

        public void OnShowTrack(MidiTrackWrapper track)
        {
            shownTrack = track;

            if (track != null)
            {
                var trackViewer = new MidiTrackViewer(shownTrack);
                SetTrackViewerParams(trackViewer, true);

                var trackViewerOrg = new MidiTrackViewer(shownTrack.midiTrackOrg, shownTrack.tempoMap);
                SetTrackViewerParams(trackViewerOrg, false);

                trackViewers = new MidiTrackViewer[2] { trackViewer, trackViewerOrg };
                shownTransposeOffset = shownTrack.TransposeOffset;

                FindMidOctave();
                shownSecond = 0;
                maxSeconds = (int)(shownTrack.GetDurationUs() / (1000 * 1000));
                IsOpen = true;
            }
            else
            {
                trackViewers = null;
                shownSecond = 0;
                maxSeconds = 1;
                IsOpen = false;
            }
        }

        public override void Draw()
        {
            if (trackViewers == null || shownTrack == null || trackViewers.Length != 2)
            {
                return;
            }

            if (shownTrack.TransposeOffset != shownTransposeOffset)
            {
                RefreshTransposeView();
                shownTransposeOffset = shownTrack.TransposeOffset;
            }

            var drawList = ImGui.GetWindowDrawList();
            var availWindowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

            ImGui.SetNextItemWidth(availWindowWidth);
            if (ImGui.SliderInt("##trackViewerSecond", ref shownSecond, 0, maxSeconds))
            {
                long timeUs = (long)shownSecond * 1000 * 1000;
                trackViewers[0].SetTimeUs(timeUs);
                trackViewers[1].SetTimeUs(timeUs);
            }

            ImGui.Text(locDetailsHeader);
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(locDetailsHint);

            var contentRegionMin = ImGui.GetWindowContentRegionMin() + new Vector2(0, 50) + ImGui.GetWindowPos();
            var contentRegionMax = ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos();
            var spaceX = contentRegionMax.X - contentRegionMin.X;
            var spaceY = contentRegionMax.Y - contentRegionMin.Y;
            var trackViewer = trackViewers[0];
            var noteNumberRange = Math.Max(1, shownTrack.statsOrg.maxNote - shownTrack.statsOrg.minNote);
            var startTimeUs = trackViewers[0].TimeRangeStartUs;
            var timeRangeUs = trackViewers[0].TimeRangeUs;

            // draw bar lines
            foreach (var barTimeUs in trackViewer.shownBarLines)
            {
                float alphaX = 1.0f * (barTimeUs - startTimeUs) / timeRangeUs;
                if (alphaX >= 0.0f && alphaX <= 1.0f && barTimeUs >= 0)
                {
                    var posX = contentRegionMin.X + (spaceX * alphaX);
                    var barNum = TimeConverter.ConvertTo<BarBeatTicksTimeSpan>(new MetricTimeSpan(barTimeUs), shownTrack.tempoMap).Bars;

                    drawList.AddLine(new Vector2(posX, contentRegionMin.Y), new Vector2(posX, contentRegionMax.Y), colorTimeLineBar, 1.5f);
                    drawList.AddText(new Vector2(posX + 5, contentRegionMin.Y), colorTimeLineBar, barNum.ToString());
                }
            }
            foreach (var beatTimeUs in trackViewer.shownBeatLines)
            {
                float alphaX = 1.0f * (beatTimeUs - startTimeUs) / timeRangeUs;
                if (alphaX >= 0.0f && alphaX <= 1.0f)
                {
                    var posX = contentRegionMin.X + (int)(spaceX * alphaX);
                    drawList.AddLine(new Vector2(posX, contentRegionMin.Y), new Vector2(posX, contentRegionMax.Y), colorTimeLineBeat, 1.0f);
                }
            }

            // draw org notes
            foreach (var noteInfo in trackViewers[1].shownNotes)
            {
                DrawNote(noteInfo, colorNoteOrg, colorNoteOrg);
            }

            // draw active notes
            foreach (var noteInfo in trackViewers[0].shownNotes)
            {
                DrawNote(noteInfo, colorNote, colorNoteInvalid);
            }

            void DrawNote(MidiTrackViewer.NoteInfo noteInfo, uint noteColor, uint invalidColor)
            {
                float alphaX0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.startUs - startTimeUs) / timeRangeUs));
                float alphaX1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.endUs - startTimeUs) / timeRangeUs));
                float alphaY = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.note.NoteNumber - shownTrack.statsOrg.minNote) / noteNumberRange));

                var posX0 = contentRegionMin.X + (spaceX * alphaX0);
                var posX1 = contentRegionMin.X + (spaceX * alphaX1);
                var posY = contentRegionMin.Y + (spaceY * (1.0f - alphaY));

                uint drawColor = (noteInfo.note.NoteNumber < minValidNoteNumber || noteInfo.note.NoteNumber > maxValidNoteNumber) ? invalidColor : noteColor;

                const float noteHalfHeight = 2.5f;
                drawList.AddRectFilled(new Vector2(posX0, posY + noteHalfHeight), new Vector2(posX1, posY - noteHalfHeight), drawColor);
                drawList.AddText(new Vector2(posX0, posY - ImGui.GetTextLineHeight()), drawColor, noteInfo.note.ToString());
            }
        }

        private void SetTrackViewerParams(MidiTrackViewer viewer, bool isMainTrack)
        {
            viewer.generateBarData = isMainTrack;
            viewer.generateBindingData = false;
            viewer.timeWindowSecondsAhead = 18.0f;
            viewer.timeWindowSecondsBehind = 2.0f;

            viewer.SetTimeUs(0);
        }

        private void RefreshTransposeView()
        {
            if (trackViewers != null && shownTrack != null)
            {
                trackViewers[0] = new MidiTrackViewer(shownTrack);
                SetTrackViewerParams(trackViewers[0], true);
            }
        }

        private void FindMidOctave()
        {
            var minNote = SevenBitNumber.MaxValue;
            var maxNote = SevenBitNumber.MinValue;
            minValidNoteNumber = -1000;
            maxValidNoteNumber = 1000;

            foreach (var note in shownTrack.midiTrack.GetNotes())
            {
                if (minNote > note.NoteNumber)
                {
                    minNote = note.NoteNumber;
                }
                if (maxNote < note.NoteNumber)
                {
                    maxNote = note.NoteNumber;
                }

                bool isValid = shownTrack.stats.IsOctaveRangeValid(minNote, maxNote, out int dummyId);
                if (isValid)
                {
                    minValidNoteNumber = minNote;
                    maxValidNoteNumber = maxNote;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
