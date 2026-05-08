FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OctopusWMS.Api/OctopusWMS.Api.csproj src/OctopusWMS.Api/
RUN dotnet restore src/OctopusWMS.Api/OctopusWMS.Api.csproj
COPY . .
RUN dotnet publish src/OctopusWMS.Api/OctopusWMS.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OctopusWMS.Api.dll"]
