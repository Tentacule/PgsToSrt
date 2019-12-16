FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS builder
RUN cd / && git clone --branch 3.04.00 https://github.com/tesseract-ocr/tessdata
RUN apt-get update && \
    apt-get install -y automake ca-certificates g++ git libtool libleptonica-dev=1.75.3-3 make pkg-config libgdiplus && \
    git clone https://github.com/tesseract-ocr/tesseract.git  --depth 1 --branch 3.05 --single-branch && \
    cd tesseract && \
    ./autogen.sh && \
    ./configure && \
    make && \
    make install && \
    ldconfig

COPY . /src
RUN cd /src && \
    dotnet restore  && \
    dotnet publish -c Release -o out && \
    mv /src/entrypoint.sh /entrypoint.sh && chmod +x /entrypoint.sh

RUN mv /src/PgsToSrt/out  /app

ENV LANGUAGE=eng
ENV INPUT=/input.sup
ENV OUTPUT=/output.srt
VOLUME /tessdata
ENTRYPOINT /entrypoint.sh
