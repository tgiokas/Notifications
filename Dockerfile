# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Worker/Notifications.Worker.csproj", "src/Worker/"]
COPY ["src/Application/Notifications.Application.csproj", "src/Application/"]
COPY ["src/Domain/Notifications.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Notifications.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "./src/Worker/Notifications.Worker.csproj"
COPY . .
WORKDIR "src/Worker"
RUN dotnet build "./Notifications.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Notifications.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notifications.Worker.dll"]