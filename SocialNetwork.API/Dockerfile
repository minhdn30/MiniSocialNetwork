# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution và project csproj
COPY ../MiniSocialNetwork.sln ./
COPY ./SocialNetwork.API/*.csproj ./SocialNetwork.API/

# Restore nuget
RUN dotnet restore

# Copy toàn bộ source code
COPY . ./

# Publish project
RUN dotnet publish ./SocialNetwork.API/SocialNetwork.API.csproj -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy output từ stage build
COPY --from=build /out ./

# Expose port để Render sử dụng
EXPOSE 10000

# Chạy ứng dụng
ENTRYPOINT ["dotnet", "SocialNetwork.API.dll"]
