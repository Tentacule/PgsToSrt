using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Nikse.SubtitleEdit.Core.BluRaySup.BluRaySupParser;

namespace PgsToSrt
{
    internal class PgsParser
    {
        private readonly ILogger _logger;

        public PgsParser(ILogger logger)
        {
            _logger = logger;
        }

        public List<PcsData> Load(string filename, int track, string output)
        {
            List<PcsData> pcsDataList = null;

            if (Path.GetExtension(filename.ToLowerInvariant()) == ".sup")
                pcsDataList = LoadSup(filename, output);
            else if (MkvUtilities.IsMkvFile(filename))
                pcsDataList = LoadMkv(filename, track, output);

            return pcsDataList;
        }

        private static List<PcsData> LoadSup(string filename, string output)
        {
            return LoadSubtitles(filename);
        }

        private List<PcsData> LoadMkv(string filename, int trackNumber, string output)
        {
            List<PcsData> result = null;

            using (var matroska = new MatroskaFile(filename))
            {
                if (matroska.IsValid)
                {
                    var pgsTracks = MkvUtilities.GetPgsSubtitleTracks(matroska);
                    var track = (from t in pgsTracks where t.TrackNumber == trackNumber select t).FirstOrDefault();

                    if (track != null)
                    {
                        result = LoadSubtitles(matroska, track);
                    }
                    else
                    {
                        _logger.LogInformation($"Track {trackNumber} is not a PGS track.");
                        ShowPgsTracks(filename, pgsTracks);
                    }
                }
                else
                {
                    _logger.LogInformation($"Invalid matroska file '{filename}'");
                }
            }

            return result;
        }

        private void ShowPgsTracks(string filename, IReadOnlyCollection<MatroskaTrackInfo> pgsTracks)
        {
            _logger.LogInformation($"-> {pgsTracks.Count} PGS tracks found in '{filename}'");
            foreach (var track in pgsTracks)
            {
                _logger.LogInformation(
                    $"- {track.TrackNumber.ToString().PadRight(2)} {track.Language.PadLeft(3)} {track.Name}");
            }
        }

        public static List<PcsData> LoadSubtitles(string supFileName)
        {
            var log = new StringBuilder();
            return ParseBluRaySup(supFileName, log);
        }

        public static List<PcsData> LoadSubtitles(MatroskaFile matroska, MatroskaTrackInfo track)
        {
            List<PcsData> result = null;

            if (matroska.IsValid)
            {
                result = LoadBluRaySubFromMatroska(track, matroska);
            }

            return result;
        }

        private static List<PcsData> LoadBluRaySubFromMatroska(MatroskaTrackInfo matroskaSubtitleInfo, MatroskaFile matroska)
        {
            var sub = matroska.GetSubtitle(matroskaSubtitleInfo.TrackNumber, null);
            var subtitles = new List<BluRaySupParser.PcsData>();
            var log = new StringBuilder();
            var clusterStream = new MemoryStream();
            var lastPalettes = new Dictionary<int, List<PaletteInfo>>();
            var bitmapObjects = new Dictionary<int, List<OdsData>>();

            foreach (var p in sub)
            {
                var buffer = p.GetData(matroskaSubtitleInfo);
                if (buffer != null && buffer.Length > 2)
                {
                    clusterStream.Write(buffer, 0, buffer.Length);
                    if (ContainsBluRayStartSegment(buffer))
                    {
                        if (subtitles.Count > 0 && subtitles[subtitles.Count - 1].StartTime == subtitles[subtitles.Count - 1].EndTime)
                        {
                            subtitles[subtitles.Count - 1].EndTime = (long)((p.Start - 1) * 90.0);
                        }
                        clusterStream.Position = 0;
                        var list = BluRaySupParser.ParseBluRaySup(clusterStream, log, true, lastPalettes, bitmapObjects);
                        foreach (var sup in list)
                        {
                            sup.StartTime = (long)((p.Start - 1) * 90.0);
                            sup.EndTime = (long)((p.End - 1) * 90.0);
                            subtitles.Add(sup);

                            // fix overlapping
                            if (subtitles.Count > 1 && sub[subtitles.Count - 2].End > sub[subtitles.Count - 1].Start)
                                subtitles[subtitles.Count - 2].EndTime = subtitles[subtitles.Count - 1].StartTime - 1;
                        }
                        clusterStream = new MemoryStream();
                    }
                }
                else if (subtitles.Count > 0)
                {
                    var lastSub = subtitles[subtitles.Count - 1];
                    if (lastSub.StartTime == lastSub.EndTime)
                    {
                        lastSub.EndTime = (long)((p.Start - 1) * 90.0);
                        if (lastSub.EndTime - lastSub.StartTime > 1000000)
                            lastSub.EndTime = lastSub.StartTime;
                    }
                }
            }

            return subtitles;
        }

        private static bool ContainsBluRayStartSegment(byte[] buffer)
        {
            const int epochStart = 0x80;
            var position = 0;
            while (position + 3 <= buffer.Length)
            {
                var segmentType = buffer[position];
                if (segmentType == epochStart)
                    return true;
                var length = BigEndianInt16(buffer, position + 1) + 3;
                position += length;
            }
            return false;
        }
    }
}