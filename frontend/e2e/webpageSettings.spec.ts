import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

async function openDashboard(
  page: Page,
  token: string,
  pageName: string | undefined = undefined,
): Promise<any> {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText(pageName ?? 'Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
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

test.describe('Web page – Configure', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Open webpage config', async ({ page }) => {
    await backend.deleteWebPageSettings();
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Webseiteneinstellungen', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Webseiteneinstellungen', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Webseiteneinstellungen', exact: true }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Vereinsnamen' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Vereinsnamen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Konfiguration speichern' })).toBeVisible();
  });

  test('Change webpage config', async ({ page }) => {
    await backend.deleteWebPageSettings();
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Webseiteneinstellungen', exact: true }).click();
    await page.getByRole('textbox', { name: 'Vereinsnamen' }).click();
    await page.getByRole('textbox', { name: 'Vereinsnamen' }).fill('Test page 2');
    await expect(page.getByRole('button', { name: 'Konfiguration speichern' })).toBeVisible();
    await page.getByRole('button', { name: 'Konfiguration speichern' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Speichern erfolgreich.' }),
    ).toBeVisible();
    await expect(page.getByText('Test page 2')).toBeVisible({ timeout: 1000 });
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await backend.deleteWebPageSettings();
  });

  test('Delete webpage settings', async ({ page }) => {
    let pageName = await backend.updateWebPageSettings();
    await openDashboard(page, tc.get().token, pageName);

    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Webseiteneinstellungen', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Webseiteneinstellungen', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Webseiteneinstellungen', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await expect(page.getByText('Webseiteneinstellungen wirklich löschen?')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'Löschen erfolgreich.' })).toBeVisible();
  });

  test('Show changed webpage name', async ({ page }) => {
    let pageName = await backend.updateWebPageSettings();
    await page.goto('http://localhost/');
    await expect(
      page.getByRole('heading', { name: `${pageName} Vereinsverwaltung` }),
    ).toBeVisible();
    await expect(
      page.getByText(`© ${new Date().getFullYear()} ${pageName} Vereinsverwaltung`),
    ).toBeVisible();
    await openDashboard(page, tc.get().token, pageName);
    await expect(page.getByText(pageName.substring(0, 1), { exact: true })).toBeVisible();
    await backend.deleteWebPageSettings();
  });
});
