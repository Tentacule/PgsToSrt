using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using PgsToSrt.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PgsToSrt
{
    internal static class MkvUtilities
    {
        private const string _pgsTrackCodecId = "S_HDMV/PGS";

        internal static bool IsMkvFile(string filename)
        {
            return filename.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetDefaultOutputFilename(List<TrackOutputOption> trackOutputOptions, string filename, MatroskaTrackInfo track)
        {
            string result = null;
            int? number = null;

            while (true)
            {
                var defaultOutputFilename = GetDefaultOutputFilename(filename, track, number);
                var existing = (
                    from t in trackOutputOptions
                    where string.Equals(t.Output, defaultOutputFilename, StringComparison.OrdinalIgnoreCase)
                    select t).Any();

                if (!existing)
                {
                    result = defaultOutputFilename;
                    break;
                }
                else
                {
                    if (!number.HasValue)
                        number = 1;

                    number += 1;
                }
            }

            return result;
        }

        internal static string GetDefaultOutputFilename(string filename, MatroskaTrackInfo track, int? number)
        {
            var defaultOutputFilename = Path.Combine(
                   Path.GetDirectoryName(filename),
                   Path.GetFileNameWithoutExtension(filename) + "." + track.Language + number + (track.IsForced ? ".forced" : "") + ".srt");

            return defaultOutputFilename;
        }

        internal static List<TrackOutputOption> GetTracksByLanguage(string filename, string trackLanguage)
        {
            var result = new List<TrackOutputOption>();

            using (var matroska = new MatroskaFile(filename))
            {
                if (matroska.IsValid)
                {
                    var pgsTracks = GetPgsSubtitleTracks(matroska);
                    var tracks = (from t in pgsTracks where string.Equals(trackLanguage, t.Language, StringComparison.OrdinalIgnoreCase) select t);

                    foreach (var track in tracks)
                    {
                        var defaultOutputFilename = GetDefaultOutputFilename(result, filename, track);
                        result.Add(new TrackOutputOption() { Track = track.TrackNumber, Output = defaultOutputFilename });
                    }
                }
            }

            return result;
        }

        public static List<MatroskaTrackInfo> GetPgsSubtitleTracks(MatroskaFile matroska)
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
    }
}
