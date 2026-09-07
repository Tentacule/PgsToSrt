FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

RUN apt-get -y update && \
  apt-get -y upgrade && \
  apt-get -y install \
    automake \
    ca-certificates \
    g++ \
    libtool \
    libtesseract5 \
    make \
    pkg-config \
    libc6-dev

COPY ./src /src

RUN cd /src && \
    dotnet restore  && \
    dotnet publish -c Release -f net8.0 -o /src/PgsToSrt/out

FROM mcr.microsoft.com/dotnet/runtime:8.0
ARG TESSDATA_DIR=tessdata
WORKDIR /app
ENV LANGUAGE=
ENV INPUT=/input.sup
ENV OUTPUT=/output.srt

RUN apt-get update && \
    apt-get install -y \
        curl \
        libtesseract5 \
    && apt-get clean && \
    rm -rf /var/lib/apt/lists/*

VOLUME /tessdata

COPY --from=builder /src/PgsToSrt/out .
COPY ${TESSDATA_DIR} /tmp/tessdata
RUN mkdir -p /tessdata && \
    find /tmp/tessdata -maxdepth 1 -name '*.traineddata' -exec cp -t /tessdata {} + ; \
    rm -rf /tmp/tessdata
COPY ./src/entrypoint.sh /entrypoint.sh

# Docker for Windows: EOL must be LF.
ENTRYPOINT ["/entrypoint.sh"]
