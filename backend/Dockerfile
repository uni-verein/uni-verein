FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UniVerein.Api/UniVerein.Api.csproj", "UniVerein.Api/"]
COPY ["UniVerein.DAL/UniVerein.DAL.csproj", "UniVerein.DAL/"]

RUN dotnet restore "UniVerein.Api/UniVerein.Api.csproj"

COPY . .

WORKDIR "/src/UniVerein.Api"
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update -o Acquire::Retries=3 \
    && apt-get upgrade -y -o Acquire::Retries=3 \
    && apt-get install -y -o Acquire::Retries=3 --no-install-recommends \
    curl \
    mariadb-client \
    && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8095/health || exit 1

ENTRYPOINT ["dotnet", "UniVerein.Api.dll"]