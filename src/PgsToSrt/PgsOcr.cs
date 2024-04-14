using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using PgsToSrt;
using PgsToSrt.BluRaySup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TesseractOCR;
using TesseractOCR.Enums;

public class PgsOcr
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly Subtitle _subtitle = new Subtitle();
    private readonly string _tesseractVersion;
    private readonly string _libLeptName;
    private readonly string _libLeptVersion;

    private List<BluRaySupParser.PcsData> _bluraySubtitles;

    public string TesseractDataPath { get; set; }
    public string TesseractLanguage { get; set; } = "eng";

    public PgsOcr(Microsoft.Extensions.Logging.ILogger logger, string tesseractVersion, string libLeptName, string libLeptVersion)
    {
        _logger = logger;
        _tesseractVersion = tesseractVersion;
        _libLeptName = libLeptName;
        _libLeptVersion = libLeptVersion;
    }

    public bool ToSrt(List<BluRaySupParser.PcsData> subtitles, string outputFileName)
    {
        _bluraySubtitles = subtitles;

        if (!DoOcr())
            return false;

        try
        {
            Save(outputFileName);
            _logger.LogInformation($"Saved '{outputFileName}' with {_subtitle.Paragraphs.Count} items.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Saving '{outputFileName}' failed:");
            return false;
        }
    }

    private void Save(string outputFileName)
    {
        using (var file = new StreamWriter(outputFileName, false, new UTF8Encoding(false)))
        {
            file.Write(_subtitle.ToText(new SubRip()));
        }
    }

    private bool DoOcr()
    {
        _logger.LogInformation($"Starting OCR for {_bluraySubtitles.Count} items...");
        _logger.LogInformation($"Tesseract verion {_tesseractVersion}");

        var exception = TesseractApi.Initialize(_tesseractVersion, _libLeptName, _libLeptVersion);
        if (exception != null)
        {
            _logger.LogError(exception, $"Failed: {exception.Message}");
            return false;
        }

        using (var engine = new Engine(TesseractDataPath, TesseractLanguage))
        {
            for (var i = 0; i < _bluraySubtitles.Count; i++)
            {
                var item = _bluraySubtitles[i];

                var paragraph = new Paragraph
                {
                    Number = i + 1,
                    StartTime = new TimeCode(item.StartTime / 90.0),
                    EndTime = new TimeCode(item.EndTime / 90.0),
                    Text = GetText(engine, i)
                };

                _subtitle.Paragraphs.Add(paragraph);

                if (i % 50 == 0)
                {
                    _logger.LogInformation($"Processed item {paragraph.Number}.");
                }
            }

            _logger.LogInformation("Finished OCR.");
        }

        return true;
    }

    private string GetText(Engine engine, int index)
    {
        string result;

        using (var bitmap = GetSubtitleBitmap(index))
        using (var image = GetPix(bitmap))
        using (var page = engine.Process(image, PageSegMode.Auto))
        {
            result = page.Text;
            result = result?.Trim();
        }

        return result;
    }

    private static TesseractOCR.Pix.Image GetPix(Image<Rgba32> bitmap)
    {
        byte[] bytes;

        using (var stream = new MemoryStream())
        {
            bitmap.SaveAsBmp(stream);
            bytes = stream.ToArray();
        }
       
        return TesseractOCR.Pix.Image.LoadFromMemory(bytes);
    }

    private Image<Rgba32> GetSubtitleBitmap(int index)
    {
        return _bluraySubtitles[index].GetRgba32();
    }

}
