# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /src

# Copy the solution file and project file
COPY monitoring-tool.sln .
COPY MonitoringTool/MonitoringTool.csproj MonitoringTool/

# Restore dependencies
RUN dotnet restore MonitoringTool/MonitoringTool.csproj

# Copy the rest of the source code
COPY MonitoringTool/ MonitoringTool/

# Build the application
WORKDIR /src/MonitoringTool
RUN dotnet build MonitoringTool.csproj -c Release -o /app/build

# Publish the application
RUN dotnet publish MonitoringTool.csproj -c Release -o /app/publish /p:UseAppHost=false

# Use the official .NET 8 runtime image for the final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# Set the working directory
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Create a directory for configuration files
RUN mkdir -p /app/config

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production

# Create a non-root user (only if it doesn't exist)
RUN id -u app &>/dev/null || useradd --create-home --shell /bin/bash app
RUN chown -R app:app /app
USER app

# Expose any ports if needed (not typically needed for background services)
# EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "MonitoringTool.dll"]