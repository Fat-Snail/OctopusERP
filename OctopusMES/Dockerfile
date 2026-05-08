FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OctopusMES.Api/OctopusMES.Api.csproj src/OctopusMES.Api/
RUN dotnet restore src/OctopusMES.Api/OctopusMES.Api.csproj
COPY . .
RUN dotnet publish src/OctopusMES.Api/OctopusMES.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OctopusMES.Api.dll"]
