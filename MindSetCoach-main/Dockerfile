# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY MindSetCoach.Api/MindSetCoach.Api.csproj MindSetCoach.Api/
COPY MindSetCoach.Web/MindSetCoach.Web.csproj MindSetCoach.Web/
COPY MindSetCoach.Tests/MindSetCoach.Tests.csproj MindSetCoach.Tests/
RUN dotnet restore MindSetCoach.Api/MindSetCoach.Api.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/MindSetCoach.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Fly.io uses PORT environment variable
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MindSetCoach.Api.dll"]
