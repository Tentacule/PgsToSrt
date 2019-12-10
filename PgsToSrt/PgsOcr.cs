using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Tesseract;

public class PgsOcr
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly Subtitle _subtitle = new Subtitle();

    private List<BluRaySupParser.PcsData> _bluraySubtitles;
    public string TesseractDataPath { get; set; }
    public string TesseractLanguage { get; set; } = "eng";

    public PgsOcr(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
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
            _logger.LogError(ex, "Saving '{outputFileName}' failed:");
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
        try
        {
            using (var engine = new TesseractEngine(TesseractDataPath, TesseractLanguage, EngineMode.TesseractOnly))
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
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error: " + e.Message + e.StackTrace);

            return false;
        }
    }

    private string GetText(TesseractEngine engine, int index)
    {
        string result;

        using (var bitmap = GetSubtitleBitmap(index))
        using (var image = GetPix(bitmap))
        using (var page = engine.Process(image))
        {
            result = page.GetText();
            result = result?.Trim();
        }

        return result;
    }

    private static Pix GetPix(Bitmap bitmap)
    {
        byte[] pngBytes;

        using (var stream = new MemoryStream())
        {
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
            pngBytes = stream.ToArray();
        }

        return Pix.LoadFromMemory(pngBytes);
    }

    private Bitmap GetSubtitleBitmap(int index)
    {
        return _bluraySubtitles[index].GetBitmap();
    }

}
