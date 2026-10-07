# TaskManager server: API, history, and live stream. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/PerformanceStatistics/ PerformanceStatistics/
COPY src/PerformanceStatistics.Monitoring/ PerformanceStatistics.Monitoring/
COPY src/TaskManager.Server/ TaskManager.Server/
COPY LICENSE.md README.md /
COPY Assets/logo.png /Assets/logo.png
RUN dotnet publish TaskManager.Server/TaskManager.Server.csproj -c Release -o /app -p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    TASKMGR__STORAGE=/data/taskmgr.db
VOLUME /data
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "TaskManager.Server.dll"]
