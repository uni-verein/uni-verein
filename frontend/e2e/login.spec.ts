import { test, expect } from '@playwright/test';
import { BackendClient } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

function makeTestContextOnlyUser() {
  let user: TestUser;
  return {
    get: () => user,
    setup: async (role: Role) => {
      user = await backend.createUser(role);
      createdUserIds.add(user.id);
    },
    teardown: async () => {
      await backend.deleteUser(user.id);
      createdUserIds.delete(user.id);
    },
  };
}

test.beforeAll(async () => {
  await backend.init();
});

test.describe('Login – Page end login', () => {
  test('show formular', async ({ page }) => {
    await page.goto('http://localhost/');
    await expect(page.getByRole('heading', { name: 'Vereinsverwaltung' })).toBeVisible();
    await expect(page.getByText('Bitte melden Sie sich an, um fortzufahren.')).toBeVisible();
    await expect(page.locator('form')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Benutzername' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Passwort' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Anmelden' })).toBeVisible();
  });

  test('show copyright', async ({ page }) => {
    await page.goto('http://localhost/');
    await expect(page.getByText(`© ${new Date().getFullYear()} Vereinsverwaltung`)).toBeVisible();
  });
});

test('fail to login', async ({ page }) => {
  await page.goto('http://localhost/');
  await expect(page.locator('form')).toBeVisible();
  await page.getByRole('textbox', { name: 'Benutzername' }).click();
  await page.getByRole('textbox', { name: 'Benutzername' }).fill('TestUser');
  await page.getByRole('textbox', { name: 'Benutzername' }).press('Tab');
  await page.getByRole('textbox', { name: 'Passwort' }).fill('password123');
  await page.getByRole('textbox', { name: 'Passwort' }).press('Tab');
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await expect(page.locator('form')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Anmelden' })).toBeVisible();
});

test.describe('Login as admin', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Login', async ({ page }) => {
    await page.goto('http://localhost/');
    await expect(page.locator('form')).toBeVisible();
    await page.getByRole('textbox', { name: 'Benutzername' }).click();
    await page.getByRole('textbox', { name: 'Benutzername' }).fill(tc.get().username);
    await page.getByRole('textbox', { name: 'Passwort' }).click();
    await page.getByRole('textbox', { name: 'Passwort' }).fill(tc.get().password);
    await page.getByRole('button', { name: 'Anmelden' }).click();
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).toBeVisible({
      timeout: 8000,
    });
    await expect(page.getByText('ADMIN', { exact: true })).toBeVisible();
  });

  test('keeps the session in an httpOnly cookie, not in the localStorage', async ({ page }) => {
    await page.goto('http://localhost/');
    await page.getByRole('textbox', { name: 'Benutzername' }).fill(tc.get().username);
    await page.getByRole('textbox', { name: 'Passwort' }).fill(tc.get().password);
    await page.getByRole('button', { name: 'Anmelden' }).click();
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).toBeVisible({
      timeout: 8000,
    });

    expect(await page.evaluate(() => localStorage.getItem('token'))).toBeNull();
    expect(await page.evaluate(() => document.cookie)).not.toContain('uv_auth');
    const cookie = (await page.context().cookies()).find((c) => c.name === 'uv_auth');
    expect(cookie?.httpOnly).toBe(true);

    await page.reload();
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).toBeVisible({
      timeout: 8000,
    });

    await page.getByRole('button', { name: 'Abmelden', exact: true }).click();
    await expect(page.locator('form')).toBeVisible();
    expect((await page.context().cookies()).find((c) => c.name === 'uv_auth')).toBeUndefined();
  });
});

test.describe('Login as user', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.USER);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Login', async ({ page }) => {
    await page.goto('http://localhost/');
    await expect(page.locator('form')).toBeVisible();
    await page.getByRole('textbox', { name: 'Benutzername' }).click();
    await page.getByRole('textbox', { name: 'Benutzername' }).fill(tc.get().username);
    await page.getByRole('textbox', { name: 'Passwort' }).click();
    await page.getByRole('textbox', { name: 'Passwort' }).fill(tc.get().password);
    await page.getByRole('button', { name: 'Anmelden' }).click();
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).toBeVisible({
      timeout: 8000,
    });
    await expect(page.getByText('USER', { exact: true })).toBeVisible();
  });
});

test.describe('Login as financial manager', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.FINANCIAL_MANAGER);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Login', async ({ page }) => {
    await page.goto('http://localhost/');
    await expect(page.locator('form')).toBeVisible();
    await page.getByRole('textbox', { name: 'Benutzername' }).click();
    await page.getByRole('textbox', { name: 'Benutzername' }).fill(tc.get().username);
    await page.getByRole('textbox', { name: 'Passwort' }).click();
    await page.getByRole('textbox', { name: 'Passwort' }).fill(tc.get().password);
    await page.getByRole('button', { name: 'Anmelden' }).click();
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).toBeVisible({
      timeout: 8000,
    });
    await expect(page.getByText('FINANCIAL_MANAGER', { exact: true })).toBeVisible();
  });
});
