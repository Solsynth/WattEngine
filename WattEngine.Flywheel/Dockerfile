FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WattEngine.Flywheel/WattEngine.Flywheel.csproj", "WattEngine.Flywheel/"]
COPY ["DysonNetwork.Shared/DysonNetwork.Shared.csproj", "DysonNetwork.Shared/"]
COPY ["DysonNetwork.Shared/Directory.Packages.props", "DysonNetwork.Shared/"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "WattEngine.Flywheel/WattEngine.Flywheel.csproj"
COPY . .
WORKDIR "/src/WattEngine.Flywheel"
RUN dotnet publish "./WattEngine.Flywheel.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WattEngine.Flywheel.dll"]
