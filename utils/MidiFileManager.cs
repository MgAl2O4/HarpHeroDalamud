using Dalamud.Logging;
using Melanchall.DryWetMidi.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HarpHero
{
    public class MidiFileManager
    {
        public string FilePath;
        public List<MidiTrackWrapper> tracks = new();

        public Action<MidiFileManager> OnImported;

        public async void ShowImportDialog()
        {
            // TODO: async for real?
            var path = await ShowDialogWorker();
            ImportFile(path);
        }

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

        private Task<string> ShowDialogWorker()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".mid";
            dialog.Filter = "Midi file (.mid)|*.mid";

            var result = dialog.ShowDialog();
            if (result == true)
            {
                return Task.FromResult(dialog.FileName);
            }

            return Task.FromResult<string>(null);
        }
    }
}
