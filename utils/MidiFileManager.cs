﻿using Dalamud.Logging;
using Melanchall.DryWetMidi.Core;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class MidiFileManager
    {
        public string FilePath;
        public List<MidiTrackWrapper> tracks = new();

        public Action<MidiFileManager> OnImported;

        public void ImportFile(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                FilePath = path;
                PluginLog.Log("importing: {0}", path);

                try
                {
                    var midiFile = MidiFile.Read(path);
                    tracks = MidiTrackWrapper.GenerateTracks(midiFile);

                    OnImported?.Invoke(this);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "import failed");
                    FilePath = null;
                    tracks.Clear();
                }
            }
        }
    }
}
