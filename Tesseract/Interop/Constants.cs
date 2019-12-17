using System;

namespace Tesseract.Interop
{
    /// <summary>
    /// Description of Constants.
    /// </summary>
    internal static class Constants
    {
        public const string LeptonicaDllNameWindows = "liblept1753";
        public const string TesseractDllNameWindows = "libtesseract3052";

        public const string LeptonicaDllNameUnix = "liblept.so.5";
        public const string TesseractDllNameUnix = "libtesseract.so.3";
        
        // tesseract uses an int to represent true false values.
        public const int TRUE = 1;
        public const int FALSE = 0;
    }
}