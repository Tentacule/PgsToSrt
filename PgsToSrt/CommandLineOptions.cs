using CommandLine;

namespace PgsToSrt
{
    internal class CommandLineOptions
    {
        [Option(Required = true, HelpText = "Input filename, it can be a .mkv or a .sup extracted with mkvextract.")]
        public string Input { get; set; }

        [Option(HelpText = "Track number of the PGS subtitle to use, only needed when input is an .mkv file.")]
        public int? Track { get; set; }

        [Option(Required = true, HelpText = "Output .srt filename.")]
        public string Output { get; set; }

        [Option(HelpText = "Tesseract language to use if multiple languages are available in the tesseract data directory.")]
        public string TesseractLanguage { get; set; }

        [Option(HelpText = "Path of tesseract language data files, by default 'tessdata' in the executable direcotry.")]
        public string TesseractData { get; set; }
    }
}
