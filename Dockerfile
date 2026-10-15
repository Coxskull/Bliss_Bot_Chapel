# syntax=docker/dockerfile:1
FROM node:24-bookworm-slim AS financial-ui
WORKDIR /src/bliss-frontend
COPY bliss-frontend/package.json bliss-frontend/package-lock.json ./
# Optional public CA bundle for proxy-managed builds; never copied into image layers.
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then export NODE_EXTRA_CA_CERTS=/run/secrets/proxy_ca; fi; \
    npm ci --ignore-scripts --strict-ssl=true
COPY bliss-frontend/ ./
RUN npm test && npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
COPY --from=financial-ui /src/frontend/operations/financial-review/ ./frontend/operations/financial-review/
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then export SSL_CERT_FILE=/run/secrets/proxy_ca; fi; \
    dotnet publish Bliss.Api/Bliss.Api.csproj -c Release -o /app/publish /p:UseAppHost=false \
    && test -s /app/publish/frontend/operations/financial-review/index.html

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-cache policy perl-base \
    && apt-get install -y --no-install-recommends ffmpeg fonts-dejavu-core perl-base \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' perl-base)" ge '5.36.0-7+deb12u4' \
    && dpkg-query -W -f='perl-base installed: ${Version}\n' perl-base \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Bliss.Api.dll"]
