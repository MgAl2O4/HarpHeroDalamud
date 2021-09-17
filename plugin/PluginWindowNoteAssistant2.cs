using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowNoteAssistant2 : Window, IDisposable, ITickable
    {
        private const float NoMusicUpkeepTime = 3.0f;

        private const uint colorCircleIdleW = 0x80404040;
        private const uint colorCircleIdleB = 0x80808080;

        private const uint colorNoteLowerOctaveB = 0xffecbb09;
        private const uint colorNoteThisOctaveB = 0xff09edbc;
        private const uint colorNoteHigherOctaveB = 0xff0986ec;

        private const uint colorNoteLowerOctaveW = 0xffb63507;
        private const uint colorNoteThisOctaveW = 0xff07b837;
        private const uint colorNoteHigherOctaveW = 0xff0730b6;

        private readonly UIReaderBardPerformance uiReader;
        private readonly NoteUIMapper noteMapper;
        private readonly TrackAssistant trackAssistant;

        private float[] cachedNotePosX = null;
        private float cachedWhiteKeyY = 0;
        private float cachedBlackKeyY = 0;
        private float cachedOverlayAlpha = 1.0f;
        private float noMusicUpkeepRemaining = 0.0f;
        private float markerRadius = 10.0f;
        private int maxMarkersToShow = 1;

        private class NoteMarker
        {
            public int octaveOffset;
            public int noteIdx;
            public int noteCounter;
            public long createdTimeUs;
            public float effectDuration;
            public float effectRemaining;

            public void SetEffectTime(float duration = 1.0f)
            {
                if (effectDuration == 0)
                {
                    effectDuration = duration;
                    effectRemaining = duration;
                }
            }
        }
        private Dictionary<long, NoteMarker> activeNoteMarkers = new();

        public PluginWindowNoteAssistant2(UIReaderBardPerformance uiReader, TrackAssistant trackAssistant, NoteUIMapper noteMapper, Configuration config) : base("Piano Overlay")
        {
            this.uiReader = uiReader;
            this.noteMapper = noteMapper;
            this.trackAssistant = trackAssistant;
            maxMarkersToShow = Math.Min(4, Math.Max(1, config.AssistNote2Markers));

            uiReader.OnVisibilityChanged += OnPerformanceActive;
            trackAssistant.OnPlayChanged += OnPlayChanged;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            BgAlpha = 0.0f;

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

        public void OnPerformanceActive(bool active)
        {
            // this can blink out and back in when changing wide/short during performance
            if (!active)
            {
                IsOpen = false;
                noMusicUpkeepRemaining = 0.0f;
            }
            else if (trackAssistant?.IsPlaying ?? false)
            {
                OnPlayChanged(true);
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (trackAssistant.CanShowNoteAssistant2)
            {
                if (active && uiReader.IsVisible)
                {
                    IsOpen = true;
                    noMusicUpkeepRemaining = NoMusicUpkeepTime;
                    activeNoteMarkers.Clear();
                }
            }
            else
            {
                IsOpen = false;
            }
        }

        public override void PreDraw()
        {
            int numMappedNotes = noteMapper.notes?.Length ?? 0;
            if (numMappedNotes > 0)
            {
                var uiLowC = uiReader.cachedState.keys[noteMapper.notes[0].uiIndex];
                var uiHighC = uiReader.cachedState.keys[noteMapper.notes[numMappedNotes - 1].uiIndex];
                var uiLowCS = uiReader.cachedState.keys[noteMapper.notes[1].uiIndex];

                float upkeepPct = (noMusicUpkeepRemaining / NoMusicUpkeepTime);
                cachedOverlayAlpha = upkeepPct * upkeepPct;

                Position = new Vector2(uiLowC.pos.X, uiLowC.pos.Y);
                Size = new Vector2(uiHighC.pos.X + uiHighC.size.X - uiLowC.pos.X, uiLowC.size.Y);

                if (cachedNotePosX == null || cachedNotePosX.Length != numMappedNotes)
                {
                    cachedNotePosX = new float[numMappedNotes];
                }

                for (int idx = 0; idx < numMappedNotes; idx++)
                {
                    var uiKey = uiReader.cachedState.keys[noteMapper.notes[idx].uiIndex];
                    cachedNotePosX[idx] = uiKey.pos.X + (uiKey.size.X * 0.5f);
                }

                cachedWhiteKeyY = uiLowC.pos.Y + uiLowC.size.Y - 25;
                cachedBlackKeyY = uiLowCS.pos.Y + uiLowCS.size.Y - 25;
                markerRadius = uiLowC.size.X * 0.3f;
            }
            else
            {
                cachedNotePosX = null;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (IsOpen)
            {
                if (trackAssistant == null || !trackAssistant.IsPlaying)
                {
                    noMusicUpkeepRemaining -= deltaSeconds;
                    if (noMusicUpkeepRemaining <= 0.0f)
                    {
                        IsOpen = false;
                    }
                }
                else
                {
                    UpdateNoteMarkers();
                }

                if (activeNoteMarkers.Count > 0)
                {
                    TickMarkers(deltaSeconds);
                }
            }
        }

        private void TickMarkers(float deltaSeconds)
        {
            var removeList = new List<long>();
            foreach (var kvp in activeNoteMarkers)
            {
                var marker = kvp.Value;
                if (marker.effectRemaining > 0.0f)
                {
                    marker.effectRemaining -= deltaSeconds;
                    if (marker.effectRemaining <= 0.0f)
                    {
                        removeList.Add(kvp.Key);
                    }
                }
            }

            foreach (var markerId in removeList)
            {
                activeNoteMarkers.Remove(markerId);
            }
        }

        public override void Draw()
        {
            if (cachedNotePosX != null)
            {
                var drawList = ImGui.GetWindowDrawList();
                var currentTimeUs = trackAssistant.musicViewer?.TimeUs ?? 0;

                for (int idx = 0; idx < noteMapper.notes.Length; idx++)
                {
                    bool isHalfStep = noteMapper.octaveNotes[noteMapper.notes[idx].octaveIdx].isHalfStep;
                    float useDrawY = isHalfStep ? cachedBlackKeyY : cachedWhiteKeyY;
                    uint drawColor = GetAlphaModulatedColor(isHalfStep ? colorCircleIdleB : colorCircleIdleW);

                    drawList.AddCircle(new Vector2(cachedNotePosX[idx], useDrawY), markerRadius, drawColor);
                }

                foreach (var kvp in activeNoteMarkers)
                {
                    int noteIdx = kvp.Value.noteIdx;

                    bool isHalfStep = noteMapper.octaveNotes[noteMapper.notes[noteIdx].octaveIdx].isHalfStep;
                    var pos = new Vector2(cachedNotePosX[noteIdx], isHalfStep ? cachedBlackKeyY : cachedWhiteKeyY);
                    var noteColor =
                        (kvp.Value.octaveOffset == 0) ? (isHalfStep ? colorNoteThisOctaveB : colorNoteThisOctaveW) :
                        (kvp.Value.octaveOffset < 0) ? (isHalfStep ? colorNoteLowerOctaveB : colorNoteLowerOctaveW) :
                        (isHalfStep ? colorNoteHigherOctaveB : colorNoteHigherOctaveW);

                    if (kvp.Value.effectDuration > 0)
                    {
                        var pctEffect = 1 - (kvp.Value.effectRemaining / kvp.Value.effectDuration);
                        var pctSize = 1 - Math.Pow(1 - pctEffect, 4);
                        var pctAlpha = 1 - pctEffect;

                        var modColor = GetAlphaModulatedColor(noteColor, pctAlpha);
                        drawList.AddCircleFilled(pos, markerRadius * (1 + (float)pctSize), modColor);
                    }
                    else
                    {
                        var modColor = GetAlphaModulatedColor(noteColor);

                        var timeToPlayUs = kvp.Key - currentTimeUs;
                        if (timeToPlayUs < (100 * 1000))
                        {
                            drawList.AddCircleFilled(pos, markerRadius * 0.75f, modColor);
                        }

                        var radiusMult = (maxMarkersToShow - kvp.Value.noteCounter) * 1.0f / maxMarkersToShow;
                        drawList.AddCircle(pos, markerRadius * 0.75f * radiusMult, modColor, 32, 4.0f);
                    }
                }

                uint GetAlphaModulatedColor(uint color, float modAlpha = 1.0f)
                {
                    var drawAlpha = ((color >> 24) / 255.0f) * cachedOverlayAlpha * modAlpha;
                    return (color & 0x00ffffff) | (uint)Math.Min(255, drawAlpha * 255) << 24;
                }
            }
        }

        private void UpdateNoteMarkers()
        {
            if (trackAssistant == null || trackAssistant.musicViewer == null)
            {
                return;
            }

            // find next MaxMarkersToShow unique markers for notes
            var currentTimeUs = trackAssistant.CurrentTimeUs;
            var maxMarkerTime = currentTimeUs + (trackAssistant.musicViewer.TimeRangeUs / 2);
            long lastPlayingTimeUs = 0;
            int noteCounter = 0;
            bool foundLastPlayingNote = false;

            foreach (var noteInfo in trackAssistant.musicViewer.shownNotes)
            {
                if (!noteMapper.GetMappedNoteIdx(noteInfo.note, out int mappedNoteIdx, out int octaveOffset))
                {
                    continue;
                }

                if (noteInfo.startUs >= currentTimeUs)
                {
                    if (!activeNoteMarkers.ContainsKey(noteInfo.startUs))
                    {
                        activeNoteMarkers.Add(noteInfo.startUs, new NoteMarker() { noteIdx = mappedNoteIdx, octaveOffset = octaveOffset, createdTimeUs = currentTimeUs });
                    }

                    activeNoteMarkers[noteInfo.startUs].noteCounter = noteCounter;
                    noteCounter++;

                    if (noteInfo.startUs >= maxMarkerTime || noteCounter >= maxMarkersToShow)
                    {
                        break;
                    }
                }
                else
                {
                    // keep overwriting, it will settle at latest playing
                    lastPlayingTimeUs = noteInfo.startUs;
                    foundLastPlayingNote = true;
                }
            }

            // update current effects: switch to playing state
            if (foundLastPlayingNote && activeNoteMarkers.TryGetValue(lastPlayingTimeUs, out var playingMarkerOb))
            {
                playingMarkerOb.SetEffectTime(0.5f);
            }
        }
    }
}
