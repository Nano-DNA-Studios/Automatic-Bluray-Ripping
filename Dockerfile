# Use the Ubuntu Docker Image
FROM ubuntu:24.04

# Define the production account configuration
ENV USERNAME=ABR
ENV DEBIAN_FRONTEND=noninteractive

# Combine all compilation tools, runtime tools, and dependencies
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
    ffmpeg \
    eject \
    handbrake-cli \
    && mkdir -p /tmp/makemkv \
    && cd /tmp/makemkv \
    && curl -fsSL "https://www.makemkv.com/download/makemkv-oss-1.18.4.tar.gz" -o oss.tar.gz \
    && curl -fsSL "https://www.makemkv.com/download/makemkv-bin-1.18.4.tar.gz" -o bin.tar.gz \
    && tar -xzf oss.tar.gz \
    && tar -xzf bin.tar.gz \
    && cd makemkv-oss-1.18.4 \
    && ./configure --disable-gui \
    && make -j$(nproc) \
    && make install \
    && cd ../makemkv-bin-1.18.4 \
    && mkdir -p tmp \
    && touch tmp/eula_accepted \
    && make -j$(nproc) \
    && make install \
    && rm -rf /tmp/makemkv \
    && useradd -ms /bin/bash ${USERNAME} \
    && rm -rf /var/lib/apt/lists/*

# Register the newly built libraries
RUN ldconfig

# Set the operational workspace
WORKDIR /ABR

# Copy your published .NET execution app items
COPY ./Automatic-Bluray-Ripping/bin/Release/net8.0/linux-x64/publish/ .

# Secure file permissions
RUN chown -R ${USERNAME}:${USERNAME} /ABR

# Add the Key Registration
RUN mkdir -p ~/.MakeMKV && echo 'app_Key = "T-BSaJ6gwgMx4eIggWkVYXiVP_6zehm7WAO9dEydvzOHFHoZ6YQ82BL5cGpYDxvyRWnS"' > ~/.MakeMKV/settings.conf

# Drop privileges to non-root account
USER ABR

# Launch the app
CMD ["./Automatic-Bluray-Ripping"]