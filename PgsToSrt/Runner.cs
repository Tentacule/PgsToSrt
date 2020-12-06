using CommandLine;
using Microsoft.Extensions.Logging;
using PgsToSrt.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace PgsToSrt
{
    internal class Runner
    {
        private readonly ILogger _logger;

        private string _tesseractData;
        private string _tesseractLanguage;

        public Runner(ILogger<Runner> logger)
        {
            _logger = logger;
        }

        public void Run(Parsed<CommandLineOptions> values)
        {
            if (values != null)
            {
                var (argumentChecked, runnerOptions) = GetTrackOptions(values);

                if (argumentChecked)
                {
                    foreach (var runnerOption in runnerOptions)
                    {
                        ConvertPgs(runnerOption.Input, runnerOption.Track, runnerOption.Output);
                    }
                }
            }
        }

        private (bool result, List<TrackOption> trackOptions) GetTrackOptions(Parsed<CommandLineOptions> values)
        {
            var result = true;
            var trackOptions = new List<TrackOption>();
            var input = values.Value.Input;
            var output = values.Value.Output;
            var trackLanguage = values.Value.TrackLanguage;
            var track = values.Value.Track;

            _tesseractData = !string.IsNullOrEmpty(values.Value.TesseractData)
                ? values.Value.TesseractData
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

            if (!File.Exists(values.Value.Input))
            {
                _logger.LogError($"Input file '{input}' doesn't exist.");
                result = false;
            }

            if (MkvUtilities.IsMkvFile(input))
            {
                if (string.IsNullOrEmpty(trackLanguage) && !track.HasValue)
                {
                    _logger.LogError("Track must be set when input is an mkv file.");
                    result = false;
                }
                else if (!string.IsNullOrEmpty(trackLanguage))
                {
                    var runnerOptionLanguages = MkvUtilities.GetTracksByLanguage(input, trackLanguage);
                    foreach (var item in runnerOptionLanguages)
                    {
                        trackOptions.Add(new TrackOption() { Input = input, Output = item.Output, Track = item.Track });
                    }
                }
                else
                {
                    trackOptions.Add(new TrackOption() { Input = input, Output = output, Track = track });
                }
            }
            else
            {
                trackOptions.Add(new TrackOption() { Input = input, Output = output, Track = null });
            }

            if (!string.IsNullOrEmpty(output))
            {
                var outputDirectory = Path.GetDirectoryName(output);
                if (!Directory.Exists(outputDirectory))
                {
                    _logger.LogError($"Output directory '{outputDirectory}' doesn't exist.");
                    result = false;
                }
            }

            if (Directory.Exists(_tesseractData))
            {
                var tesseractData = new TesseractData(_logger);
                _tesseractLanguage = tesseractData.GetTesseractLanguage(_tesseractData, values.Value.TesseractLanguage);

                if (string.IsNullOrEmpty(_tesseractLanguage))
                {
                    result = false;
                }
            }
            else
            {
                _logger.LogError($"Tesseract data directory '{_tesseractData}' doesn't exist.");
                result = false;
            }

            return (result, trackOptions: trackOptions);
        }

        private bool ConvertPgs(string input, int? track, string output)
        {
            var pgsParser = new PgsParser(_logger);
            var (subtitles, defaultOutputFilename) = pgsParser.Load(input, track.GetValueOrDefault());

            if (subtitles is null)
                return false;

            if (string.IsNullOrEmpty(output))
                output = defaultOutputFilename;

            var pgsOcr = new PgsOcr(_logger)
            {
                TesseractDataPath = _tesseractData,
                TesseractLanguage = _tesseractLanguage
            };

            pgsOcr.ToSrt(subtitles, output);

            return true;
        }    
    }
}
