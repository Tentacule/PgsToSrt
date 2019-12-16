/*
 * Copyright 2009 Volker Oth (0xdeadbeef)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * NOTE: Converted to C# and modified by Nikse.dk@gmail.com
 * NOTE: For more info see http://blog.thescorpius.com/index.php/2017/07/15/presentation-graphic-stream-sup-files-bluray-subtitle-format/
 */

using System;
using System.Collections.Generic;
using System.Drawing;

namespace PgsToSrt.BluRaySup
{

    public class BluRaySupPicture
    {
        /// <summary>
        /// screen width
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// screen height
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// start time in milliseconds
        /// </summary>
        public long StartTime { get; set; }

        public int StartTimeForWrite => (int)(StartTime * 90.0);

        /// <summary>
        /// end time in milliseconds
        /// </summary>
        public long EndTime { get; set; }

        public int EndTimeForWrite => (int)(EndTime * 90.0);

        /// <summary>
        /// if true, this is a forced subtitle
        /// </summary>
        public bool IsForced { get; set; }

        /// <summary>
        /// composition number - increased at start and end PCS
        /// </summary>
        public int CompositionNumber { get; set; }

        /// <summary>
        /// objectID used in decoded object
        /// </summary>
        public int ObjectId { get; set; }

        /// <summary>
        /// list of ODS packets containing image info
        /// </summary>
        public List<ImageObject> ImageObjects;

        /// <summary>
        /// width of subtitle window (might be larger than image)
        /// </summary>
        public int WindowWidth { get; set; }

        /// <summary>
        /// height of subtitle window (might be larger than image)
        /// </summary>
        public int WindowHeight { get; set; }

        /// <summary>
        /// upper left corner of subtitle window x
        /// </summary>
        public int WindowXOffset { get; set; }

        /// <summary>
        /// upper left corner of subtitle window y
        /// </summary>
        public int WindowYOffset { get; set; }

        /// <summary>
        /// FPS type (e.g. 0x10 = 24p)
        /// </summary>
        public int FramesPerSecondType { get; set; }

        /// <summary>
        /// List of (list of) palette info - there are up to 8 palettes per epoch, each can be updated several times
        /// </summary>
        public List<List<PaletteInfo>> Palettes;
        
        private static byte FindBestMatch(Color color, Dictionary<Color, int> palette)
        {
            int smallestDiff = 1000;
            int smallestDiffIndex = -1;
            foreach (var kvp in palette)
            {
                int diff = Math.Abs(kvp.Key.A - color.A) + Math.Abs(kvp.Key.R - color.R) + Math.Abs(kvp.Key.G - color.G) + Math.Abs(kvp.Key.B - color.B);
                if (diff < smallestDiff)
                {
                    smallestDiff = diff;
                    smallestDiffIndex = kvp.Value;
                }
            }
            return (byte)smallestDiffIndex;
        }

        private static bool HasCloseColor(Color color, Dictionary<Color, int> palette, int maxDifference)
        {
            foreach (var kvp in palette)
            {
                int difference = Math.Abs(kvp.Key.A - color.A) + Math.Abs(kvp.Key.R - color.R) + Math.Abs(kvp.Key.G - color.G) + Math.Abs(kvp.Key.B - color.B);
                if (difference < maxDifference)
                    return true;
            }
            return false;
        }

        //private static Dictionary<Color, int> GetBitmapPalette(NikseBitmap bitmap)
        //{
        //    var pal = new Dictionary<Color, int>();
        //    //for (int y = 0; y < bitmap.Height; y++)
            //{
            //    for (int x = 0; x < bitmap.Width; x++)
            //    {
            //        var c = bitmap.GetPixel(x, y);
            //        if (c != Color.Transparent)
            //        {
            //            if (pal.Count < 100)
            //            {
            //                if (!HasCloseColor(c, pal, 1))
            //                    pal.Add(c, pal.Count);
            //            }
            //            else if (pal.Count < 240)
            //            {
            //                if (!HasCloseColor(c, pal, 5))
            //                    pal.Add(c, pal.Count);
            //            }
            //            else if (pal.Count < 254 && !HasCloseColor(c, pal, 25))
            //            {
            //                pal.Add(c, pal.Count);
            //            }
            //        }
            //    }
            //}
            //pal.Add(Color.Transparent, pal.Count); // last entry must be transparent
        //   return pal;
       // }

        /// <summary>
        /// Get ID for given frame rate
        /// </summary>
        /// <param name="fps">frame rate</param>
        /// <returns>byte ID for the given frame rate</returns>
        private static int GetFpsId(double fps)
        {
            if (Math.Abs(fps - Core.Fps24Hz) < 0.01) // 24
                return 0x20;
            if (Math.Abs(fps - Core.FpsPal) < 0.01) // 25
                return 0x30;
            if (Math.Abs(fps - Core.FpsNtsc) < 0.01) // 29.97
                return 0x40;
            if (Math.Abs(fps - Core.FpsPalI) < 0.01) // 50
                return 0x60;
            if (Math.Abs(fps - Core.FpsNtscI) < 0.1) // 59.94
                return 0x70;

            return 0x10; // 23.976
        }
    }
}
