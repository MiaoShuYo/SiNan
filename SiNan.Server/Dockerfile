# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ../SiNan.ServiceDefaults/SiNan.ServiceDefaults.csproj ./SiNan.ServiceDefaults/
COPY ./SiNan.Server.csproj ./SiNan.Server/

RUN dotnet restore ./SiNan.Server/SiNan.Server.csproj

COPY ../SiNan.ServiceDefaults ./SiNan.ServiceDefaults/
COPY . ./SiNan.Server/

WORKDIR /src/SiNan.Server
RUN dotnet publish -c Release -o /app/publish

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
