FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY DeviceService.API/DeviceService.API.csproj DeviceService.API/
COPY DeviceService.Core/DeviceService.Core.csproj DeviceService.Core/
COPY DeviceService.Data/DeviceService.Data.csproj DeviceService.Data/
COPY DeviceService.Services/DeviceService.Services.csproj DeviceService.Services/
RUN dotnet restore DeviceService.API/DeviceService.API.csproj

COPY DeviceService.API/ DeviceService.API/
COPY DeviceService.Core/ DeviceService.Core/
COPY DeviceService.Data/ DeviceService.Data/
COPY DeviceService.Services/ DeviceService.Services/
RUN dotnet publish DeviceService.API/DeviceService.API.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "DeviceService.API.dll"]
