# PgsToSrt

Convert pgs subtitles to srt using OCR.

### Prerequisites
- [.NET Core 2.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/2.1)
- [Tesseract 3.04/3.05 language data files](https://github.com/tesseract-ocr/tessdata/tree/3.04.00)

Data files must be placed in a _tessdata_ folder inside PgsToSrt folder, or the path can be specified in the command line with the --tesseractdata parameter.

You only need data files for the language(s) you want to convert.

### Usage

dotnet PgsToSrt.dll [parameters]

| Parameters         |                |
| :------------      | :------------- |
| --input            | Input filename, can be an mkv file or pgs subtitle extracted to a .sup file with mkvextract.|
| --output           | Output Subrip (.srt) filename.|
| --track            | Track number of the subtitle to process in an mkv file (only required when input is a matroska file)  |
| --tesseractlanguage| Tesseract language to use if multiple languages are available in the tesseract data directory.        |
| --tesseractdata    | Path of tesseract language data files, by default 'tessdata' in the executable direcotry.             |

Examples

dotnet PgsToSrt.dll --input video1.fr.sup --output video1.fr.srt --tesseractlanguage fra

dotnet PgsToSrt.dll --input video1.mkv --output video1.srt --track 4

### Built With
- LibSE from [Subtitle Edit](https://www.nikse.dk/SubtitleEdit/)
- [Tesseract .net wrapper](https://github.com/charlesw/tesseract/)
- [CommandLineParser](https://github.com/commandlineparser/commandline) 
