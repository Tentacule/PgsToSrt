using CommandLine;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace PgsToSrt
{
    internal class Runner
    {
        private readonly ILogger _logger;

        private string _input;
        private int? _track;

        private string _output;

        private string _tesseractData;
        private string _tesseractLanguage;

        public Runner(ILogger<Runner> logger)
        {
            _logger = logger;
        }

        public bool Run(Parsed<CommandLineOptions> values)
        {
            var result = false;

            if (values != null && CheckArguments(values))
            {
                result = ConvertPgs();
            }

            return result;
        }

        private bool CheckArguments(Parsed<CommandLineOptions> values)
        {
            var result = true;
            _input = values.Value.Input;
            _output = values.Value.Output;
            _track = values.Value.Track;
            _tesseractData = !string.IsNullOrEmpty(values.Value.TesseractData)
                ? values.Value.TesseractData
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

            if (!File.Exists(values.Value.Input))
            {
                _logger.LogError($"Input file '{_input}' doesn't exist.");
                result = false;
            }

            if (!_track.HasValue && IsMkvFile(_input))
            {
                _logger.LogError("Track must be set when input in an mkv file.");
                result = false;
            }
                       
            if (!string.IsNullOrEmpty(_output))
            {
                var outputDirectory = Path.GetDirectoryName(_output);
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

            return result;
        }

        private bool ConvertPgs()
        {
            var pgsParser = new PgsParser(_logger);
            var (subtitles, defaultOutputFilename) = pgsParser.Load(_input, _track.GetValueOrDefault());

            if (subtitles is null)
                return false;

            if (string.IsNullOrEmpty(_output))
                _output = defaultOutputFilename;

            var pgsOcr = new PgsOcr(_logger)
            {
                TesseractDataPath = _tesseractData,
                TesseractLanguage = _tesseractLanguage
            };

            pgsOcr.ToSrt(subtitles, _output);

            return true;
        }

        private static bool IsMkvFile(string filename)
        {
            return filename.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase);
        }
    }
}
