using System;
using System.Collections.Generic;
using System.Text;
using CommandLine;

namespace PgsToSrt
{
    internal class CommandLineOptions
    {
        [Option(Required = true)]
        public string Input { get; set; }

        [Option()]
        public int? Track { get; set; }

        [Option(Required = true)]
        public string Output { get; set; }

        [Option()]
        public string TesseractLanguage { get; set; }

        [Option()]
        public string TesseractData { get; set; }
    }
}
