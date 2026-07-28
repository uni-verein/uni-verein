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
# postgresql-client-17 (matching the postgres:17-alpine server) isn't in Ubuntu's own repos yet,
# so it's pulled from the official PostgreSQL APT repository instead.
RUN apt-get update -o Acquire::Retries=3 \
    && apt-get upgrade -y -o Acquire::Retries=3 \
    && apt-get install -y -o Acquire::Retries=3 --no-install-recommends \
    curl \
    gnupg \
    ca-certificates \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
    && . /etc/os-release \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $VERSION_CODENAME-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update -o Acquire::Retries=3 \
    && apt-get install -y -o Acquire::Retries=3 --no-install-recommends postgresql-client-17 \
    && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8095/health || exit 1

ENTRYPOINT ["dotnet", "UniVerein.Api.dll"]