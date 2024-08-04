FROM mcr.microsoft.com/dotnet/sdk:6.0 AS builder

ARG LANGUAGE=eng

RUN apt-get -y update && \
  apt-get -y upgrade && \
  apt-get -y install \
    automake \
    ca-certificates \
    g++ \
    libtool \
    libtesseract4 \
    make \
    pkg-config \
    wget \
    libc6-dev

ADD https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata /tessdata/

COPY ./tessdata/ /tessdata/
COPY ./src /src

RUN cd /src && \
    dotnet restore  && \
    dotnet publish -c Release -f net6.0 -o /src/PgsToSrt/out

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
ENV LANGUAGE=eng
ENV INPUT=/input.sup
ENV OUTPUT=/output.srt
VOLUME /tessdata

COPY --from=builder /src/PgsToSrt/out .
COPY --from=builder /tessdata /tessdata
COPY ./src/entrypoint.sh /entrypoint.sh

# Docker for Windows: EOL must be LF.
ENTRYPOINT ["/entrypoint.sh"]
