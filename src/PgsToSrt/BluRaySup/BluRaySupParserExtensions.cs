using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.BluRaySup;

namespace PgsToSrt.BluRaySup
{
    public static class BluRaySupParserExtensions
    {
        public static Image<Rgba32> GetRgba32(this BluRaySupParserImageSharp.PcsData pcsData)
        {
            if (pcsData.PcsObjects.Count == 1)
                return SupDecoder.DecodeImage(pcsData.PcsObjects[0], pcsData.BitmapObjects[0], pcsData.PaletteInfos);

            var r = Rectangle.Empty;
            for (var ioIndex = 0; ioIndex < pcsData.PcsObjects.Count; ioIndex++)
            {
                var ioRect = new Rectangle(pcsData.PcsObjects[ioIndex].Origin, pcsData.BitmapObjects[ioIndex][0].Size);
                r = r.IsEmpty ? ioRect : Rectangle.Union(r, ioRect);
            }

            var mergedBmp = new Image<Rgba32>(r.Width, r.Height);
            for (var ioIndex = 0; ioIndex < pcsData.PcsObjects.Count; ioIndex++)
            {
                var offset = pcsData.PcsObjects[ioIndex].Origin - new Size(r.Location);
                using var singleBmp = SupDecoder.DecodeImage(pcsData.PcsObjects[ioIndex], pcsData.BitmapObjects[ioIndex], pcsData.PaletteInfos);

                mergedBmp.Mutate(b => b.DrawImage(singleBmp, new Point(offset.X, offset.Y), 0));
            }

            return mergedBmp;
        }
    }

    static class SupDecoder
    {
        /// <summary>
        /// Decode caption from the input stream
        /// </summary>
        /// <returns>bitmap of the decoded caption</returns>
        public static Image<Rgba32> DecodeImage(
            BluRaySupParserImageSharp.PcsObject pcs,
            IList<BluRaySupParserImageSharp.OdsData> data,
            List<BluRaySupParserImageSharp.PaletteInfo> palettes)
        {
            if (pcs == null || data == null || data.Count == 0)
                return new Image<Rgba32>(1, 1);
            var width = data[0].Size.Width;
            var height = data[0].Size.Height;
            if (width <= 0 || height <= 0 || data[0].Fragment.ImageBuffer.Length == 0)
                return new Image<Rgba32>(1, 1);

            using var bmp = new Image<Rgba32>(width, height);

            bmp.DangerousTryGetSinglePixelMemory(out var pixelMemory);
            var pixelSpan = pixelMemory.Span;

            var palette = BluRaySupParserImageSharp.DecodePalette(palettes);
            int num1 = 0;
            int num2 = 0;
            int num3 = 0;
            byte[] imageBuffer = data[0].Fragment.ImageBuffer;
            do
            {
                var color1 = imageBuffer[num3++] & byte.MaxValue;
                if (color1 == 0 && num3 < imageBuffer.Length)
                {
                    int num4 = imageBuffer[num3++] & byte.MaxValue;
                    if (num4 == 0)
                    {
                        num1 = num1 / width * width;
                        if (num2 < width)
                            num1 += width;
                        num2 = 0;
                    }
                    else if ((num4 & 192) == 64)
                    {
                        if (num3 < imageBuffer.Length)
                        {
                            int num5 = (num4 - 64 << 8) + ((int) imageBuffer[num3++] & (int) byte.MaxValue);
                            Color color2 = GetColorFromInt(palette.GetArgb(0));
                            for (int index = 0; index < num5; ++index)
                                PutPixel(pixelSpan, num1++, color2);
                            num2 += num5;
                        }
                    }
                    else if ((num4 & 192) == 128)
                    {
                        if (num3 < imageBuffer.Length)
                        {
                            int num6 = num4 - 128;
                            int index1 = imageBuffer[num3++] & byte.MaxValue;
                            Color color3 = GetColorFromInt(palette.GetArgb(index1));
                            for (int index2 = 0; index2 < num6; ++index2)
                                PutPixel(pixelSpan, num1++, color3);
                            num2 += num6;
                        }
                    }
                    else if ((num4 & 192) != 0)
                    {
                        if (num3 < imageBuffer.Length)
                        {
                            int num7 = num4 - 192 << 8;
                            byte[] numArray1 = imageBuffer;
                            int index3 = num3;
                            int num8 = index3 + 1;
                            int num9 = numArray1[index3] & byte.MaxValue;
                            int num10 = num7 + num9;
                            byte[] numArray2 = imageBuffer;
                            int index4 = num8;
                            num3 = index4 + 1;
                            int index5 = (int) numArray2[index4] & byte.MaxValue;
                            Color color4 = GetColorFromInt(palette.GetArgb(index5));
                            for (int index6 = 0; index6 < num10; ++index6)
                                PutPixel(pixelSpan, num1++, color4);
                            num2 += num10;
                        }
                    }
                    else
                    {
                        Color color5 = GetColorFromInt(palette.GetArgb(0));
                        for (int index = 0; index < num4; ++index)
                            PutPixel(pixelSpan, num1++, color5);
                        num2 += num4;
                    }
                }
                else
                {
                    PutPixel(pixelSpan, num1++, color1, palette);
                    ++num2;
                }
            } while (num3 < imageBuffer.Length);

            var bmp2 = new Image<Rgba32>(width + 50, height + 50);
            // ReSharper disable once AccessToDisposedClosure
            bmp2.Mutate(i => i.DrawImage(bmp, new Point(25, 25), 1f));

            return bmp2;
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
