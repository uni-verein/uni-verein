import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
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

test.describe('User management page', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Open user management page', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Nutzerverwaltung', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Nutzerverwaltung' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Benutzername' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'E-Mail' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Rolle' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Aktionen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Neuer Nutzer' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Admin' }).nth(3)).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Admin' }).nth(1)).toBeVisible();
    await expect(
      page.getByRole('row', { name: 'Admin ADMIN Bearbeiten Löschen' }).getByLabel('Bearbeiten'),
    ).toBeVisible();
    await expect(
      page.getByRole('row', { name: 'Admin ADMIN Bearbeiten Löschen' }).getByLabel('Löschen'),
    ).toBeVisible();
  });

  test('Create user', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Neuer Nutzer' })).toBeVisible();
    await page.getByRole('button', { name: 'Neuer Nutzer' }).click();
    await expect(page.getByRole('heading', { name: 'Neuer Nutzer' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Username' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Username' }).fill(`playwright_user`);

    await expect(page.getByRole('textbox', { name: 'Passwort' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Passwort' }).fill(`playw`);
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Passwort muss mindestens 11')).toBeVisible();
    await page.getByRole('textbox', { name: 'Passwort' }).fill(`playwright_user`);
    await expect(page.getByText('Passwort muss mindestens 11')).not.toBeVisible();

    await expect(page.getByRole('textbox', { name: 'E-Mail' })).toBeVisible();
    await page.getByRole('textbox', { name: 'E-Mail' }).fill(`test@mail.com`);

    await expect(page.getByText('User', { exact: true })).toBeVisible();
    await page.getByText('User', { exact: true }).click();
    await expect(page.getByRole('option', { name: 'User' })).toBeVisible();
    await expect(page.getByRole('option', { name: 'Admin' })).toBeVisible();
    await expect(page.getByRole('option', { name: 'Finanzverwalter' })).toBeVisible();
    await page.getByRole('option', { name: 'User' }).click();

    await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Speichern' })).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Nutzer erfolgreich erstellt.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'playwright_user', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'test@mail.com', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'User', exact: true })).toBeVisible();
    await expect(
      page
        .getByRole('row', { name: 'playwright_user test@mail.com User' })
        .getByLabel('Bearbeiten'),
    ).toBeVisible();
    await expect(
      page.getByRole('row', { name: 'playwright_user test@mail.com User' }).getByLabel('Löschen'),
    ).toBeVisible();

    await page
      .getByRole('row', { name: 'playwright_user test@mail.com User' })
      .getByLabel('Löschen')
      .click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await expect(page.getByText('Nutzer wirklich löschen?')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Nutzer erfolgreich gelöscht.' }),
    ).toBeVisible();
    await expect(
      page.getByRole('cell', { name: 'playwright_user', exact: true }),
    ).not.toBeVisible();
  });

  test('Create admin', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Neuer Nutzer' })).toBeVisible();
    await page.getByRole('button', { name: 'Neuer Nutzer' }).click();
    await page.getByRole('textbox', { name: 'Username' }).fill(`playwright_admin`);
    await page.getByRole('textbox', { name: 'E-Mail' }).fill(`admin@test.com`);
    await page.getByRole('textbox', { name: 'Passwort' }).fill(`playwright_admin`);
    await expect(page.getByText('Passwort muss mindestens 11')).not.toBeVisible();
    await page.getByText('User', { exact: true }).click();
    await page.getByRole('option', { name: 'Admin' }).click();
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Nutzer erfolgreich erstellt.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'playwright_admin', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'admin@test.com', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'ADMIN' }).nth(5)).toBeVisible();
    await expect(
      page
        .getByRole('row', { name: 'playwright_admin admin@test.com ADMIN' })
        .getByLabel('Bearbeiten'),
    ).toBeVisible();
    await expect(
      page
        .getByRole('row', { name: 'playwright_admin admin@test.com ADMIN' })
        .getByLabel('Löschen'),
    ).toBeVisible();

    await page
      .getByRole('row', { name: 'playwright_admin admin@test.com ADMIN' })
      .getByLabel('Löschen')
      .click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Nutzer erfolgreich gelöscht.' }),
    ).toBeVisible();
    await expect(
      page.getByRole('cell', { name: 'playwright_admin', exact: true }),
    ).not.toBeVisible();
  });

  test('Try to create duplicate user', async ({ page }) => {
    let user = await backend.createUser(Role.USER);

    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Neuer Nutzer' })).toBeVisible();
    await page.getByRole('button', { name: 'Neuer Nutzer' }).click();
    await page.getByRole('textbox', { name: 'Username' }).fill(user.username);
    await page.getByRole('textbox', { name: 'Passwort' }).fill(user.password);
    await expect(page.getByText('Passwort muss mindestens 11')).not.toBeVisible();
    await page.getByLabel('Neuer Nutzer').getByText('User', { exact: true }).click();
    await page.getByRole('option', { name: 'Admin' }).click();
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page.getByText('Nutzer konten nicht erstellt werden. Nutzername existiert bereits.'),
    ).toBeVisible();
    await expect(
      page.getByRole('alert').filter({ hasText: /^Nutzername existiert bereits\.$/ }),
    ).toBeVisible();

    await page
      .getByRole('row', { name: `${user.username} User` })
      .getByLabel('Löschen')
      .click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
  });

  test('edit user', async ({ page }) => {
    let user = await backend.createUser(Role.USER);
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }).click();
    await page
      .getByRole('row', { name: `${user.username} User` })
      .getByLabel('Bearbeiten')
      .click();
    expect(await page.getByRole('textbox', { name: 'Username' }).inputValue()).toEqual(
      user.username,
    );
    await expect(
      page.getByRole('textbox', { name: 'Passwort (leer lassen zum Behalten)' }),
    ).toBeVisible();
    await page.getByLabel('Nutzer bearbeiten').getByText('User', { exact: true }).click();
    await page.getByRole('option', { name: 'Admin' }).click();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Nutzeränderungen erfolgreich gespeichert.' }),
    ).toBeVisible();

    await page
      .getByRole('row', { name: `${user.username} Admin` })
      .getByLabel('Löschen')
      .click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
  });
});

test.describe('user account', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.USER);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Open user management page on user avatar', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await expect(page.getByLabel('Profileinstellungen')).toBeVisible();
    await page.getByLabel('Profileinstellungen').click();
    await expect(page.getByRole('heading', { name: 'Nutzerverwaltung' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Benutzername' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Rolle' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Aktionen' })).toBeVisible();
    await expect(
      page.getByRole('cell', { name: tc.get().user.username, exact: true }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'User', exact: true })).toBeVisible();
    await expect(
      page
        .getByRole('row', { name: `${tc.get().user.username} User Bearbeiten` })
        .getByLabel('Bearbeiten'),
    ).toBeVisible();
  });

  test('edit user', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await expect(page.getByLabel('Profileinstellungen')).toBeVisible();
    await page.getByLabel('Profileinstellungen').click();
    await expect(page.getByRole('heading', { name: 'Nutzerverwaltung' })).toBeVisible();
    await page
      .getByRole('row', { name: `${tc.get().user.username} User` })
      .getByLabel('Bearbeiten')
      .click();
    expect(await page.getByRole('textbox', { name: 'Username' }).inputValue()).toEqual(
      tc.get().user.username,
    );
    await expect(
      page.getByRole('textbox', { name: 'Passwort (leer lassen zum Behalten)' }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Nutzeränderungen erfolgreich gespeichert.' }),
    ).toBeVisible();
  });
});
