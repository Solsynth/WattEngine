FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WattEngine.Valve/WattEngine.Valve.csproj", "WattEngine.Valve/"]
COPY ["DysonNetwork.Shared/DysonNetwork.Shared.csproj", "DysonNetwork.Shared/"]
COPY ["DysonNetwork.Shared/Directory.Packages.props", "DysonNetwork.Shared/"]
COPY ["Directory.Packages.props", "./"]

RUN dotnet restore "WattEngine.Valve/WattEngine.Valve.csproj"
COPY . .
WORKDIR "/src/WattEngine.Valve"
RUN dotnet build "./WattEngine.Valve.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WattEngine.Valve.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WattEngine.Valve.dll"]
