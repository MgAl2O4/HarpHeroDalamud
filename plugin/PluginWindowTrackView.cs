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
        private readonly TrackAssistant trackAssistant;

        private MidiTrackWrapper shownTrack;
        private MidiTrackViewer[] trackViewers;
        private int shownSecond;
        private int maxSeconds;

        private int minValidNoteNumber;
        private int maxValidNoteNumber;

        private const uint colorTimeLineBeat = 0xff004040;
        private const uint colorTimeLineBar = 0xff008080;
        private const uint colorNoteOrg = 0xff404040;
        private const uint colorNote = 0xffa8a810;
        private const uint colorNoteInvalid = 0xff1010a8;

        public PluginWindowTrackView(TrackAssistant trackAssistant) : base("Track View")
        {
            this.trackAssistant = trackAssistant;

            IsOpen = false;

            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(600, 400), MaximumSize = new Vector2(2000, 1000) };
            SizeCondition = ImGuiCond.FirstUseEver;
            BgAlpha = 1.0f;
        }

        public void Dispose()
        {
            // meh
        }

        public void OnShowTrack(MidiTrackWrapper track)
        {
            shownTrack = track;

            if (track != null)
            {
                var trackViewer = new MidiTrackViewer(shownTrack);
                var trackViewerOrg = new MidiTrackViewer(shownTrack.midiTrackOrg, shownTrack.tempoMap);

                trackViewers = new MidiTrackViewer[2] { trackViewer, trackViewerOrg };
                foreach (var viewer in trackViewers)
                {
                    viewer.generateBarData = (viewer == trackViewer);
                    viewer.generateBindingData = false;
                    viewer.timeWindowSecondsAhead = 18.0f;
                    viewer.timeWindowSecondsBehind = 2.0f;

                    viewer.SetTimeUs(0);
                }

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

            var drawList = ImGui.GetWindowDrawList();

            ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionWidth());
            if (ImGui.SliderInt("##trackViewerSecond", ref shownSecond, 0, maxSeconds))
            {
                long timeUs = (long)shownSecond * 1000 * 1000;
                trackViewers[0].SetTimeUs(timeUs);
                trackViewers[1].SetTimeUs(timeUs);
            }

            var contentRegionMin = ImGui.GetWindowContentRegionMin() + new Vector2(0, 40) + ImGui.GetWindowPos();
            var contentRegionMax = ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos();
            var spaceX = contentRegionMax.X - contentRegionMin.X;
            var spaceY = contentRegionMax.Y - contentRegionMin.Y;
            var trackViewer = trackViewers[0];
            var noteNumberRange = shownTrack.statsOrg.maxNote - shownTrack.statsOrg.minNote;
            var startTimeUs = trackViewers[0].TimeRangeStartUs;
            var timeRangeUs = trackViewers[0].TimeRangeUs;

            // draw bar lines
            foreach (var barTimeUs in trackViewer.shownBarLines)
            {
                float alphaX = 1.0f * (barTimeUs - startTimeUs) / timeRangeUs;
                if (alphaX >= 0.0f && alphaX <= 1.0f)
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
