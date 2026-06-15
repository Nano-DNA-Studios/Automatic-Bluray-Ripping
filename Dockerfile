# STAGE 1: Builder Stage (Install the Heavy Software)
FROM ubuntu:24.04 AS builder

ENV DEBIAN_FRONTEND=noninteractive

# Install compilation tools and build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    pkg-config \
    libc6-dev \
    libssl-dev \
    libexpat1-dev \
    libavcodec-dev \
    zlib1g-dev \
    ca-certificates \
    curl \
    && mkdir -p /tmp/makemkv \
    && cd /tmp/makemkv \
    && curl -fsSL "https://www.makemkv.com/download/makemkv-oss-1.18.3.tar.gz" -o oss.tar.gz \
    && curl -fsSL "https://www.makemkv.com/download/makemkv-bin-1.18.3.tar.gz" -o bin.tar.gz \
    && tar -xzf oss.tar.gz \
    && tar -xzf bin.tar.gz \
    && cd makemkv-oss-1.18.3 \
    && ./configure --disable-gui \
    && make -j$(nproc) \
    && make install \
    && cd ../makemkv-bin-1.18.3 \
    && mkdir -p tmp \
    && touch tmp/eula_accepted \
    && make -j$(nproc) \
    && make install

# STAGE 2: Build the container using results from previous build
FROM ubuntu:24.04

# Define the production account configuration
ENV USERNAME=ABR
ENV DEBIAN_FRONTEND=noninteractive

# Install runtime media players and base certificates
RUN apt-get update && apt-get install -y \
    ffmpeg \
    ca-certificates \
    libssl3 \
    libexpat1 \
    && useradd -ms /bin/bash ${USERNAME} \
    && rm -rf /var/lib/apt/lists/*

# Copy the compiled files directly from the builder stage
COPY --from=builder /usr/local/bin/ /usr/local/bin/
COPY --from=builder /usr/local/lib/ /usr/local/lib/
COPY --from=builder /usr/bin/makemkvcon /usr/bin/makemkvcon
COPY --from=builder /usr/share/MakeMKV/ /usr/share/MakeMKV/

# Register the newly transferred libraries inside Ubuntu
RUN ldconfig

# Set the operational workspace
WORKDIR /ABR

# Copy your published .NET execution app items
COPY ./Automatic-Bluray-Ripping/bin/Release/net8.0/linux-x64/publish/ .

# Secure file permissions
RUN chown -R ${USERNAME}:${USERNAME} /ABR

# Drop privileges to non-root account
USER ABR

# Launch the app
CMD ["./Automatic-Bluray-Ripping"]