# syntax=docker/dockerfile:1
#
# One image for the three backend processes, so they always run the same build:
#   API         (default)  dotnet CreatorPlatform.Api.dll                working dir /app/api
#   migrations  one-shot   dotnet CreatorPlatform.Api.dll --migrate      working dir /app/api
#   Worker                 dotnet CreatorPlatform.Worker.dll             working dir /app/worker
# The working directory is each host's content root (where its appsettings.json is read from). See deploy/README.md.
#
# Build: docker build -t luma-backend:<tag> .   (multi-arch: docker buildx build --platform linux/amd64,linux/arm64 ...)

ARG DOTNET_VERSION=10.0

# The SDK runs on the build machine's own architecture and cross-publishes for the target (no emulation needed).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG TARGETARCH
WORKDIR /src

COPY . .

RUN --mount=type=cache,id=luma-nuget,target=/root/.nuget/packages \
    dotnet publish src/Api/CreatorPlatform.Api/CreatorPlatform.Api.csproj \
        --configuration Release --arch "$TARGETARCH" --output /out/api -p:UseAppHost=false \
 && dotnet publish src/Workers/CreatorPlatform.Worker/CreatorPlatform.Worker.csproj \
        --configuration Release --arch "$TARGETARCH" --output /out/worker -p:UseAppHost=false

# Chiseled: no shell, no package manager, runs as the non-root user "app" (UID 1654). "extra" adds ICU and tzdata,
# which the app needs (creator time zones, culture-aware formatting).
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS final
ARG GIT_SHA=unknown
LABEL org.opencontainers.image.title="luma-backend" \
      org.opencontainers.image.description="Luma API, Worker and database migrations" \
      org.opencontainers.image.revision="${GIT_SHA}"

COPY --from=build /out/api /app/api
COPY --from=build /out/worker /app/worker

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

WORKDIR /app/api
ENTRYPOINT ["dotnet", "CreatorPlatform.Api.dll"]
