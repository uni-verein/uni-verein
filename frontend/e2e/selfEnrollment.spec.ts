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

async function fillPublicEnrollmentForm(
  page: Page,
  { firstName, lastName, email }: { firstName: string; lastName: string; email: string },
) {
  await page.getByRole('textbox', { name: 'Vorname' }).fill(firstName);
  await page.getByRole('textbox', { name: 'Nachname' }).fill(lastName);

  await page.setViewportSize({ width: 1280, height: 1400 });
  const birthdayGroup = page.getByRole('group', { name: 'Geburtsdatum' });
  await birthdayGroup.getByRole('spinbutton', { name: 'Day' }).click();
  await page.keyboard.type('15032026');

  await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).fill('Teststr. 3');
  await page.getByRole('textbox', { name: 'PLZ' }).fill('24103');
  await page.getByRole('textbox', { name: 'Stadt' }).fill('Kiel');

  await page.getByRole('combobox', { name: 'Ländercode' }).click();
  await page.getByRole('option', { name: 'Angola (AO)' }).click();

  await page.getByRole('textbox', { name: 'E-Mail' }).fill(email);

  await page.getByRole('group', { name: 'Studienbeginn' }).getByLabel('Choose date').click();
  await page.getByRole('radio', { name: 'März', exact: true }).click();
  await page.getByRole('radio', { name: '2025', exact: true }).click();

  await page
    .getByRole('textbox', { name: 'Motivation' })
    .fill('Ich studiere Informatik und möchte mich im Verein engagieren.');
  await page.getByRole('textbox', { name: 'IBAN' }).fill(generateIBAN({ countryCode: 'DE' }));
  await page.getByRole('textbox', { name: 'BIC' }).fill('DEUTDEDEXXX');
}

test.beforeAll(async () => {
  await backend.init();
});

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

