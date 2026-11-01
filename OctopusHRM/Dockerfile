FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OctopusHRM.Api/OctopusHRM.Api.csproj src/OctopusHRM.Api/
RUN dotnet restore src/OctopusHRM.Api/OctopusHRM.Api.csproj
COPY . .
RUN dotnet publish src/OctopusHRM.Api/OctopusHRM.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OctopusHRM.Api.dll"]
