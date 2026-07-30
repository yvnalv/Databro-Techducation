# DataBro API — build context is ./backend.
#
# Two targets:
#   dev      — `dotnet watch`, source bind-mounted by compose. Rebuilds on every file change.
#   runtime  — trimmed publish output for staging/production (docs/DEPLOYMENT.md).

# ---------------------------------------------------------------------------
# dev: hot-reload target used by the `apps` compose profile.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev
WORKDIR /src

# The host source tree is bind-mounted over /src at run time. MSBuild reads these as
# properties, redirecting all bin/obj output to /artifacts (a named volume) so the
# container never reads or writes the Windows host's bin/obj — their absolute paths
# are not valid inside the container.
ENV UseArtifactsOutput=true \
    ArtifactsPath=/artifacts \
    NUGET_PACKAGES=/nuget \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1 \
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_HTTP_PORTS=8080

# Warm the NuGet cache and /artifacts at image-build time so container start is fast.
# Docker seeds the empty named volume from this layer's /artifacts on first run.
COPY . .
RUN dotnet restore DataBro.sln

EXPOSE 8080
CMD ["dotnet", "watch", "--project", "src/Api/DataBro.Api/DataBro.Api.csproj", "run", "--no-launch-profile"]

# ---------------------------------------------------------------------------
# build → runtime: the deployable image.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, against the project graph only, so the layer caches across source edits.
COPY global.json Directory.Build.props ./
COPY src/Modules/Directory.Build.props src/Modules/
COPY DataBro.sln ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet restore src/Api/DataBro.Api/DataBro.Api.csproj

RUN dotnet publish src/Api/DataBro.Api/DataBro.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Run unprivileged (docs/SECURITY.md).
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DataBro.Api.dll"]
