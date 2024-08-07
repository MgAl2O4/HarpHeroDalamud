using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HarpHero
{
    public class PluginWindowNoteAssistant2 : Window, IDisposable, ITickable
    {
        private const float NoMusicUpkeepTime = 3.0f;

        private const uint colorCircleIdleW = UIColors.colorGray25;
        private const uint colorCircleIdleB = UIColors.colorGray50;

        private const uint colorNoteLowerOctaveB = UIColors.colorBlue;
        private const uint colorNoteThisOctaveB = UIColors.colorGreen;
        private const uint colorNoteHigherOctaveB = UIColors.colorRed;

        private const uint colorNoteLowerOctaveW = UIColors.colorBlueDark;
        private const uint colorNoteThisOctaveW = UIColors.colorGreenDark;
        private const uint colorNoteHigherOctaveW = UIColors.colorRedDark;

        private readonly UIReaderBardPerformance uiReader;
        private readonly NoteUIMapper noteMapper;

        private float[]? cachedNotePosX = null;
        private float cachedWhiteKeyY = 0;
        private float cachedBlackKeyY = 0;
        private float cachedOverlayAlpha = 1.0f;
        private float noMusicUpkeepRemaining = 0.0f;
        private float markerRadius = 10.0f;
        private int maxMarkersToShow = 1;
        private long markerWarnTimeUs = 100 * 1000;

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

        public PluginWindowNoteAssistant2(UIReaderBardPerformance uiReader, NoteUIMapper noteMapper) : base("Piano Overlay")
        {
            this.uiReader = uiReader;
            this.noteMapper = noteMapper;

            uiReader.OnVisibilityChanged += OnPerformanceActive;
            Service.trackAssistant.OnPlayChanged += OnPlayChanged;

            IsOpen = false;

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            BgAlpha = 0.0f;
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

        public void OnPerformanceActive(bool active)
        {
            // this can blink out and back in when changing wide/short during performance
            if (!active)
            {
                IsOpen = false;
                noMusicUpkeepRemaining = 0.0f;
            }
            else if (Service.trackAssistant?.IsPlaying ?? false)
            {
                OnPlayChanged(true);
            }
        }

        public void OnPlayChanged(bool active)
        {
            if (Service.trackAssistant.CanShowNoteAssistant2)
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

        public override void OnOpen()
        {
            Plugin.TickScheduler.Register(this);

            maxMarkersToShow = Math.Min(4, Math.Max(0, Service.config.AssistNote2Markers));
            markerWarnTimeUs = Math.Min(1000, Math.Max(1, Service.config.AssistNote2WarnMs)) * 1000;
        }

        public override void PreDraw()
        {
            int numMappedNotes = noteMapper.notes?.Length ?? 0;
            if (numMappedNotes > 0 && noteMapper.notes != null)
            {
                var viewportOffset = ImGui.GetMainViewport().Pos;

                var uiLowC = uiReader.cachedState.keys[noteMapper.notes[0].uiIndex];
                var uiHighC = uiReader.cachedState.keys[noteMapper.notes[numMappedNotes - 1].uiIndex];
                var uiLowCS = uiReader.cachedState.keys[noteMapper.notes[1].uiIndex];

                float upkeepPct = (noMusicUpkeepRemaining / NoMusicUpkeepTime);
                cachedOverlayAlpha = upkeepPct * upkeepPct * Service.config.AssistBgAlpha;

                Position = new Vector2(uiLowC.pos.X, uiLowC.pos.Y);
                Size = new Vector2(uiHighC.pos.X + uiHighC.size.X - uiLowC.pos.X, uiLowC.size.Y) / ImGuiHelpers.GlobalScale;

                if (cachedNotePosX == null || cachedNotePosX.Length != numMappedNotes)
                {
                    cachedNotePosX = new float[numMappedNotes];
                }

                for (int idx = 0; idx < numMappedNotes; idx++)
                {
                    var uiKey = uiReader.cachedState.keys[noteMapper.notes[idx].uiIndex];
                    cachedNotePosX[idx] = uiKey.pos.X + (uiKey.size.X * 0.5f) + viewportOffset.X;
                }

                cachedWhiteKeyY = uiLowC.pos.Y + uiLowC.size.Y - 25 + viewportOffset.Y;
                cachedBlackKeyY = uiLowCS.pos.Y + uiLowCS.size.Y - 25 + viewportOffset.Y;
                markerRadius = uiLowC.size.X * 0.3f;
            }
            else
            {
                cachedNotePosX = null;
            }
        }

        public bool Tick(float deltaSeconds)
        {
            if (IsOpen)
            {
                if (Service.trackAssistant == null || !Service.trackAssistant.IsPlaying)
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

            // can tick? only if open
            return IsOpen;
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
            if (cachedNotePosX != null && maxMarkersToShow > 0 && noteMapper.notes != null)
            {
                var drawList = ImGui.GetWindowDrawList();
                var currentTimeUs = Service.trackAssistant.musicViewer?.TimeUs ?? 0;

                for (int idx = 0; idx < noteMapper.notes.Length; idx++)
                {
                    bool isHalfStep = noteMapper.octaveNotes[noteMapper.notes[idx].octaveIdx].isHalfStep;
                    float useDrawY = isHalfStep ? cachedBlackKeyY : cachedWhiteKeyY;
                    uint drawColor = GetAlphaModulatedColor(isHalfStep ? colorCircleIdleB : colorCircleIdleW, 0.5f);

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
                        if (timeToPlayUs < markerWarnTimeUs)
                        {
                            drawList.AddCircleFilled(pos, markerRadius * 0.75f, modColor);
                        }

                        var radiusMult = (maxMarkersToShow - kvp.Value.noteCounter) * 1.0f / maxMarkersToShow;
                        drawList.AddCircle(pos, markerRadius * 0.75f * radiusMult, modColor, 32, 4.0f);
                    }
                }

                uint GetAlphaModulatedColor(uint color, float modAlpha = 1.0f)
                {
                    return UIColors.GetAlphaModulated(color, modAlpha * cachedOverlayAlpha);
                }
            }
        }

        private void UpdateNoteMarkers()
        {
            if (Service.trackAssistant == null || Service.trackAssistant.musicViewer == null || maxMarkersToShow <= 0)
            {
                return;
            }

            // find next MaxMarkersToShow unique markers for notes
            var currentTimeUs = Service.trackAssistant.CurrentTimeUs;
            var maxMarkerTime = currentTimeUs + (Service.trackAssistant.musicViewer.TimeRangeUs / 2);
            long lastPlayingTimeUs = 0;
            int noteCounter = 0;
            bool foundLastPlayingNote = false;

            foreach (var noteInfo in Service.trackAssistant.musicViewer.shownNotes)
            {
                if (!noteMapper.GetMappedNoteIdx(noteInfo.note, out int mappedNoteIdx, out int octaveOffset))
                {
                    continue;
                }

                bool isNoteIgnored =
                    (Service.trackAssistant.TrackStartTimeUs >= 0 && noteInfo.startUs < Service.trackAssistant.TrackStartTimeUs) ||
                    (Service.trackAssistant.TrackEndTimeUs >= 0 && noteInfo.endUs >= Service.trackAssistant.TrackEndTimeUs);
                if (isNoteIgnored)
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
