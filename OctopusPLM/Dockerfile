FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OctopusPLM.Core/OctopusPLM.Core.csproj src/OctopusPLM.Core/
COPY src/OctopusPLM.Infrastructure/OctopusPLM.Infrastructure.csproj src/OctopusPLM.Infrastructure/
COPY src/OctopusPLM.Api/OctopusPLM.Api.csproj src/OctopusPLM.Api/
RUN dotnet restore src/OctopusPLM.Api/OctopusPLM.Api.csproj
COPY . .
RUN dotnet publish src/OctopusPLM.Api/OctopusPLM.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OctopusPLM.Api.dll"]
