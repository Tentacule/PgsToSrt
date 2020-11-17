using Nikse.SubtitleEdit.Core.BluRaySup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using static Nikse.SubtitleEdit.Core.BluRaySup.BluRaySupParser;

namespace PgsToSrt.BluRaySup
{
    public static class BluRaySupParserExtensions
    {
        public static Image<Rgba32> GetRgba32(this BluRaySupParser.PcsData pcsData)
        {
            if (pcsData.PcsObjects.Count == 1)
                return SupDecoder.DecodeImage(pcsData.PcsObjects[0], pcsData.BitmapObjects[0], pcsData.PaletteInfos);

            var r = System.Drawing.Rectangle.Empty;
            for (var ioIndex = 0; ioIndex < pcsData.PcsObjects.Count; ioIndex++)
            {
                var ioRect = new System.Drawing.Rectangle(pcsData.PcsObjects[ioIndex].Origin, pcsData.BitmapObjects[ioIndex][0].Size);
                r = r.IsEmpty ? ioRect : System.Drawing.Rectangle.Union(r, ioRect);
            }

            var mergedBmp = new Image<Rgba32>(r.Width, r.Height);
            for (var ioIndex = 0; ioIndex < pcsData.PcsObjects.Count; ioIndex++)
            {
                var offset = pcsData.PcsObjects[ioIndex].Origin - new System.Drawing.Size(r.Location);
                using (var singleBmp = SupDecoder.DecodeImage(pcsData.PcsObjects[ioIndex], pcsData.BitmapObjects[ioIndex], pcsData.PaletteInfos))
                {
                    mergedBmp.Mutate(b => b.DrawImage(singleBmp, new SixLabors.ImageSharp.Point(offset.X, offset.Y), 0));
                }
            }
            return mergedBmp;
        }

    }

    static class SupDecoder
    {
        private const int AlphaCrop = 14;

        public static BluRaySupPalette DecodePalette(IList<PaletteInfo> paletteInfos)
        {
            var palette = new BluRaySupPalette(256);
            // by definition, index 0xff is always completely transparent
            // also all entries must be fully transparent after initialization

            bool fadeOut = false;
            for (int j = 0; j < paletteInfos.Count; j++)
            {
                var p = paletteInfos[j];
                int index = 0;

                for (int i = 0; i < p.PaletteSize; i++)
                {
                    // each palette entry consists of 5 bytes
                    int palIndex = p.PaletteBuffer[index];
                    int y = p.PaletteBuffer[++index];
                    int cr = p.PaletteBuffer[++index];
                    int cb = p.PaletteBuffer[++index];
                    int alpha = p.PaletteBuffer[++index];

                    int alphaOld = palette.GetAlpha(palIndex);
                    // avoid fading out
                    if (alpha >= alphaOld)
                    {
                        if (alpha < AlphaCrop)
                        {// to not mess with scaling algorithms, make transparent color black
                            y = 16;
                            cr = 128;
                            cb = 128;
                        }
                        palette.SetAlpha(palIndex, alpha);
                    }
                    else
                    {
                        fadeOut = true;
                    }

                    palette.SetYCbCr(palIndex, y, cb, cr);
                    index++;
                }
            }
            if (fadeOut)
            {
                System.Diagnostics.Debug.Print("fade out detected -> patched palette\n");
            }
            return palette;
        }

        /// <summary>
        /// Decode caption from the input stream
        /// </summary>
        /// <returns>bitmap of the decoded caption</returns>
        public static Image<Rgba32> DecodeImage(PcsObject pcs, IList<OdsData> data, List<PaletteInfo> palettes)
        {
            if (pcs == null || data == null || data.Count == 0)
                return new Image<Rgba32>(1, 1);

            int w = data[0].Size.Width;
            int h = data[0].Size.Height;

            var pal = DecodePalette(palettes);
            var bm = new Image<Rgba32>(w, h);
            bm.TryGetSinglePixelSpan(out var pixelSpan);

            int ofs = 0;
            int xpos = 0;
            var index = 0;

            byte[] buf = data[0].Fragment.ImageBuffer;
            do
            {
                int b = buf[index++] & 0xff;
                if (b == 0 && index < buf.Length)
                {
                    b = buf[index++] & 0xff;
                    if (b == 0)
                    {
                        // next line
                        ofs = (ofs / w) * w;
                        if (xpos < w)
                            ofs += w;
                        xpos = 0;
                    }
                    else
                    {
                        int size;
                        if ((b & 0xC0) == 0x40)
                        {
                            if (index < buf.Length)
                            {
                                // 00 4x xx -> xxx zeroes
                                size = ((b - 0x40) << 8) + (buf[index++] & 0xff);
                                var c = GetColorFromInt(pal.GetArgb(0));
                                for (int i = 0; i < size; i++)
                                    PutPixel(pixelSpan, ofs++, c);
                                xpos += size;
                            }
                        }
                        else if ((b & 0xC0) == 0x80)
                        {
                            if (index < buf.Length)
                            {
                                // 00 8x yy -> x times value y
                                size = (b - 0x80);
                                b = buf[index++] & 0xff;
                                var c = GetColorFromInt(pal.GetArgb(b));
                                for (int i = 0; i < size; i++)
                                    PutPixel(pixelSpan, ofs++, c);
                                xpos += size;
                            }
                        }
                        else if ((b & 0xC0) != 0)
                        {
                            if (index < buf.Length)
                            {
                                // 00 cx yy zz -> xyy times value z
                                size = ((b - 0xC0) << 8) + (buf[index++] & 0xff);
                                b = buf[index++] & 0xff;
                                var c = GetColorFromInt(pal.GetArgb(b));
                                for (int i = 0; i < size; i++)
                                    PutPixel(pixelSpan, ofs++, c);
                                xpos += size;
                            }
                        }
                        else
                        {
                            // 00 xx -> xx times 0
                            var c = GetColorFromInt(pal.GetArgb(0));
                            for (int i = 0; i < b; i++)
                                PutPixel(pixelSpan, ofs++, c);
                            xpos += b;
                        }
                    }
                }
                else
                {
                    PutPixel(pixelSpan, ofs++, b, pal);
                    xpos++;
                }
            } while (index < buf.Length);

            return bm;
        }

        private static void PutPixel(Span<Rgba32> bmp, int index, int color, BluRaySupPalette palette)
        {
            var colorArgb = GetColorFromInt(palette.GetArgb(color));
            PutPixel(bmp, index, colorArgb);
        }

        private static void PutPixel(Span<Rgba32> bmp, int index, Rgba32 color)
        {
            if (color.A > 0)
            {
                bmp[index] = color;
            }
        }

        private static Rgba32 GetColorFromInt(int number)
        {
            var values = BitConverter.GetBytes(number);
            if (!BitConverter.IsLittleEndian) Array.Reverse(values);

            var b = values[0];
            var g = values[1];
            var r = values[2];
            var a = values[3];

            return new Rgba32(r, g, b, a);
        }
    }
}
