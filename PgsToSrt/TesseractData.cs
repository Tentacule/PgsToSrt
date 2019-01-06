using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace PgsToSrt
{
    internal class TesseractData
    {
        private readonly ILogger _logger;

        public TesseractData(ILogger logger)
        {
            _logger = logger;
        }

        public string GetTesseractLanguage(string tesseractData, string wantedLanguage)
        {
            string result = null;
            var languages = GetAvailableLanguages(tesseractData);

            if (wantedLanguage != null && !languages.Contains(wantedLanguage.ToLowerInvariant()))
            {
                _logger.LogError($"Language '{wantedLanguage}' is not available in Tesseract data directory.");
                _logger.LogInformation("Available languages:");
                foreach (var language in languages)
                {
                    _logger.LogInformation($"- {language}");
                }
            }
            else if (wantedLanguage != null)
            {
                result = wantedLanguage;
            }
            else if (languages.Any())
            {
                result = GetDefaultTesseractLanguage(languages);
            }
            else
            {
                _logger.LogError("No tesseract language data files found.");
            }

            return result;
        }

        public List<string> GetAvailableLanguages(string dataPath)
        {
            var files = Directory.GetFiles(dataPath, "*.traineddata");
            var result = new List<string>();

            foreach (var trainedDataFile in files)
            {
                var language = Path.GetFileNameWithoutExtension(trainedDataFile).ToLowerInvariant();
                result.Add(language);
                _logger.LogInformation($"Detected tesseract language data for language '{language}'.");
            }

            return result;
        }

        public static string GetDefaultTesseractLanguage(List<string> languages)
        {
            return string.Join("+", languages); ;
        }
    }
}
