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

RUN apt-get update && apt-get install -y \
    mariadb-client \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "UniVerein.Api.dll"]