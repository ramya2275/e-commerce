# Stage 1: Build application using .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
#Adding a new comment for testing git
# Copy project files for layer caching
COPY Directory.Build.props ./
COPY src/Gateways/ApiGateway/ApiGateway.csproj ./src/Gateways/ApiGateway/

# Restore dependencies
RUN dotnet restore src/Gateways/ApiGateway/ApiGateway.csproj

# Copy full source
COPY src/Gateways/ApiGateway/ ./src/Gateways/ApiGateway/

# Build and publish release binaries
RUN dotnet publish src/Gateways/ApiGateway/ApiGateway.csproj -c Release -o /app/publish --no-restore

# Stage 2: Minimal ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
