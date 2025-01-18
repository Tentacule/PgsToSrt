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

        public List<BluRaySupParserImageSharp.PcsData> Load(string filename, int track, string output)
        {
            List<BluRaySupParserImageSharp.PcsData> pcsDataList = null;

            if (Path.GetExtension(filename.ToLowerInvariant()) == ".sup")
                pcsDataList = LoadSup(filename, output);
            else if (MkvUtilities.IsMkvFile(filename))
                pcsDataList = LoadMkv(filename, track, output);

            return pcsDataList;
        }

        private static List<BluRaySupParserImageSharp.PcsData> LoadSup(string filename, string output)
        {
            return LoadSubtitles(filename);
        }

        private List<BluRaySupParserImageSharp.PcsData> LoadMkv(string filename, int trackNumber, string output)
        {
            List<BluRaySupParserImageSharp.PcsData> result = null;

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

        private static List<BluRaySupParserImageSharp.PcsData> LoadSubtitles(string supFileName)
        {
            var log = new StringBuilder();
            return BluRaySupParserImageSharp.ParseBluRaySup(supFileName, log);
        }

        private static List<BluRaySupParserImageSharp.PcsData> LoadSubtitles(MatroskaFile matroska, MatroskaTrackInfo track)
        {
            List<BluRaySupParserImageSharp.PcsData> result = null;

            if (matroska.IsValid)
            {
                result = BluRaySupParserImageSharp.ParseBluRaySupFromMatroska(track, matroska);
            }

            return result;
        }
    }
}
