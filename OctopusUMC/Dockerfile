FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OctopusUMC.Core/OctopusUMC.Core.csproj src/OctopusUMC.Core/
COPY src/OctopusUMC.Infrastructure/OctopusUMC.Infrastructure.csproj src/OctopusUMC.Infrastructure/
COPY src/OctopusUMC.Api/OctopusUMC.Api.csproj src/OctopusUMC.Api/
RUN dotnet restore src/OctopusUMC.Api/OctopusUMC.Api.csproj
COPY . .
RUN dotnet publish src/OctopusUMC.Api/OctopusUMC.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OctopusUMC.Api.dll"]
