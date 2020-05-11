using System;

namespace Tesseract.Interop
{
    /// <summary>
    /// Description of Constants.
    /// </summary>
    internal static class Constants
    {
        public const string LeptonicaWindowsDllName = "liblept1753";
        public const string TesseractWindowsDllName = "libtesseract3052";

        public const string LeptonicaLibraryName = "lept";
        public const string TesseractLibraryName = "tesseract";
        
        // tesseract uses an int to represent true false values.
        public const int TRUE = 1;
        public const int FALSE = 0;
    }
}