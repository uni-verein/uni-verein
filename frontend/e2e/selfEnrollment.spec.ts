import { expect, Page, test } from '@playwright/test';
import { APP_BASE, BackendClient } from './BackendClient';
import { generateIBAN } from './utils';
import { Role } from '../src/types';

const backend = new BackendClient();
const createdUserIds = new Set<string>();

async function openDashboard(page: Page, token: string, pageName = 'Test web page') {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText(pageName)).toBeVisible({ timeout: 8000 });
}

test.beforeAll(async () => {
  await backend.init();
});

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

test.describe('Self-enrollment (UV-13)', () => {
  test.afterEach(async () => {
    await backend.deleteWebPageSettings();
    await backend.deleteAllMember();
  });

  test('Disabled self-enrollment shows closed message, not the form', async ({ page }) => {
    await backend.setSelfEnrollmentEnabled(false);

    await page.goto('/enroll');

    await expect(page.getByText('Selbstregistrierung nicht verfügbar')).toBeVisible();
    await expect(page.getByText('Neues Mitglied anlegen', { exact: true })).not.toBeVisible();
  });

  test('Direct fresh navigation to /enroll works (nginx SPA fallback)', async ({ page }) => {
    await backend.setSelfEnrollmentEnabled(true);

    const response = await page.goto('/enroll');

    expect(response?.ok()).toBeTruthy();
    await expect(page.getByText('Neues Mitglied anlegen', { exact: true })).toBeVisible();
  });

  test('Enabled self-enrollment shows the public form without admin-only fields', async ({
    page,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);

    await page.goto('/enroll');

    await expect(page.getByText('Neues Mitglied anlegen', { exact: true })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Mitgliedsnummer' })).not.toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Vorname' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Nachname' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'E-Mail' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Mitgliederkategorie' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: /Aufgabe im Verein/ })).not.toBeVisible();
    await expect(page.getByRole('group', { name: 'AustrittsDatum' })).not.toBeVisible();
    await expect(page.getByRole('group', { name: 'Eintrittsdatum' })).not.toBeVisible();
    await expect(page.getByRole('group', { name: 'SEPA-Zustimmung' })).not.toBeVisible();
  });

  test('Submitting the public form creates a live member as "Mitglied"', async ({
    page,
    browser,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await page.goto('/enroll');

    await page.getByRole('textbox', { name: 'Vorname' }).fill('SelfEnroll');
    await page.getByRole('textbox', { name: 'Nachname' }).fill('Tester');

    await page.setViewportSize({ width: 1280, height: 1400 });
    const birthdayGroup = page.getByRole('group', { name: 'Geburtsdatum' });
    await birthdayGroup.getByRole('spinbutton', { name: 'Day' }).click();
    await page.keyboard.type('15032026');

    await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).fill('Teststr. 3');
    await page.getByRole('textbox', { name: 'PLZ' }).fill('24103');
    await page.getByRole('textbox', { name: 'Stadt' }).fill('Kiel');

    await page.getByRole('combobox', { name: 'Ländercode' }).click();
    await page.getByRole('option', { name: 'Angola (AO)' }).click();

    const email = `selfenroll_${Date.now()}@test.de`;
    await page.getByRole('textbox', { name: 'E-Mail' }).fill(email);

    await page.getByRole('group', { name: 'Studienbeginn' }).getByLabel('Choose date').click();
    await page.getByRole('radio', { name: 'März', exact: true }).click();
    await page.getByRole('radio', { name: '2025', exact: true }).click();

    await page.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
    await page.getByRole('option', { name: 'Anderes' }).click();

    await page.getByRole('textbox', { name: 'IBAN' }).fill(generateIBAN({ countryCode: 'DE' }));
    await page.getByRole('textbox', { name: 'BIC' }).fill('DEUTDEDEXXX');

    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Willkommen im Verein!')).toBeVisible();

    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await openDashboard(adminPage, token);
    await adminPage.getByRole('textbox', { name: 'Name suche' }).fill('SelfEnroll');
    await expect(adminPage.getByRole('cell', { name: 'SelfEnroll Tester' })).toBeVisible();
    await expect(adminPage.getByRole('row', { name: 'SelfEnroll Tester Mitglied' })).toBeVisible();
    await adminContext.close();
  });

  test('Duplicate email on the public form shows the same conflict error as the admin dialog', async ({
    page,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    const existing = await backend.createTestMember({
      email: `dupe_${Date.now()}@test.de`,
      iban: generateIBAN({ countryCode: 'DE' }),
    });

    await page.goto('/enroll');

    await page.getByRole('textbox', { name: 'Vorname' }).fill('Dupe');
    await page.getByRole('textbox', { name: 'Nachname' }).fill('Tester');

    await page.setViewportSize({ width: 1280, height: 1400 });
    const birthdayGroup = page.getByRole('group', { name: 'Geburtsdatum' });
    await birthdayGroup.getByRole('spinbutton', { name: 'Day' }).click();
    await page.keyboard.type('15032026');

    await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).fill('Teststr. 3');
    await page.getByRole('textbox', { name: 'PLZ' }).fill('24103');
    await page.getByRole('textbox', { name: 'Stadt' }).fill('Kiel');

    await page.getByRole('combobox', { name: 'Ländercode' }).click();
    await page.getByRole('option', { name: 'Angola (AO)' }).click();

    await page.getByRole('textbox', { name: 'E-Mail' }).fill(existing.email);

    await page.getByRole('group', { name: 'Studienbeginn' }).getByLabel('Choose date').click();
    await page.getByRole('radio', { name: 'März', exact: true }).click();
    await page.getByRole('radio', { name: '2025', exact: true }).click();

    await page.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
    await page.getByRole('option', { name: 'Anderes' }).click();

    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page
        .getByRole('alert')
        .filter({ hasText: /^Mitglied mit gleicher IBAN oder E-Mail existiert bereits\.$/ }),
    ).toBeVisible();
  });
});
