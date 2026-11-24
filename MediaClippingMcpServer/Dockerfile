# Multi-stage Dockerfile for MediaClippingMcpServer
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["MediaClippingMcpServer.csproj", "./"]
RUN dotnet restore "MediaClippingMcpServer.csproj"

# Copy everything and build
COPY . .
RUN dotnet publish "MediaClippingMcpServer.csproj" -c Release -o /app/publish \
    --self-contained true \
    --runtime linux-x64 \
    /p:PublishSingleFile=true \
    /p:PublishReadyToRun=true \
    /p:InvariantGlobalization=true

# Runtime stage - minimal Debian with FFmpeg
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

# Install FFmpeg and yt-dlp
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    python3 \
    python3-pip \
    ca-certificates \
    && pip3 install --no-cache-dir yt-dlp \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Entry point
ENTRYPOINT ["./MediaClippingMcpServer"]