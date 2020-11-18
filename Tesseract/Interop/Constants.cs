using System;
using Tncl.NativeLoader;

namespace Tesseract.Interop
{
    /// <summary>
    /// Description of Constants.
    /// </summary>
    internal static class Constants
    {
        public const string LeptonicaDllName = "leptonica-1.80.0";
        public const string TesseractDllName = "tesseract41";

        // tesseract uses an int to represent true false values.
        public const int TRUE = 1;
        public const int FALSE = 0;


        public const string LeptonicaWindowsDllName = "leptonica-1.80.0";
        public const string TesseractWindowsDllName = "tesseract41";

        public const string LeptonicaLibraryName = "lept";
        public const string TesseractLibraryName = "tesseract";

        public static NativeLoader Loader { get; } = new NativeLoader();
    }
}