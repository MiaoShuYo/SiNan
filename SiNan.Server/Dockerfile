# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY ["SiNan.ServiceDefaults/SiNan.ServiceDefaults.csproj", "SiNan.ServiceDefaults/"]
COPY ["SiNan.Server/SiNan.Server.csproj", "SiNan.Server/"]

# Restore dependencies
RUN dotnet restore "SiNan.Server/SiNan.Server.csproj"

# Copy source code
COPY SiNan.ServiceDefaults/ SiNan.ServiceDefaults/
COPY SiNan.Server/ SiNan.Server/

# Build and publish
WORKDIR /src/SiNan.Server
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
	&& apt-get install -y curl \
	&& rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SiNan.Server.dll"]
