# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY backend/HAQPOS.Domain/HAQPOS.Domain.csproj backend/HAQPOS.Domain/
COPY backend/HAQPOS.Application/HAQPOS.Application.csproj backend/HAQPOS.Application/
COPY backend/HAQPOS.Infrastructure/HAQPOS.Infrastructure.csproj backend/HAQPOS.Infrastructure/
COPY backend/HAQPOS.Api/HAQPOS.Api.csproj backend/HAQPOS.Api/
COPY backend/HAQPOS.Domain/packages.lock.json backend/HAQPOS.Domain/
COPY backend/HAQPOS.Application/packages.lock.json backend/HAQPOS.Application/
COPY backend/HAQPOS.Infrastructure/packages.lock.json backend/HAQPOS.Infrastructure/
COPY backend/HAQPOS.Api/packages.lock.json backend/HAQPOS.Api/
RUN dotnet restore backend/HAQPOS.Api/HAQPOS.Api.csproj --locked-mode
COPY backend/ backend/
RUN dotnet publish backend/HAQPOS.Api/HAQPOS.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 CMD ["dotnet", "HAQPOS.Api.dll", "--health-probe"]
ENTRYPOINT ["dotnet", "HAQPOS.Api.dll"]
