# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only csproj to optimize cache
COPY ["ActoEngine.WebApi.csproj", "./"]
# Restore dependencies
RUN dotnet restore "./ActoEngine.WebApi.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/."
# Build the application
RUN dotnet build "ActoEngine.WebApi.csproj" -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish "ActoEngine.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# Expose port 5093 (matching docker-compose and .env)
EXPOSE 5093

# Environment variables
ENV ASPNETCORE_URLS=http://+:5093

# Create a non-root user
RUN useradd -m -u 1000 appuser

# Copy published files with correct ownership
COPY --chown=appuser:appuser --from=publish /app/publish .

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "ActoEngine.WebApi.dll"]
