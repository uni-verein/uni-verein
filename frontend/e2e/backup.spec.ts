import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import path from 'path';
import fs from 'fs';
import os from 'os';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

async function openDashboard(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

test.beforeAll(async () => {
  await backend.init();
  await backend.deleteAllMember();
});

function makeTestContextOnlyUser() {
  let ctx: { user: TestUser; token: string };
  return {
    get: () => ctx,
    setup: async (role: Role) => {
      const user = await backend.createUser(role);
      const token = await backend.loginUser(user.username, user.password);

      createdUserIds.add(user.id);

      ctx = { user, token };
    },
    teardown: async () => {
      await backend.deleteUser(ctx.user.id);
      createdUserIds.delete(ctx.user.id);
    },
  };
}

test.beforeAll(async () => {
  await backend.init();
});

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

test.describe('Backup page', () => {
  test.describe('Check the backup page', () => {
    const tc = makeTestContextOnlyUser();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Open backup page', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.getByRole('button', { name: 'Backup', exact: true }).click();

      await expect(page.getByRole('heading', { name: 'Backup & Datenmanagement' })).toBeVisible();
      await expect(
        page.getByText('Sichern Sie Ihre Vereinsdaten oder importieren Sie bestehende Datensätze.'),
      ).toBeVisible();

      await expect(page.getByText('Datensicherheit')).toBeVisible();
      await expect(
        page.getByText(
          'Es wird empfohlen, vor jedem Import eine manuelle Sicherung der Datenbank durchzuführen.',
        ),
      ).toBeVisible();

      await expect(page.getByRole('heading', { name: 'System-Backup' })).toBeVisible();
      await expect(
        page.getByText(
          'Erstellt eine vollständige Sicherung der Datenbank. Alle Mitglieder, Beiträge und Einstellungen werden in einer .bak Datei gespeichert.',
        ),
      ).toBeVisible();
      await expect(
        page.getByRole('button', { name: 'Sicherung jetzt herunterladen' }),
      ).toBeVisible();

      await expect(page.getByRole('heading', { name: 'System wiederherstellen' })).toBeVisible();
      await expect(
        page.getByText(
          'Wählen Sie eine Backup-Datei aus, um das System auf einen früheren Stand zurückzusetzen.',
        ),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'Backup-Datei wählen' })).toBeVisible();

      await expect(
        page.getByRole('heading', { name: 'Mitglieder via CSV importieren' }),
      ).toBeVisible();
      await expect(
        page.getByText(
          'Laden Sie eine Liste im Format .csv hoch, um mehrere Mitglieder gleichzeitig anzulegen.',
        ),
      ).toBeVisible();
      await expect(
        page.getByRole('button', { name: 'Beispiel-CSV-Datei herunterladen' }),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'CSV Datei hochladen' })).toBeVisible();
    });

    test('Download system backup from backend', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.getByRole('button', { name: 'Backup', exact: true }).click();

      const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.getByRole('button', { name: /Sicherung jetzt herunterladen/i }).click(),
      ]);

      expect(download.suggestedFilename()).toMatch(/Verein_Backup_\d{4}-\d{2}-\d{2}\.sql/);

      const downloadPath = await download.path();
      const stats = fs.statSync(downloadPath!);
      expect(stats.size).toBeGreaterThan(0);

      await expect(page.getByText('Download erfolgreich.')).toBeVisible();
    });

    test('Download system backup and restore', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.getByRole('button', { name: 'Backup', exact: true }).click();

      const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.getByRole('button', { name: /Sicherung jetzt herunterladen/i }).click(),
      ]);

      await expect(page.getByText('Download erfolgreich.')).toBeVisible();
      const backupPath = path.join(os.tmpdir(), download.suggestedFilename());
      await download.saveAs(backupPath);
      expect(fs.existsSync(backupPath)).toBe(true);

      const fileInput = page.locator('input[type="file"]').first();
      await fileInput.setInputFiles(backupPath);

      await expect(page.getByText(/ACHTUNG/i)).toBeVisible();
      await page.getByRole('button', { name: /System wiederherstellen/i }).click();
      await expect(page.getByText('System erfolgreich wiederhergestellt.')).toBeVisible({
        timeout: 15000,
      });

      fs.unlinkSync(backupPath);
    });

    test('Cancel restore dialog', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.getByRole('button', { name: 'Backup', exact: true }).click();
      const backupPath = path.join(os.tmpdir(), 'dummy-backup.sql');
      fs.writeFileSync(backupPath, '-- dummy');

      const fileInput = page.locator('input[type="file"]').first();
      await fileInput.setInputFiles(backupPath);
      await expect(page.getByText(/ACHTUNG/i)).toBeVisible();

      const [request] = await Promise.all([
        page.waitForRequest('**/backup/restore', { timeout: 2000 }).catch(() => null),
        page.getByRole('button', { name: /Abbrechen/i }).click(),
      ]);

      expect(request).toBeNull();
      fs.unlinkSync(backupPath);
    });

    test('Download member example import CSV file', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.getByRole('button', { name: 'Backup', exact: true }).click();

      const [downloadExampleCsv] = await Promise.all([
        page.waitForEvent('download'),
        page.getByRole('button', { name: /Beispiel-CSV-Datei herunterladen/i }).click(),
      ]);

      await expect(page.getByText('Download erfolgreich.')).toBeVisible();
      const backupPath = path.join(os.tmpdir(), downloadExampleCsv.suggestedFilename());
      await downloadExampleCsv.saveAs(backupPath);
      expect(fs.existsSync(backupPath)).toBe(true);

      const fileContent = fs.readFileSync(backupPath, 'utf-8');
      expect(fileContent.length).toBeGreaterThan(0);
      const lines = fileContent.split('\n').filter((line) => line.trim() !== '');
      const headers = lines[0].split(';').map((h) => h.trim());
      expect(headers).toContain('Member number');
      expect(headers).toContain('Gender');
      expect(headers).toContain('Name');
      expect(headers).toContain('First name');
      expect(headers).toContain('Middle name');
      expect(headers).toContain('Birthday');
      expect(headers).toContain('Phone nummer');
      expect(headers).toContain('Bulk mail');
      expect(headers).toContain('Mail');
      expect(headers).toContain('Street and number');
      expect(headers).toContain('ZIP code');
      expect(headers).toContain('City');
      expect(headers).toContain('Study start');
      expect(headers).toContain('Study end');
      expect(headers).toContain('Academic degree');
      expect(headers).toContain('Course of study');
      expect(headers).toContain('Task within the club');
      expect(headers).toContain('Member category');
      expect(headers).toContain('Entry date');
      expect(headers).toContain('Exit date');
      expect(headers).toContain('IBAN');
      expect(headers).toContain('BIC');
      expect(headers).toContain('Sepa consent date');
      expect(headers).toContain('Contribution amount');

      expect(lines.length).toBe(2);
      const firstDataRow = lines[1].split(';');
      expect(firstDataRow).toContain('1');
      expect(firstDataRow).toContain('MALE');
      expect(firstDataRow).toContain('Mustermann');
      expect(firstDataRow).toContain('Max');
      expect(firstDataRow).toContain('01.01.2000');
      expect(firstDataRow).toContain('+49 172 12345678');
      expect(firstDataRow).toContain('ALLOWED');
      expect(firstDataRow).toContain('max.mustermann@gmail.com');
      expect(firstDataRow).toContain('Musterstraße 1');
      expect(firstDataRow).toContain('12345');
      expect(firstDataRow).toContain('Musterstadt');
      expect(firstDataRow).toContain('01.10.2015');
      expect(firstDataRow).toContain('b.sc.');
      expect(firstDataRow).toContain('Informatics');
      expect(firstDataRow).toContain('MEMBER');
      expect(firstDataRow).toContain('STUDENT');
      expect(firstDataRow).toContain('10.10.2015');
      expect(firstDataRow).toContain('DE89370400440532013000');
      expect(firstDataRow).toContain('INGDDEFFXXX');
      expect(firstDataRow).toContain('12\r');

      fs.unlinkSync(backupPath);
    });

    test('Try to Import invalid members by a CSV-file', async ({ page }) => {
      await backend.deleteAllMember();
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 1200 });
      await page.getByRole('button', { name: 'Backup' }).click();
      await expect(page.getByRole('button', { name: 'CSV Datei hochladen' })).toBeVisible();

      const csvPath = path.join(os.tmpdir(), 'test-members.csv');
      fs.writeFileSync(
        csvPath,
        [
          'Member number;Gender;Name;First name;Middle name;Birthday;Phone nummer;Bulk mail;Mail;Street and number;ZIP code;City;Study start;Study end;Academic degree;Course of study;Task within the club;Member category;Entry date;Exit date;Iban;Bic;Sepa consent date;Extra payment',
          '1;;Mustermann;Max;;22.03.1992;015210947617;ALLOWD;max.mustermann@gmx.de ,;Teststraße 3;24105;Kiel;01.10.2017;01.07.2019;BSC;Angewandte-Informatik;MEMBER;STUDENT;01.10.2017;;DE02120300000000202051;BYLADEM1001;01.10.2017;30',
          '1;;Mustermann;Max;;22.03.1992;015210947617;ALLOWD;max.mustermann@gmx.de ,;Teststraße 3;24105;Kiel;01.10.2017;01.07.2019;BSC;Angewandte-Informatik;MEMBE;STUDNT;01.10.2017;;D02120300000000202051;BYLADEM1001;01.10.2017;30',
        ].join('\n'),
      );
      const importButton = page.locator('input[type="file"]').nth(1);
      await importButton.setInputFiles(csvPath);

      await expect(page.getByText(/Mitglieder wirklich importieren/i)).toBeVisible();
      await page.getByRole('button', { name: /Importieren/i }).click();

      await expect(page.getByText('Importfehler')).toBeVisible({ timeout: 10000 });
      await expect(
        page.getByText('Die eingelesene Datei weist folgende Fehler auf:'),
      ).toBeVisible();

      await expect(
        page.getByRole('listitem').filter({ hasText: "Zeile 2: 'Gender' ist ein Pflichtfeld." }),
      ).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({
          hasText:
            "Zeile 2: 'Bulk mail' hat einen ungültigen Wert 'ALLOWD'. Erlaubt: ALLOWED, NOT_ALLOWED.",
        }),
      ).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({
          hasText: "Zeile 2: 'Mail' hat ein ungültiges Format (Wert: 'max.mustermann@gmx.de ,').",
        }),
      ).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({ hasText: "Zeile 3: 'Gender' ist ein Pflichtfeld." }),
      ).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({
          hasText: "Zeile 3: 'Mail' hat ein ungültiges Format (Wert: 'max.mustermann@gmx.de ,').",
        }),
      ).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({
          hasText:
            "Zeile 3: 'Task within the club' hat einen ungültigen Wert 'MEMBE'. Erlaubt: MEMBER, CHAIRMAN, SECOND_CHAIRMAN, JUNIOR_BOARD_MEMBER, CHIEF_FINANCE_OFFICER, WEBSITE_MANAGER, ALUMNI_OFFICER, STUDENT_COUNCIL_REPRESENTATIVE.",
        }),
      ).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({
          hasText: "Zeile 3: 'IBAN' hat ein ungültiges Format (Wert: 'D02120300000000202051').",
        }),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'Schließen' })).toBeVisible();

      fs.unlinkSync(csvPath);
    });

    test('Import members by a CSV-file', async ({ page }) => {
      await backend.deleteAllMember();
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 1200 });
      await page.getByRole('button', { name: 'Backup' }).click();
      await expect(page.getByRole('button', { name: 'CSV Datei hochladen' })).toBeVisible();

      const csvPath = path.join(os.tmpdir(), 'test-members.csv');
      fs.writeFileSync(
        csvPath,
        [
          'Member number;Gender;Name;First name;Middle name;Birthday;Phone nummer;Bulk mail;Mail;Street and number;ZIP code;City;Study start;Study end;Academic degree;Course of study;Task within the club;Member category;Entry date;Exit date;Iban;Bic;Sepa consent date;Extra payment',
          '1;MALE;Mustermann;Max;;22.03.1992;015210947617;ALLOWED;max.mustermann@gmx.de;Teststraße 3;24105;Kiel;01.10.2017;01.07.2019;b.sc.;Angewandte-Informatik;MEMBER;STUDENT;01.10.2017;;DE02120300000000202051;BYLADEM1001;01.10.2017;30',
        ].join('\n'),
      );
      const importButton = page.locator('input[type="file"]').nth(1);
      await importButton.setInputFiles(csvPath);

      await expect(page.getByText(/Mitglieder wirklich importieren/i)).toBeVisible();
      await page.getByRole('button', { name: /Importieren/i }).click();

      await expect(page.getByText('Mitglieder erfolgreich importiert.')).toBeVisible({
        timeout: 10000,
      });

      fs.unlinkSync(csvPath);
    });

    test('Export members to a CSV-file', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 1200 });
      await page.getByRole('button', { name: 'Backup', exact: true }).click();

      const exportButton = page.getByRole('button', { name: /Exportiere alle Mitglieder/i });
      await expect(exportButton).toBeVisible();

      const [download] = await Promise.all([page.waitForEvent('download'), exportButton.click()]);

      expect(download.suggestedFilename()).toMatch(/export_\d{4}-\d{2}-\d{2}\.csv/);

      const downloadPath = await download.path();
      const content = fs.readFileSync(downloadPath!, 'utf-8');
      expect(content.split('\n').length).toBeGreaterThan(2);

      await expect(page.getByText('Mitglieder erfolgreich exportiert.')).toBeVisible();
      await backend.deleteAllMember();
    });
  });
});
