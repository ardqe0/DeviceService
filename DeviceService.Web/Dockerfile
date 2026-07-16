FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY DeviceService.Web/DeviceService.Web.csproj DeviceService.Web/
COPY DeviceService.Core/DeviceService.Core.csproj DeviceService.Core/
COPY DeviceService.Services/DeviceService.Services.csproj DeviceService.Services/
COPY DeviceService.Shared/DeviceService.Shared.csproj DeviceService.Shared/
RUN dotnet restore DeviceService.Web/DeviceService.Web.csproj

COPY DeviceService.Web/ DeviceService.Web/
COPY DeviceService.Core/ DeviceService.Core/
COPY DeviceService.Services/ DeviceService.Services/
COPY DeviceService.Shared/ DeviceService.Shared/
RUN dotnet publish DeviceService.Web/DeviceService.Web.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "DeviceService.Web.dll"]
