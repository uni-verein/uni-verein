FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UniVerein.DbMigrator/UniVerein.DbMigrator.csproj", "UniVerein.DbMigrator/"]
COPY ["UniVerein.DAL/UniVerein.DAL.csproj", "UniVerein.DAL/"]

RUN dotnet restore "UniVerein.DbMigrator/UniVerein.DbMigrator.csproj"

COPY . .

WORKDIR "/src/UniVerein.DbMigrator"
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "UniVerein.DbMigrator.dll"]
