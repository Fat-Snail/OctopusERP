FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OctopusOA.Api/OctopusOA.Api.csproj src/OctopusOA.Api/
RUN dotnet restore src/OctopusOA.Api/OctopusOA.Api.csproj
COPY . .
RUN dotnet publish src/OctopusOA.Api/OctopusOA.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OctopusOA.Api.dll"]
