# syntax=docker/dockerfile:1
# Build context: thư mục src/backend/   →   docker build -t eduviet-api ./src/backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json OnlineLearning.sln ./
COPY src/OnlineLearning.Domain/OnlineLearning.Domain.csproj src/OnlineLearning.Domain/
COPY src/OnlineLearning.Application/OnlineLearning.Application.csproj src/OnlineLearning.Application/
COPY src/OnlineLearning.Infrastructure/OnlineLearning.Infrastructure.csproj src/OnlineLearning.Infrastructure/
COPY src/OnlineLearning.API/OnlineLearning.API.csproj src/OnlineLearning.API/
RUN dotnet restore src/OnlineLearning.API/OnlineLearning.API.csproj
COPY src/ src/
RUN dotnet publish src/OnlineLearning.API/OnlineLearning.API.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Font có dấu tiếng Việt cho chứng chỉ PDF (QuestPDF dùng font hệ thống)
RUN apt-get update \
 && apt-get install -y --no-install-recommends fonts-dejavu-core fonts-noto-core fontconfig curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data/keys /data/storage /app/wwwroot/uploads && chown -R app:app /data /app/wwwroot/uploads

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__KeysPath=/data/keys \
    Storage__LocalRoot=/data/storage \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "OnlineLearning.API.dll"]
