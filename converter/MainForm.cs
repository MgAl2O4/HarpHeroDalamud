using HarpHero;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HarpHeroConverter
{
    public partial class MainForm : Form
    {
        private float midiTimeScaling = 1.0f;
        private long currentTimeUs = 0;

        private string loadedFilePath;
        private MidiFile loadedFile;
        private List<MidiTrackWrapper> loadedTracks = new List<MidiTrackWrapper>();

        private MidiTrackWrapper musicTrack;
        private MidiTrackPlayer musicPlayer;
        private MidiTrackViewer musicViewer;
        private MidiTrackViewer musicViewerOrg;
        private bool isValid3Octave = false;
        private bool isValid5Octave = false;
        private int midOctaveId = -1;

        private Font drawFontSmall = new Font(new FontFamily(GenericFontFamilies.Monospace), 8.0f);

        private Stopwatch stopwatch = new Stopwatch();
        private Brush[] brushBindings = new Brush[]
        {
            new SolidBrush(Color.FromArgb(0x4C, 0x5B, 0x5C)),
            new SolidBrush(Color.FromArgb(0xBC, 0xED, 0x09)),
            new SolidBrush(Color.FromArgb(0xF9, 0xCB, 0x40)),
            new SolidBrush(Color.FromArgb(0xFF, 0x71, 0x5B)),
        };
        private Brush[] brushBindingsDark = new Brush[]
        {
            new SolidBrush(Color.FromArgb(0x4C, 0x5B, 0x5C)),
            new SolidBrush(Color.FromArgb(0x7D, 0x9D, 0x06)),
            new SolidBrush(Color.FromArgb(0xED, 0xB4, 0x07)),
            new SolidBrush(Color.FromArgb(0xFF, 0x2B, 0x0A)),
        };
        private Pen[] penBindingsDark = new Pen[]
        {
            new Pen(Color.FromArgb(0x4C, 0x5B, 0x5C)),
            new Pen(Color.FromArgb(0x7D, 0x9D, 0x06)),
            new Pen(Color.FromArgb(0xED, 0xB4, 0x07)),
            new Pen(Color.FromArgb(0xFF, 0x2B, 0x0A)),
        };

        private class TrackIdWrapper
        {
            public MidiTrackWrapper trackOb;
            public int trackIdx;

            public override string ToString()
            {
                return $"{trackIdx}: {trackOb.name}";
            }
        }

        private class NoteProcessingBox
        {
            public MidiTrackWrapper.NoteProcessingInfo info;
            public Rectangle bounds;
        }
        private List<NoteProcessingBox> noteProcessingBounds = new List<NoteProcessingBox>();

        private string orgTitle = "Harp Hero: converter";
        private int versionNum = 0;

        public MainForm()
        {
            InitializeComponent();

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            versionNum = version.Major;
            RunUpdateCheck();
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            var result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                LoadMidiFile(openFileDialog1.FileName);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (musicTrack != null)
            {
                var result = saveFileDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    SaveMidiFile(saveFileDialog1.FileName);
                }
            }
        }

        private void buttonPlay_Click(object sender, EventArgs e)
        {
            if (timerUpdate.Enabled)
            {
                timerUpdate.Stop();
                musicPlayer?.Stop();

                buttonPlay.Text = "Play";
                checkPlayOrgTrack.Enabled = true;
                hScrollBar1.Enabled = true;
                labelTrackPos.Visible = false;
                buttonLoad.Enabled = true;
            }
            else if (musicTrack != null)
            {
                int numWarmupBars = 0;
                int numWarmupBeats = 2;
                currentTimeUs = -TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatTicksTimeSpan(numWarmupBars, numWarmupBeats), musicTrack.tempoMap).TotalMicroseconds;

                musicPlayer = new MidiTrackPlayer(musicTrack, checkPlayOrgTrack.Checked);
                musicPlayer.SetTimeScaling(midiTimeScaling);
                musicPlayer.OnFinished += () => timerUpdate.Stop();

                try
                {
                    // this can throw exception when device is in use
                    musicPlayer.WarmupDevice();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Exception on device warmup! " + ex.ToString());
                }

                timerUpdate.Start();
                stopwatch.Start();

                buttonPlay.Text = "Stop";
                checkPlayOrgTrack.Enabled = false;
                hScrollBar1.Enabled = false;
                labelTrackPos.Visible = true;
                buttonLoad.Enabled = false;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (musicTrack != null)
            {
                midiTimeScaling = musicTrack.GetScalingForBPM((int)numericTargetBPM.Value);
                if (musicPlayer != null)
                {
                    musicPlayer.SetTimeScaling(midiTimeScaling);
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex >= 0 && comboBox1.SelectedIndex < loadedTracks.Count)
            {
                LoadMidiTrack(loadedTracks[comboBox1.SelectedIndex]);
            }
        }

        private void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            if (musicTrack != null)
            {
                float scrollPct = hScrollBar1.Value / (1.0f * hScrollBar1.Maximum);
                long midiTime = musicTrack.GetDurationUs();

                currentTimeUs = (long)(midiTime * scrollPct);
                Invalidate();
            }
        }

        private void timerUpdate_Tick(object sender, EventArgs e)
        {
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            Invalidate();

            if (musicPlayer?.IsPlaying ?? false)
            {
                currentTimeUs = musicPlayer.GetCurrentTimeUs();
            }
            else
            {
                currentTimeUs += (long)(elapsedMs * midiTimeScaling * 1000);
                if (currentTimeUs >= 0)
                {
                    musicPlayer.Start();// At(currentTimeUs);
                    stopwatch.Stop();
                }
            }

            labelTrackPos.Text = $"{(currentTimeUs * 0.000001f):0.##}s";
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            OnFileChanged();
            OnTrackLoaded();
        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            string newHint = "";
            foreach (var info in noteProcessingBounds)
            {
                if (info.bounds.Contains(e.Location))
                {
                    if (newHint.Length > 0) { newHint += ", "; }
                    newHint += info.info.ToString();
                }
            }

            if (string.IsNullOrEmpty(newHint))
            {
                if (musicTrack == null)
                {
                    newHint = "Load track to do stuff";
                }
                else if (!isValid5Octave)
                {
                    newHint = "Track requires more than 5 octaves, can't play in game";
                }
                else if (!isValid3Octave)
                {
                    newHint = "Track requires 5 octaves, annoying and not available for gamepads";
                }
                else
                {
                    newHint = "Mouse over notes for details";
                }
            }

            labelTransformHint.Text = newHint;
        }

        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            //e.Graphics.DrawRectangle(Pens.DarkGray, new Rectangle(panelNotes.Left - 1, panelNotes.Top - 1, panelNotes.Width + 2, panelNotes.Height + 2));
            //e.Graphics.DrawRectangle(Pens.DarkGray, new Rectangle(panelBindings.Left - 1, panelBindings.Top - 1, panelBindings.Width + 2, panelBindings.Height + 2));

            if (musicViewer != null)
            {
                musicViewer.SetTimeUs(currentTimeUs);
                musicViewerOrg.SetTimeUs(currentTimeUs);

                DrawNotes(e.Graphics);
                DrawBindings(e.Graphics);
            }
        }

        private void DrawNotes(Graphics graphics)
        {
            var musicViewers = new MidiTrackViewer[] { musicViewerOrg, musicViewer };

            var spaceX = panelNotes.Width;
            var spaceY = panelNotes.Height - 10;
            var noteNumberRange = Math.Max(1, musicTrack.statsOrg.maxNote - musicTrack.statsOrg.minNote);
            var startTimeUs = musicViewer.TimeRangeStartUs;
            var timeRangeUs = musicViewer.TimeRangeUs;

            foreach (var barTimeUs in musicViewer.shownBarLines)
            {
                float alphaX = 1.0f * (barTimeUs - startTimeUs) / timeRangeUs;
                if (alphaX >= 0.0f && alphaX <= 1.0f)
                {
                    int posX = panelNotes.Left + (int)(spaceX * alphaX);
                    graphics.DrawLine(Pens.DarkGray, posX, panelNotes.Top, posX, panelNotes.Bottom);
                }
            }
            foreach (var beatTimeUs in musicViewer.shownBeatLines)
            {
                float alphaX = 1.0f * (beatTimeUs - startTimeUs) / timeRangeUs;
                if (alphaX >= 0.0f && alphaX <= 1.0f)
                {
                    int posX = panelNotes.Left + (int)(spaceX * alphaX);
                    graphics.DrawLine(Pens.LightGray, posX, panelNotes.Top, posX, panelNotes.Bottom);
                }
            }

            noteProcessingBounds.Clear();
            for (int idx = 0; idx < musicViewers.Length; idx++)
            {
                var drawBrush = (idx == 0) ? Brushes.DarkGray : Brushes.Blue;
                var drawViewer = musicViewers[idx];
                foreach (var noteInfo in drawViewer.shownNotes)
                {
                    float alphaX0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.startUs - startTimeUs) / timeRangeUs));
                    float alphaX1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.endUs - startTimeUs) / timeRangeUs));
                    float alphaY = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteInfo.note.NoteNumber - musicTrack.statsOrg.minNote) / noteNumberRange));

                    int posX0 = panelNotes.Left + (int)(spaceX * alphaX0);
                    int posX1 = panelNotes.Left + (int)(spaceX * alphaX1);
                    int posY = panelNotes.Top + (int)(spaceY * (1.0f - alphaY));
                    var bounds = new Rectangle(posX0, posY, posX1 - posX0, 6);

                    var useBrush = drawBrush;
                    if (idx == 1)
                    {
                        int octaveDiff = Math.Abs(noteInfo.note.Octave - midOctaveId);
                        if (octaveDiff > 2)
                        {
                            useBrush = Brushes.Red;
                        }
                        else if (octaveDiff > 1)
                        {
                            useBrush = Brushes.Orange;
                        }
                    }

                    graphics.FillRectangle(useBrush, bounds);
                    graphics.DrawString(noteInfo.note.ToString(), drawFontSmall, useBrush, posX0, posY - drawFontSmall.Height);

                    if (idx == 0)
                    {
                        var matchIdx = musicTrack.noteProcessing.FindIndex(x => (x.timeUs == noteInfo.startUs) && (x.note.NoteNumber == noteInfo.note.NoteNumber));
                        if (matchIdx < 0)
                        {
                            long bestTimeDiff = 0;
                            for (int searchIdx = 0; searchIdx < musicTrack.noteProcessing.Count; searchIdx++)
                            {
                                var matchInfo = musicTrack.noteProcessing[searchIdx];
                                if (matchInfo.note.NoteNumber != noteInfo.note.NoteNumber)
                                {
                                    continue;
                                }

                                long timeDiffUs = Math.Abs(matchInfo.timeUs - noteInfo.startUs);
                                if (timeDiffUs > 500 * 5000)
                                {
                                    continue;
                                }

                                if (matchIdx < 0 || bestTimeDiff > timeDiffUs)
                                {
                                    bestTimeDiff = timeDiffUs;
                                    matchIdx = searchIdx;
                                }
                            }
                        }

                        if (matchIdx >= 0)
                        {
                            noteProcessingBounds.Add(new NoteProcessingBox() { bounds = bounds, info = musicTrack.noteProcessing[matchIdx] });
                        }
                    }
                }
            }

            {
                float alphaX = 1.0f * musicViewer.TimeRangeNowOffset / timeRangeUs;
                var posX = panelNotes.Left + (int)(spaceX * alphaX);

                graphics.DrawLine(Pens.Red, posX, panelNotes.Top, posX, panelNotes.Bottom);
            }
        }

        private void DrawBindings(Graphics graphics)
        {
            var spaceX = panelBindings.Width;
            var spaceY = panelBindings.Height - drawFontSmall.Height;
            var drawX = panelBindings.Left;
            var drawY = panelBindings.Top + drawFontSmall.Height;
            var startTimeUs = musicViewer.TimeRangeStartUs;
            var timeRangeUs = musicViewer.TimeRangeUs;

            // draw stuff
            int playingColorIdx = 0;

            foreach (var noteBinding in musicViewer.GetShownNotesBindings())
            {
                float alphaX0 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.startUs - startTimeUs) / timeRangeUs));
                float alphaX1 = Math.Min(1.0f, Math.Max(0.0f, 1.0f * (noteBinding.noteInfo.endUs - startTimeUs) / timeRangeUs));
                float alphaY = Math.Min(1.0f, Math.Max(0.0f, 1.0f * noteBinding.bindingIdx / musicViewer.maxBindingsToShow));

                int posX0 = drawX + (int)(spaceX * alphaX0);
                int posX1 = drawX + (int)(spaceX * alphaX1);
                int posY = drawY + (int)(spaceY * alphaY);

                int hintColorIdx =
                    (noteBinding.pressIdx >= musicViewer.maxBindingsToShow) ? 0 :
                    (noteBinding.bindingIdx >= 0 && noteBinding.bindingIdx < brushBindingsDark.Length - 1) ? noteBinding.bindingIdx + 1 :
                    0;

                if (noteBinding.pressIdx == 0)
                {
                    playingColorIdx = hintColorIdx;
                }

                graphics.FillRectangle(brushBindings[hintColorIdx], new Rectangle(posX0, posY, posX1 - posX0, 4));
                if (noteBinding.showHint)
                {
                    var noteDesc = noteBinding.noteInfo.note.ToString();
                    graphics.DrawString(noteDesc, drawFontSmall, brushBindingsDark[hintColorIdx], posX0, posY - drawFontSmall.Height);
                }
            }

            float nowAlphaX = 1.0f * musicViewer.TimeRangeNowOffset / timeRangeUs;
            var nowPosX = drawX + (int)(spaceX * nowAlphaX);
            graphics.DrawLine(penBindingsDark[playingColorIdx], nowPosX, panelBindings.Top, nowPosX, panelBindings.Bottom);
        }

        private void OnFileChanged()
        {
            if (loadedFilePath != null)
            {
                Text = $"{orgTitle} [v{versionNum}] - {System.IO.Path.GetFileName(loadedFilePath)}";
                textBoxError.Visible = false;
            }
            else
            {
                Text = $"{orgTitle} [v{versionNum}]";
            }
        }

        private void OnLoadingError(Exception ex)
        {
            textBoxError.Text = ex.Message + "\r\n\r\n" + ex.StackTrace;
            textBoxError.Visible = true;
            textBoxError.BringToFront();
        }

        private void OnTrackLoaded()
        {
            labelTrackPos.Text = "";

            if (musicTrack != null)
            {
                numericTargetBPM.Value = Math.Clamp(musicTrack.stats.beatsPerMinute, numericTargetBPM.Minimum, numericTargetBPM.Maximum);
                labelOrgBPM.Text = $"(Source: {musicTrack.stats.beatsPerMinute})";

                checkPlayOrgTrack.Enabled = true;
                buttonPlay.Enabled = true;
                buttonSave.Enabled = true;
                numericTargetBPM.Enabled = true;
            }
            else
            {
                labelOrgBPM.Text = $"(Source: --)";

                checkPlayOrgTrack.Enabled = false;
                buttonPlay.Enabled = false;
                buttonSave.Enabled = false;
                numericTargetBPM.Enabled = false;
            }
        }

        private void LoadMidiFile(string path)
        {
            loadedTracks.Clear();
            loadedFilePath = null;

            try
            {
                loadedFile = System.IO.File.Exists(path) ? MidiFile.Read(path) : null;
                if (loadedFile != null)
                {
                    MidiTrackWrapper.CollectNoteProcessing = true;

                    loadedTracks = MidiTrackWrapper.GenerateTracks(loadedFile);
                    loadedFilePath = path;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to import: " + ex);
                OnLoadingError(ex);
            }

            OnFileChanged();
            comboBox1.Items.Clear();
            for (int idx = 0; idx < loadedTracks.Count; idx++)
            {
                comboBox1.Items.Add(new TrackIdWrapper() { trackOb = loadedTracks[idx], trackIdx = idx });
            }

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
            comboBox1.Enabled = comboBox1.Items.Count > 1;
        }

        private void LoadMidiTrack(MidiTrackWrapper trackOb)
        {
            musicTrack = trackOb;
            musicViewer = new MidiTrackViewer(musicTrack) { timeWindowSecondsBehind = 1.0f, timeWindowSecondsAhead = 19.0f };

            musicViewerOrg = new MidiTrackViewer(musicTrack.midiTrackOrg, musicTrack.tempoMap) { timeWindowSecondsBehind = 1.0f, timeWindowSecondsAhead = 19.0f };
            musicViewerOrg.generateBarData = false;
            musicViewerOrg.generateBindingData = false;

            midiTimeScaling = 1.0f;

            isValid3Octave = musicTrack.IsOctaveRangeValid(out midOctaveId);
            isValid5Octave = isValid3Octave || musicTrack.IsOctaveRange5Valid(out midOctaveId);

            Invalidate();
            OnTrackLoaded();
        }

        public void SaveMidiFile(string path)
        {
            Logger.WriteLine("Exporting to: {0}", path);

            try
            {
                var midiFile = new MidiFile();
                midiFile.TimeDivision = loadedFile.TimeDivision;
                midiFile.Chunks.Add(musicTrack.midiTrack);
                midiFile.Write(path, true, MidiFileFormat.SingleTrack);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to export: " + ex);
                OnLoadingError(ex);
            }
        }

        private void RunUpdateCheck()
        {
            var updateTask = new Task(() =>
            {
                bool bFoundUpdate = GithubUpdater.FindAndDownloadUpdates(out string statusMsg);

                Invoke((MethodInvoker)delegate
                {
                    Logger.WriteLine("Version check: " + statusMsg);
                    labelUpdateNotify.Visible = bFoundUpdate;
                    labelUpdateNotify.BringToFront();
                });
            });

            updateTask.Start();
        }

        private void labelUpdateNotify_Click(object sender, EventArgs e)
        {
            labelUpdateNotify.Hide();
        }
    }
}
