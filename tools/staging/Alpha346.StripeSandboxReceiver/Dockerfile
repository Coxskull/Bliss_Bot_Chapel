# Isolated ALPHA-346 Stripe TEST-MODE webhook receiver only.
# Build context MUST be the repository root because Bliss.Infrastructure is referenced.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore tools/staging/Alpha346.StripeSandboxReceiver/Alpha346.StripeSandboxReceiver.csproj
RUN dotnet publish tools/staging/Alpha346.StripeSandboxReceiver/Alpha346.StripeSandboxReceiver.csproj \
    --configuration Release --no-restore --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Alpha346.StripeSandboxReceiver.dll"]
