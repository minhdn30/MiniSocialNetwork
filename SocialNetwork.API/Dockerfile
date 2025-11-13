# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution và project csproj từ context root
COPY MiniSocialNetwork.sln ./
COPY SocialNetwork.API/SocialNetwork.API.csproj ./SocialNetwork.API/

# Restore packages
RUN dotnet restore ./SocialNetwork.API/SocialNetwork.API.csproj

# Copy toàn bộ source code project
COPY SocialNetwork.API ./SocialNetwork.API

# Publish project
RUN dotnet publish ./SocialNetwork.API/SocialNetwork.API.csproj -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /out ./
EXPOSE 10000
ENTRYPOINT ["dotnet", "SocialNetwork.API.dll"]
