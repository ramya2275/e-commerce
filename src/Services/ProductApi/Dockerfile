# Stage 1: Build application using .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for layer caching
COPY Directory.Build.props ./
COPY src/Common/SharedContracts/SharedContracts.csproj ./src/Common/SharedContracts/
COPY src/Services/ProductApi/ProductApi.csproj ./src/Services/ProductApi/

# Restore dependencies
RUN dotnet restore src/Services/ProductApi/ProductApi.csproj

# Copy full source
COPY src/Common/SharedContracts/ ./src/Common/SharedContracts/
COPY src/Services/ProductApi/ ./src/Services/ProductApi/

# Build and publish release binaries
RUN dotnet publish src/Services/ProductApi/ProductApi.csproj -c Release -o /app/publish --no-restore

# Stage 2: Minimal ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProductApi.dll"]
