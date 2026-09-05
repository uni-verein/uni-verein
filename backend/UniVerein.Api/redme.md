## Add migrations
1. Start a mysql database:
```
docker run --name postgres-db -e POSTGRES_PASSWORD=passwort -e POSTGRES_USER=uni_verein_user -e POSTGRES_DB=uni_verein -p 5432:5432 -d postgres:17-alpine
```
2. Create new migration
```
dotnet ef migrations add ExampleMigration
```

## For deploy docker use this command: 
```
docker compose up --build -d
```
or this for a specific docker file:
```
docker compose -f docker-compose.yml up --build -d
```
