## Add migrations
1. Start a mysql database:
```
docker run --name mysql-db -e MYSQL_ROOT_PASSWORD=root -e MYSQL_DATABASE=club -p 3306:3306 -d mysql:latest
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
