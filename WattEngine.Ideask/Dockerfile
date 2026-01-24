FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WattEngine.Ideask/WattEngine.Ideask.csproj", "WattEngine.Ideask/"]

RUN --mount=type=secret,id=nuget_username,env=NUGET_USERNAME \
    --mount=type=secret,id=nuget_password,env=NUGET_PASSWORD \
     dotnet nuget add source --name solsynth --username $NUGET_USERNAME --password $NUGET_PASSWORD https://git.solsynth.dev/api/packages/SolarNetwork/nuget/index.json

RUN dotnet restore "WattEngine.Ideask/WattEngine.Ideask.csproj"
COPY . .
WORKDIR "/src/WattEngine.Ideask"
RUN dotnet build "./WattEngine.Ideask.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WattEngine.Ideask.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WattEngine.Ideask.dll"]
