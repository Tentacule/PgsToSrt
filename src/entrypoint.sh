#!/bin/bash

TESSDATA_DIR="${TESSDATA:-/tessdata}"

if [[ -n "${LANGUAGE}" ]]; then
  mkdir -p "${TESSDATA_DIR}"
  test -f "${TESSDATA_DIR}/${LANGUAGE}.traineddata" || \
    curl -fL "https://github.com/tesseract-ocr/tessdata/raw/main/${LANGUAGE}.traineddata" \
      -o "${TESSDATA_DIR}/${LANGUAGE}.traineddata"
fi

args=()

if [[ -n "${INPUT}" ]]; then
  args+=('--input' "${INPUT}")
fi
if [[ -n "${OUTPUT}" ]]; then
  args+=('--output' "${OUTPUT}")
fi
if [[ -n "${TRACK}" ]]; then
  args+=('--track' "${TRACK}")
fi
if [[ -n "${TRACK_LANGUAGE}" ]]; then
  args+=('--tracklanguage' "${TRACK_LANGUAGE}")
fi
if [[ -n "${LANGUAGE}" ]]; then
  args+=('--tesseractlanguage' "${LANGUAGE}")
fi
args+=('--tesseractdata' "${TESSDATA_DIR}")

args+=('--tesseractversion' '5')
  
echo "dotnet /app/PgsToSrt.dll ${args[*]}"
dotnet /app/PgsToSrt.dll "${args[@]}"
