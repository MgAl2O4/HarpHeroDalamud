using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace HarpHero
{
    public class MusicTrackSerializer
    {
        public static string SaveToString(MusicTrack trackOb)
        {
            string outputStr = "";
            if (trackOb == null || trackOb.status != MusicTrack.Status.NoErrors)
            {
                return outputStr;
            }

            var dataStream = new MemoryStream();
            using (var writer = new BinaryWriter(dataStream))
            {
                writer.Write(trackOb.Name);
                writer.Write(trackOb.notes.Count);

                writer.Write(trackOb.beatsPerMinute);
                writer.Write(trackOb.numTicksPerQuarterNote);

                foreach (var noteOb in trackOb.notes)
                {
                    writer.Write(noteOb.time);
                    writer.Write(noteOb.duration);
                    writer.Write(noteOb.noteIdx);
                    writer.Write(noteOb.octaveIdx);
                }
            }

            var rawDataArr = dataStream.ToArray();
            dataStream.Dispose();

            var compressedData = new MemoryStream();
            using (var compressionStream = new GZipStream(compressedData, CompressionMode.Compress))
            {
                compressionStream.Write(rawDataArr, 0, rawDataArr.Length);
            }

            var compressedDataArr = compressedData.ToArray();
            compressedData.Dispose();

            outputStr = Convert.ToBase64String(compressedDataArr);
            return outputStr;
        }

        public static MusicTrack LoadFromString(string dataStr)
        {
            MusicTrack trackOb = null;
            try
            {
                byte[] compressedDataArr = Convert.FromBase64String(dataStr);
                var rawDataStream = new MemoryStream();

                using (var compressedDataStream = new MemoryStream(compressedDataArr))
                using (var compressionStream = new GZipStream(compressedDataStream, CompressionMode.Decompress))
                {
                    byte[] buffer = new byte[1024];
                    int numRead = 0;

                    while ((numRead = compressionStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        rawDataStream.Write(buffer, 0, numRead);
                    }
                }

                rawDataStream.Seek(0, SeekOrigin.Begin);
                using (var reader = new BinaryReader(rawDataStream))
                {
                    trackOb = new MusicTrack();
                    trackOb.Name = reader.ReadString();
                    int numNotes = reader.ReadInt32();

                    trackOb.beatsPerMinute = reader.ReadInt32();
                    trackOb.numTicksPerQuarterNote = reader.ReadInt32();

                    trackOb.notes = new List<MusicTrack.Note>();
                    for (int idx = 0; idx < numNotes; idx++)
                    {
                        MusicTrack.Note noteOb = new();
                        noteOb.time = reader.ReadInt32();
                        noteOb.duration = reader.ReadInt32();
                        noteOb.noteIdx = reader.ReadByte();
                        noteOb.octaveIdx = reader.ReadSByte();

                        trackOb.notes.Add(noteOb);
                    }
                }

                rawDataStream.Dispose();
                trackOb.status = MusicTrack.Status.NoErrors;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                trackOb = null;
            }

            return trackOb;
        }
    }
}
