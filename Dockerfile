# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution và csproj
COPY CloudM.sln ./
COPY CloudM.API/CloudM.API.csproj ./CloudM.API/

# Restore packages
RUN dotnet restore ./CloudM.API/CloudM.API.csproj

# Copy toàn bộ source code
COPY CloudM.API ./CloudM.API
COPY CloudM.Application ./CloudM.Application
COPY CloudM.Domain ./CloudM.Domain
COPY CloudM.Infrastructure ./CloudM.Infrastructure

# Publish project
RUN dotnet publish ./CloudM.API/CloudM.API.csproj -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
EXPOSE 10000
ENTRYPOINT ["dotnet", "CloudM.API.dll"]
