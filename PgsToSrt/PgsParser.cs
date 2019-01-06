using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PgsToSrt
{
    internal class PgsParser
    {
        private const string _pgsTrackCodecId = "S_HDMV/PGS";

        private readonly ILogger _logger;

        public PgsParser(ILogger logger)
        {
            _logger = logger;
        }

        public List<BluRaySupParser.PcsData> Load(string filename, int track)
        {
            List<BluRaySupParser.PcsData> result = null;

            if (Path.GetExtension(filename.ToLowerInvariant()) == ".sup")
                result = LoadSup(filename);
            else if (Path.GetExtension(filename.ToLowerInvariant()) == ".mkv")
                result = LoadMkv(filename, track);

            return result;
        }

        private List<BluRaySupParser.PcsData> LoadSup(string filename)
        {
            return LoadSubtitles(filename);
        }

        private List<BluRaySupParser.PcsData> LoadMkv(string filename, int trackNumber)
        {
            List<BluRaySupParser.PcsData> result = null;

            using (var matroska = new MatroskaFile(filename))
            {
                if (matroska.IsValid)
                {
                    var pgsTracks = GetPgsSubtitleTracks(matroska);
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

        public List<BluRaySupParser.PcsData> LoadSubtitles(string supFileName)
        {
            var log = new StringBuilder();
            return BluRaySupParser.ParseBluRaySup(supFileName, log);
        }

        public List<BluRaySupParser.PcsData> LoadSubtitles(string mkvFileName, int trackNumber)
        {
            List<BluRaySupParser.PcsData> result = null;

            using (var matroska = new MatroskaFile(mkvFileName))
            {
                if (matroska.IsValid)
                {
                    var tracks = matroska.GetTracks(true);
                    var track = (from t in tracks where t.TrackNumber == trackNumber && string.Equals(_pgsTrackCodecId, t.CodecId) select t).FirstOrDefault();
                    if (track != null)
                        result = LoadBluRaySubFromMatroska(track, matroska);
                }
            }

            return result;
        }

        public List<BluRaySupParser.PcsData> LoadSubtitles(MatroskaFile matroska, MatroskaTrackInfo track)
        {
            List<BluRaySupParser.PcsData> result = null;

            if (matroska.IsValid)
            {
                result = LoadBluRaySubFromMatroska(track, matroska);
            }

            return result;
        }

        public List<MatroskaTrackInfo> GetPgsSubtitleTracks(MatroskaFile matroska)
        {
            var result = new List<MatroskaTrackInfo>();

            if (matroska.IsValid)
            {
                var tracks = matroska.GetTracks(true);
                var pgsTrack = (
                    from t in tracks
                    orderby t.TrackNumber
                    where string.Equals(_pgsTrackCodecId, t.CodecId)
                    select t);

                result.AddRange(pgsTrack);
            }

            return result;
        }

        private static List<BluRaySupParser.PcsData> LoadBluRaySubFromMatroska(MatroskaTrackInfo matroskaSubtitleInfo, MatroskaFile matroska)
        {
            var sub = matroska.GetSubtitle(matroskaSubtitleInfo.TrackNumber, null);
            var subtitles = new List<BluRaySupParser.PcsData>();
            var log = new StringBuilder();
            var clusterStream = new MemoryStream();
            var lastPalettes = new Dictionary<int, List<PaletteInfo>>();
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
                        var list = BluRaySupParser.ParseBluRaySup(clusterStream, log, true, lastPalettes);
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
                var length = BluRaySupParser.BigEndianInt16(buffer, position + 1) + 3;
                position += length;
            }
            return false;
        }
    }
}