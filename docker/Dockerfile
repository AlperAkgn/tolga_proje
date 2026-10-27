# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY NuGet.config ./
COPY backend/Afterform.Api/Afterform.Api.csproj backend/Afterform.Api/packages.lock.json ./backend/Afterform.Api/
RUN dotnet restore backend/Afterform.Api/Afterform.Api.csproj --locked-mode
COPY backend/Afterform.Api/ ./backend/Afterform.Api/
COPY outputs/ ./outputs/
RUN dotnet publish backend/Afterform.Api/Afterform.Api.csproj --no-restore \
    --configuration Release --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish/ ./
RUN mkdir -p /var/afterform/demos /var/afterform/keys \
    && chown -R app:app /var/afterform
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD ["dotnet", "Afterform.Api.dll", "--health-check"]
ENTRYPOINT ["dotnet", "Afterform.Api.dll"]
