# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY . .

# Restore dependencies
RUN dotnet restore

# Publish the application
RUN dotnet publish -c Release -o /app/publish

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

# Render requires the app to listen on PORT
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CryptostellerAPI.dll"]
