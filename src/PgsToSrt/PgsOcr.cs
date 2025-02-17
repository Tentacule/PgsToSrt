using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using PgsToSrt.BluRaySup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TesseractOCR;
using TesseractOCR.Enums;

namespace PgsToSrt;

public class PgsOcr
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly Subtitle _subtitle = new ();
    private readonly string _tesseractVersion;
    private readonly string _libLeptName;
    private readonly string _libLeptVersion;
    private List<BluRaySupParserImageSharp.PcsData> _bluraySubtitles;

    public string TesseractDataPath { get; set; }
    public string TesseractLanguage { get; set; } = "eng";

    public PgsOcr(Microsoft.Extensions.Logging.ILogger logger, string tesseractVersion, string libLeptName, string libLeptVersion)
    {
        _logger = logger;
        _tesseractVersion = tesseractVersion;
        _libLeptName = libLeptName;
        _libLeptVersion = libLeptVersion;
    }

    public bool ToSrt(List<BluRaySupParserImageSharp.PcsData> subtitles, string outputFileName)
    {
        _bluraySubtitles = subtitles;

        if (!DoOcrParallel())
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
        using var file = new StreamWriter(outputFileName, false, new UTF8Encoding(false));
        file.Write(_subtitle.ToText(new SubRip()));
    }

    private bool DoOcrParallel()
    {
        _logger.LogInformation($"Starting OCR for {_bluraySubtitles.Count} items...");
        _logger.LogInformation($"Tesseract version {_tesseractVersion}");

        var exception = TesseractApi.Initialize(_tesseractVersion, _libLeptName, _libLeptVersion);
        if (exception != null)
        {
            _logger.LogError(exception, $"Failed: {exception.Message}");
            return false;
        }

        var ocrResults = new ConcurrentBag<Paragraph>();

        // Process subtitles in parallel
        Parallel.ForEach(Enumerable.Range(0, _bluraySubtitles.Count), i =>
        {
            try
            {
                using var engine = new Engine(TesseractDataPath, TesseractLanguage);

                var item = _bluraySubtitles[i];

                var paragraph = new Paragraph
                {
                    Number = i + 1,
                    StartTime = new TimeCode(item.StartTime / 90.0),
                    EndTime = new TimeCode(item.EndTime / 90.0),
                    Text = GetText(engine, i)
                };

                ocrResults.Add(paragraph);

                if (i % 50 == 0)
                {
                    _logger.LogInformation($"Processed item {paragraph.Number}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing item {i}: {ex.Message}");
            }
        });

        // Sort the results and add them to the subtitle
        _subtitle.Paragraphs.AddRange(ocrResults.OrderBy(p => p.Number));

        _logger.LogInformation("Finished OCR.");
        return true;
    }

    private string GetText(Engine engine, int index)
    {
        using var bitmap = GetSubtitleBitmap(index);
        using var image = GetPix(bitmap);
        using var page = engine.Process(image, PageSegMode.Auto);

        return page.Text?.Trim();
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
