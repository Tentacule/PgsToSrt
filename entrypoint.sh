#!/bin/bash

args=()

if [[ ! -z "${INPUT}" ]]; then
  args+=" --input ${INPUT}"
fi
if [[ ! -z "${OUTPUT}" ]]; then
  args+=" --output ${OUTPUT}"
fi
if [[ ! -z "${TRACK}" ]]; then
  args+=" --track ${TRACK}"
fi
if [[ ! -z "${TRACK_LANGUAGE}" ]]; then
  args+=" --trackLanguage ${TRACK_LANGUAGE}"
fi
if [[ ! -z "${LANGUAGE}" ]]; then
  args+=" --tesseractlanguage ${LANGUAGE}"
fi
if [[ ! -z "${TESSDATA}" ]]; then
  args+=" --tesseractdata ${TESSDATA}"
else
  args+=" --tesseractdata /tessdata"
fi

ARGS=${args// / }

echo "dotnet /app/PgsToSrt.dll $ARGS"
dotnet /app/PgsToSrt.dll $ARGS
