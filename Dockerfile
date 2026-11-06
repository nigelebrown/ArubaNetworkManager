# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
COPY . .
RUN dotnet restore Jcf.NetworkManager.slnx -p:NuGetAuditLevel=moderate -p:NuGetAuditMode=all -warnaserror:NU1902,NU1903,NU1904
RUN dotnet build Jcf.NetworkManager.slnx -c Release --no-restore
RUN dotnet test tests/Jcf.NetworkManager.Tests.csproj -c Release --no-build --no-restore
RUN dotnet publish src/Jcf.NetworkManager/Jcf.NetworkManager.csproj -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /app/data && chown -R "$APP_UID:$APP_UID" /app/data
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /publish/ ./
COPY --chown=$APP_UID:$APP_UID deploy/docker/app-entrypoint.sh /app/entrypoint.sh
RUN chmod 0555 /app/entrypoint.sh
USER $APP_UID
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 DOTNET_CLI_TELEMETRY_OPTOUT=1 Jcf__DockerMode=true
EXPOSE 8080
ENTRYPOINT ["/app/entrypoint.sh"]
