#!/bin/sh
dotnet /app/PgsToSrt.dll --tesseractdata /tessdata --tesseractlanguage $LANGUAGE --input $INPUT --output $OUTPUT
