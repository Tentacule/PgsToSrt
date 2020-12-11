FROM mcr.microsoft.com/dotnet/sdk:5.0 AS builder

RUN apt-get update && \
    apt-get install -y automake ca-certificates g++ git libtool libtesseract4 make pkg-config libc6-dev && \
    cd / && git clone https://github.com/tesseract-ocr/tessdata

COPY . /src
RUN cd /src && \
    dotnet restore  && \
    dotnet publish -c Release -o /src/PgsToSrt/out && \
    mv /src/entrypoint.sh /entrypoint.sh && chmod +x /entrypoint.sh && \
    mv /src/PgsToSrt/out  /app

ENV LANGUAGE=eng
ENV INPUT=/input.sup
ENV OUTPUT=/output.srt
VOLUME /tessdata
ENTRYPOINT /entrypoint.sh