test.describe('Self-enrollment (UV-13 / UV-21)', () => {
  test.afterEach(async () => {
    await backend.deleteWebPageSettings();
    await backend.deleteAllMember();
    await backend.deleteAllPendingSelfEnrollments();
    await backend.deleteMailSettings();
    await backend.deleteAllPapercutMessages();
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
    await expect(page.getByRole('combobox', { name: 'Mitgliederkategorie' })).not.toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Beitragstarif' })).not.toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Motivation' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'IBAN' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'BIC' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: /Aufgabe im Verein/ })).not.toBeVisible();
    await expect(page.getByRole('group', { name: 'AustrittsDatum' })).not.toBeVisible();
    await expect(page.getByRole('group', { name: 'Eintrittsdatum' })).not.toBeVisible();
    await expect(page.getByRole('group', { name: 'SEPA-Zustimmung' })).not.toBeVisible();
  });

  test('Public success message stays visible until OK is clicked, then shows the empty form again', async ({
    page,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await page.goto('/enroll');

    await fillPublicEnrollmentForm(page, {
      firstName: 'StayOpen',
      lastName: 'Tester',
      email: `stayopen_${Date.now()}@test.de`,
    });
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    // Must not auto-close on its own (it used to, after 3s).
    await page.waitForTimeout(3500);
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    await page.getByRole('button', { name: 'OK' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).not.toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Vorname' })).toHaveValue('');
  });

  test('Submitting the public form asks for email confirmation, and only after confirming + approval does the member show up', async ({
    page,
    browser,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await backend.updateMailSettings();
    await page.goto('/enroll');

    const email = `selfenroll_${Date.now()}@test.de`;
    await fillPublicEnrollmentForm(page, { firstName: 'SelfEnroll', lastName: 'Tester', email });

    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await openDashboard(adminPage, token);
    await adminPage.getByRole('textbox', { name: 'Name suche' }).fill('SelfEnroll');
    await expect(
      adminPage.getByRole('row', { name: 'SelfEnroll Tester Mitglied' }),
    ).not.toBeVisible();

    const confirmToken = await backend.waitForConfirmationToken(email);
    await page.goto(`/enroll/confirm?token=${confirmToken}`);
    await expect(page.getByText('E-Mail bestätigt!')).toBeVisible();

    await adminPage.getByRole('button', { name: 'Aktualisieren' }).click();
    await expect(
      adminPage.getByRole('row', { name: 'SelfEnroll Tester Mitglied' }),
    ).not.toBeVisible();

    await adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' }).click();
    await expect(adminPage.getByRole('cell', { name: 'SelfEnroll Tester' })).toBeVisible();
    await adminPage
      .getByRole('row', { name: 'SelfEnroll Tester' })
      .getByLabel('Überprüfen')
      .click();
    await expect(
      adminPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).toBeVisible();
    await adminPage.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
    await adminPage.getByRole('option', { name: 'Anderes' }).click();
    await adminPage.getByRole('button', { name: 'Speichern & anlegen' }).click();
    await expect(
      adminPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).not.toBeVisible();

    await adminPage.getByRole('tab', { name: 'Mitglieder', exact: true }).click();
    await adminPage.getByRole('textbox', { name: 'Name suche' }).fill('SelfEnroll');
    await expect(adminPage.getByRole('row', { name: 'SelfEnroll Tester Mitglied' })).toBeVisible();

    await adminContext.close();
  });

  test('Deleting a confirmed self-enrollment never creates a member', async ({ page, browser }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await backend.updateMailSettings();
    await page.goto('/enroll');

    const email = `rejectenroll_${Date.now()}@test.de`;
    await fillPublicEnrollmentForm(page, { firstName: 'RejectEnroll', lastName: 'Tester', email });
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    const confirmToken = await backend.waitForConfirmationToken(email);
    await page.goto(`/enroll/confirm?token=${confirmToken}`);
    await expect(page.getByText('E-Mail bestätigt!')).toBeVisible();

    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await openDashboard(adminPage, token);
    await adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' }).click();
    await expect(adminPage.getByRole('cell', { name: 'RejectEnroll Tester' })).toBeVisible();
    await adminPage.getByRole('row', { name: 'RejectEnroll Tester' }).getByLabel('Löschen').click();
    await adminPage.getByRole('button', { name: 'Löschen' }).click();
    await expect(adminPage.getByRole('cell', { name: 'RejectEnroll Tester' })).not.toBeVisible();

    await adminPage.getByRole('tab', { name: 'Mitglieder', exact: true }).click();
    await adminPage.getByRole('textbox', { name: 'Name suche' }).fill('RejectEnroll');
    await expect(
      adminPage.getByRole('row', { name: 'RejectEnroll Tester Mitglied' }),
    ).not.toBeVisible();

    await adminContext.close();
  });

  test('"Ausstehende Mitglieder" tab is hidden when self-enrollment is off and nothing is pending', async ({
    page,
  }) => {
    await backend.setSelfEnrollmentEnabled(false);
    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    await openDashboard(page, token);
    await expect(page.getByRole('tab', { name: 'Mitglieder', exact: true })).not.toBeVisible();
    await expect(page.getByRole('tab', { name: 'Ausstehende Mitglieder' })).not.toBeVisible();
  });

  test('"Ausstehende Mitglieder" tab stays visible for a confirmed-but-undecided item even after self-enrollment is disabled again, and disappears once resolved', async ({
    page,
    browser,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await page.goto('/enroll');

    const email = `tabvisibility_${Date.now()}@test.de`;
    await fillPublicEnrollmentForm(page, {
      firstName: 'TabVisibility',
      lastName: 'Tester',
      email,
    });
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    const confirmToken = await backend.waitForConfirmationToken(email);
    await page.goto(`/enroll/confirm?token=${confirmToken}`);
    await expect(page.getByText('E-Mail bestätigt!')).toBeVisible();

    await backend.setSelfEnrollmentEnabled(false);

    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await openDashboard(adminPage, token);
    await expect(adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' })).toBeVisible();

    await adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' }).click();
    await adminPage
      .getByRole('row', { name: 'TabVisibility Tester' })
      .getByLabel('Überprüfen')
      .click();
    await adminPage.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
    await adminPage.getByRole('option', { name: 'Anderes' }).click();
    await adminPage.getByRole('button', { name: 'Speichern & anlegen' }).click();

    // Resolved, and self-enrollment stayed off - the tab disappears without a reload.
    await expect(adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' })).not.toBeVisible();

    await adminContext.close();
  });

  test('Reviewing a pending enrollment assigns a category and creates the member in one step', async ({
    page,
    browser,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await backend.updateMailSettings();
    await page.goto('/enroll');

    const email = `reviewenroll_${Date.now()}@test.de`;
    await fillPublicEnrollmentForm(page, { firstName: 'ReviewEnroll', lastName: 'Tester', email });
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    const confirmToken = await backend.waitForConfirmationToken(email);
    await page.goto(`/enroll/confirm?token=${confirmToken}`);
    await expect(page.getByText('E-Mail bestätigt!')).toBeVisible();

    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await openDashboard(adminPage, token);
    await adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' }).click();
    await expect(adminPage.getByRole('cell', { name: 'ReviewEnroll Tester' })).toBeVisible();

    await expect(
      adminPage.getByRole('row', { name: 'ReviewEnroll Tester' }).getByRole('cell', { name: '–' }),
    ).toBeVisible();

    await adminPage
      .getByRole('row', { name: 'ReviewEnroll Tester' })
      .getByLabel('Überprüfen')
      .click();
    await expect(
      adminPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).toBeVisible();
    await adminPage.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
    await adminPage.getByRole('option', { name: 'Studierende' }).click();
    await adminPage.getByRole('button', { name: 'Speichern & anlegen' }).click();
    await expect(
      adminPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).not.toBeVisible();

    await adminPage.getByRole('tab', { name: 'Mitglieder', exact: true }).click();
    await adminPage.getByRole('textbox', { name: 'Name suche' }).fill('ReviewEnroll');
    await expect(
      adminPage.getByRole('row', { name: /ReviewEnroll Tester Mitglied.*Studierende/ }),
    ).toBeVisible();

    await adminContext.close();
  });

  test('Reviewing and creating a member works for a non-admin reviewer too', async ({
    page,
    browser,
  }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await backend.updateMailSettings();
    await page.goto('/enroll');

    const email = `reviewenrolluser_${Date.now()}@test.de`;
    await fillPublicEnrollmentForm(page, {
      firstName: 'ReviewEnrollUser',
      lastName: 'Tester',
      email,
    });
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    const confirmToken = await backend.waitForConfirmationToken(email);
    await page.goto(`/enroll/confirm?token=${confirmToken}`);
    await expect(page.getByText('E-Mail bestätigt!')).toBeVisible();

    const user = await backend.createUser(Role.USER);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const userContext = await browser.newContext();
    const userPage = await userContext.newPage();
    await openDashboard(userPage, token);
    await userPage.getByRole('tab', { name: 'Ausstehende Mitglieder' }).click();
    await userPage
      .getByRole('row', { name: 'ReviewEnrollUser Tester' })
      .getByLabel('Überprüfen')
      .click();
    await userPage.getByRole('textbox', { name: 'Nachname' }).fill('TesterReviewed');
    await userPage.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
    await userPage.getByRole('option', { name: 'Anderes' }).click();
    await userPage.getByRole('button', { name: 'Speichern & anlegen' }).click();
    await expect(
      userPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).not.toBeVisible();

    await userPage.getByRole('tab', { name: 'Mitglieder', exact: true }).click();
    await userPage.getByRole('textbox', { name: 'Name suche' }).fill('ReviewEnrollUser');
    await expect(
      userPage.getByRole('row', { name: 'ReviewEnrollUser TesterReviewed' }),
    ).toBeVisible();

    await userContext.close();
  });

  test('Saving without a category is blocked client-side', async ({ page, browser }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await backend.updateMailSettings();
    await page.goto('/enroll');

    const email = `nocategory_${Date.now()}@test.de`;
    await fillPublicEnrollmentForm(page, { firstName: 'NoCategory', lastName: 'Tester', email });
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).toBeVisible();

    const confirmToken = await backend.waitForConfirmationToken(email);
    await page.goto(`/enroll/confirm?token=${confirmToken}`);
    await expect(page.getByText('E-Mail bestätigt!')).toBeVisible();

    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await openDashboard(adminPage, token);
    await adminPage.getByRole('tab', { name: 'Ausstehende Mitglieder' }).click();
    await adminPage
      .getByRole('row', { name: 'NoCategory Tester' })
      .getByLabel('Überprüfen')
      .click();
    await expect(
      adminPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).toBeVisible();

    await adminPage.getByRole('button', { name: 'Speichern & anlegen' }).click();

    await expect(
      adminPage.getByRole('heading', { name: 'Anmeldung überprüfen', exact: true }),
    ).toBeVisible();
    await adminPage.getByRole('button', { name: 'Abbrechen' }).click();
    await expect(adminPage.getByRole('cell', { name: 'NoCategory Tester' })).toBeVisible();

    await adminContext.close();
  });

  test('Public form requires motivation', async ({ page }) => {
    await backend.setSelfEnrollmentEnabled(true);
    await page.goto('/enroll');

    await fillPublicEnrollmentForm(page, {
      firstName: 'MissingFields',
      lastName: 'Tester',
      email: `missingfields_${Date.now()}@test.de`,
    });
    await page.getByRole('textbox', { name: 'Motivation' }).fill('');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(page.getByText('Motivation darf nicht leer sein.')).toBeVisible();
    await expect(page.getByText('Bitte bestätige deine E-Mail-Adresse')).not.toBeVisible();
  });

  test('Confirming with an invalid or expired token shows an error', async ({ page }) => {
    await page.goto('/enroll/confirm?token=this-token-does-not-exist');

    await expect(page.getByText('Link ungültig oder abgelaufen')).toBeVisible();
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

    await fillPublicEnrollmentForm(page, {
      firstName: 'Dupe',
      lastName: 'Tester',
      email: existing.email,
    });

    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page
        .getByRole('alert')
        .filter({ hasText: /^Mitglied mit gleicher IBAN oder E-Mail existiert bereits\.$/ }),
    ).toBeVisible();
  });
});
