# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["src/CRFuelScraper.Core/CRFuelScraper.Core.csproj",                       "src/CRFuelScraper.Core/"]
COPY ["src/CRFuelScraper.Infrastructure/CRFuelScraper.Infrastructure.csproj",   "src/CRFuelScraper.Infrastructure/"]
COPY ["src/CRFuelScraper.API/CRFuelScraper.API.csproj",                         "src/CRFuelScraper.API/"]

RUN dotnet restore "src/CRFuelScraper.API/CRFuelScraper.API.csproj" \
    --runtime linux-musl-x64

COPY . .

RUN dotnet publish "src/CRFuelScraper.API/CRFuelScraper.API.csproj" \
    -c Release \
    -r linux-musl-x64 \
    --self-contained false \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache tzdata icu-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV TZ=America/Costa_Rica
ENV ASPNETCORE_ENVIRONMENT=Production

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CRFuelScraper.API.dll"]
