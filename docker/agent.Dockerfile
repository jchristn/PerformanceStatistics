# TaskManager agent for monitoring a Linux Docker host from a container.
# Run with pid: host, network_mode: host, and the host's /proc and /sys mounted under /host (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/PerformanceStatistics/ PerformanceStatistics/
COPY src/PerformanceStatistics.Monitoring/ PerformanceStatistics.Monitoring/
COPY src/TaskManager.Agent/ TaskManager.Agent/
COPY LICENSE.md README.md /
COPY Assets/logo.png /Assets/logo.png
RUN dotnet publish TaskManager.Agent/TaskManager.Agent.csproj -c Release -f net10.0 -o /app -p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENV PERFSTATS_HOST_ROOT=/host
ENTRYPOINT ["./taskmgr-agent"]
