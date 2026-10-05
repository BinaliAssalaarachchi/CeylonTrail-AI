FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["CeylonTrail.Api.csproj", "./"]
RUN dotnet restore "CeylonTrail.Api.csproj"

# Copy the remaining project files and build
COPY . .
RUN dotnet publish "CeylonTrail.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port (Render automatically routes traffic to container port 8080 or the PORT env var)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CeylonTrail.Api.dll"]
