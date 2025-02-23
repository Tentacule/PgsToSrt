using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using PgsToSrt.BluRaySup;

namespace PgsToSrt
{
    internal class PgsParser
    {
        private readonly ILogger _logger;

        public PgsParser(ILogger logger)
        {
            _logger = logger;
        }

        public List<BluRaySupParserImageSharp.PcsData> Load(string filename, int track) =>
            Path.GetExtension(filename.ToLowerInvariant()) switch
            {
                ".sup" => LoadSubtitles(filename),
                _ when MkvUtilities.IsMkvFile(filename) => LoadMkv(filename, track),
                _ => throw new InvalidOperationException(
                        $"Unsupported file type: {Path.GetExtension(filename.ToLowerInvariant())}")
            };

        private List<BluRaySupParserImageSharp.PcsData> LoadMkv(string filename, int trackNumber)
        {
            using var matroska = new MatroskaFile(filename);
            if (!matroska.IsValid)
            {
                _logger.LogInformation($"Invalid Matroska file '{filename}'");
                return null;
            }

            var pgsTracks = MkvUtilities.GetPgsSubtitleTracks(matroska);
            var track = pgsTracks.FirstOrDefault(t => t.TrackNumber == trackNumber);

            if (track != null)
            {
                return LoadSubtitles(matroska, track);
            }

            _logger.LogInformation($"Track {trackNumber} is not a PGS track.");
            LogPgsTracks(filename, pgsTracks);
            return null;
        }

        private void LogPgsTracks(string filename, List<MatroskaTrackInfo> pgsTracks)
        {
            _logger.LogInformation($"-> {pgsTracks.Count} PGS tracks found in '{filename}'");
            foreach (var track in pgsTracks)
            {
                _logger.LogInformation($"- {track.TrackNumber,-2} {track.Language,3} {track.Name}");
            }
        }

        private static List<BluRaySupParserImageSharp.PcsData> LoadSubtitles(string supFileName)
        {
            var log = new StringBuilder();
            return BluRaySupParserImageSharp.ParseBluRaySup(supFileName, log);
        }

        private static List<BluRaySupParserImageSharp.PcsData> LoadSubtitles(MatroskaFile matroska, MatroskaTrackInfo track)
        {
            return matroska.IsValid
                ? BluRaySupParserImageSharp.ParseBluRaySupFromMatroska(track, matroska)
                : null;
        }
    }
}
