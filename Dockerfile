# syntax=docker/dockerfile:1
# The CromoBound server (docs/server-deploy.md). Build it with the commit it comes from, which becomes part of the engine version
# saved in every match:
#   docker build --build-arg SOURCE_REVISION=$(git rev-parse HEAD) -t cromobound:latest .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SOURCE_REVISION
RUN test -n "$SOURCE_REVISION" || { echo "Build with --build-arg SOURCE_REVISION=\$(git rev-parse HEAD)." >&2; exit 1; }
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/CromoBound.Server/CromoBound.Server.csproj -c Release -o /app \
    -p:UseAppHost=false -p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false -p:SourceRevisionId=$SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY data/cards.json data/tokens.json data/printings.json data/sets.json data/
COPY data/effects/ data/effects/
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    CromoBound__DataFolder=/app/data \
    CromoBound__DatabasePath=/var/lib/cromobound/db/cromobound.db \
    CromoBound__KeysFolder=/var/lib/cromobound/keys
RUN mkdir -p /var/lib/cromobound/db /var/lib/cromobound/keys && chown -R $APP_UID /var/lib/cromobound
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CromoBound.Server.dll"]
