# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution và csproj
COPY MiniSocialNetwork.sln ./
COPY SocialNetwork.API/SocialNetwork.API.csproj ./SocialNetwork.API/

# Restore packages
RUN dotnet restore ./SocialNetwork.API/SocialNetwork.API.csproj

# Copy toàn bộ source code
COPY SocialNetwork.API ./SocialNetwork.API
COPY SocialNetwork.Application ./SocialNetwork.Application
COPY SocialNetwork.Domain ./SocialNetwork.Domain
COPY SocialNetwork.Infrastructure ./SocialNetwork.Infrastructure

# Publish project
RUN dotnet publish ./SocialNetwork.API/SocialNetwork.API.csproj -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
EXPOSE 10000
ENTRYPOINT ["dotnet", "SocialNetwork.API.dll"]
