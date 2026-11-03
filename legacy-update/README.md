# Legacy update (MariaDB, v1.3.1 and earlier)

`v1.4.0` replaced MariaDB with PostgreSQL. Installations that still run MariaDB cannot use the normal `docker-compose-update.yml` from the repository root, it stops with a pointer to this page. Use the updater in this folder instead, it moves the installation to `v1.4.0` and then hands over to the normal updater.

You are affected if the compose file of your installation (`docker-compose-prod-image.yml` or `docker-compose-tls.yml`) contains `image: mariadb`.

> **⚠️ Never edit `VERSION` in `.env` by hand and never swap in a newer compose file yourself.** That skips the database migration and breaks the installation.

## Steps

1. Create a manual backup in the app (Settings → Backup): database backup **and** member export.
2. In your installation directory (the one containing `.env`) download and run the legacy updater:

   ```bash
   curl -O https://raw.githubusercontent.com/uni-verein/uni-verein/main/legacy-update/docker-compose-update.yml
   docker compose -f docker-compose-update.yml run --rm update
   ```

3. Each run advances exactly one step. Check the printed "Installed version -> Target version" line and run the command again until the installation is on `v1.4.0`:

   | Installed | Target | What happens |
   |---|---|---|
   | before `v1.3.1` | `v1.3.1` | normal update, still MariaDB |
   | `v1.3.1` | `v1.4.0` | database (structure and data) is migrated to PostgreSQL, the whole stack including backups is switched over |

   It is safe to re-run, each run picks up where the previous one left off.
4. Now the installation is on PostgreSQL. Replace the updater with the normal one and continue with the latest release (see [Updating](../README.md#updating)):

   ```bash
   curl -O https://raw.githubusercontent.com/uni-verein/uni-verein/main/docker-compose-update.yml
   docker compose -f docker-compose-update.yml run --rm update
   ```

## The migration to v1.4.0

- The migration needs extra downtime, proportional to the size of your database.
- It copies the data into a temporary PostgreSQL instance while MariaDB keeps running. Only after the copy is verified, the old MariaDB container is stopped. If the migration fails, it is cleaned up and the MariaDB installation is left untouched.
- A copy of your previous compose file is kept as `<compose file>.mariadb-backup` for the duration of the update and is removed automatically once the stack has restarted on PostgreSQL.

## Afterwards

The old MariaDB data volume is stopped, not deleted, and stays as a safety net. Once you have confirmed everything works on PostgreSQL, reclaim the disk space with `docker volume rm <project>_db_data` (find the exact name via `docker volume ls`).

To roll back manually before that: restore `<compose file>.mariadb-backup` as `<compose file>`, reset `VERSION` in `.env` and run `docker compose -f <compose file> up -d`. The MariaDB data volume was not touched.
