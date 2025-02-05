# PgsToSrt

Convert [PGS](https://en.wikipedia.org/wiki/Presentation_Graphic_Stream) subtitles to [SRT](https://en.wikipedia.org/wiki/SubRip) using [OCR](https://en.wikipedia.org/wiki/Optical_character_recognition).

## Prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Tesseract 4 language data files](https://github.com/tesseract-ocr/tessdata/)

Data files must be placed in the `tessdata` folder inside PgsToSrt folder, or the path can be specified in the command line with the --tesseractdata parameter.

You only need data files for the language(s) you want to convert.

## Usage

dotnet PgsToSrt.dll [parameters]

| Parameter             | Description                                                                                                                                      |
| --------------------- |--------------------------------------------------------------------------------------------------------------------------------------------------|
| `--input`             | Input filename, can be an mkv file or pgs subtitle extracted to a .sup file with mkvextract.                                                     |
| `--output`            | Output SubRip (`.srt`) filename. Auto generated from input filename if not set.                                                                  |
| `--track`             | Track number of the subtitle to process in an `.mkv` file (only required when input is a matroska file) <br/>This can be obtained with `mkvinfo` |
| `--tracklanguage`     | Convert all tracks of the specified language (only works with `.mkv` input)                                                               |
| `--tesseractlanguage` | Tesseract language to use if multiple languages are available in the tesseract data directory.                                                   |
| `--tesseractdata`     | Path of tesseract language data files, by default `tessdata` in the executable directory.                                                        |
| `--tesseractversion`  | libtesseract version, support 4 and 5 (default: 4) (ignored on Windows platform)                                                                 |
| `--libleptname`       | leptonica library name, usually lept or leptonica, 'lib' prefix is automatically added (default: lept) (ignored on Windows platform)             |
| `--libleptversion`    | leptonica library version (default: 5) (ignored on Windows platform)                                                                             |

## Example (Command Line)

``` sh
dotnet PgsToSrt.dll --input video1.fr.sup --output video1.fr.srt --tesseractlanguage fra
dotnet PgsToSrt.dll --input video1.mkv --output video1.srt --track 4
```

## Example (Docker)

Examime `entrypoint.sh` for a full list of all available arguments.

``` sh
docker run -it --rm \
    -v /data:/data \
    -e INPUT=/data/myImageSubtitle.sup \
    -e OUTPUT=/data/myTextSubtitle.srt \
    -e LANGUAGE=eng \
    tentacule/pgstosrt
```

Hint: The default arguments coming from `Dockerfile` are `INPUT=/input.sup` and `OUTPUT=/output.srt`, so you can easily:

``` sh
touch output-file.srt  # This needs to be a file, otherwise Docker will just assume it's a directory mount and it will fail.
docker run --it -rm \
    -v source-file.sup:/input.sup \
    -v output-file.srt:/output.srt \
    -e LANGUAGE=eng \
    tentacule/pgstosrt
```

## Dependencies

- Windows : none, tesseract/leptonica libraries are included in the release package.
- Linux   : libtesseract5 (`sudo apt install libtesseract5` or whatever your distro requires)

## Build

To build PgsToSrt.dll execute the following commands in the `src/` directory:

``` sh
dotnet restore
dotnet publish -c Release -o out --framework net6.0
# The file produced is  PgsToSrt/out/PgsToSrt.dll
```

To build a Docker image for all languages:

``` sh
make build-all
```

To build a docker image for a single language:

``` sh
make build-single LANGUAGE=eng  # or any other Tessaract-available language code
```

## Built With

- LibSE from [Subtitle Edit](https://www.nikse.dk/SubtitleEdit/)
- [Tesseract .net wrapper](https://github.com/charlesw/tesseract/)
- [CommandLineParser](https://github.com/commandlineparser/commandline)
- [SixLabors ImageSharp](https://github.com/SixLabors/ImageSharp)
