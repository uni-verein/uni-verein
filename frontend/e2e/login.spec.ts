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

test.describe('Login with default password forces a password change', () => {
  test('Default password shows the force-change dialog and blocks access until changed', async ({
    page,
  }) => {
    await page.goto('http://localhost/');
    await expect(page.locator('form')).toBeVisible();
    await page.getByRole('textbox', { name: 'Benutzername' }).fill('Admin');
    await page.getByRole('textbox', { name: 'Passwort' }).fill('admin123');
    await page.getByRole('button', { name: 'Anmelden' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Passwort ändern')).toBeVisible({ timeout: 8000 });
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).not.toBeVisible();

    const newPasswordField = dialog.getByRole('textbox', { name: 'Neues Passwort', exact: true });
    const confirmField = dialog.getByRole('textbox', { name: 'Neues Passwort bestätigen' });
    const submit = dialog.getByRole('button', { name: 'Passwort festlegen' });

    // Too short.
    await newPasswordField.fill('short1');
    await confirmField.fill('short1');
    await submit.click();
    await expect(
      dialog.getByText('Das Passwort muss mindestens 11 Zeichen lang sein.'),
    ).toBeVisible();

    // Confirmation doesn't match.
    await newPasswordField.fill('NewSecurePassword1');
    await confirmField.fill('SomethingDifferent1');
    await submit.click();
    await expect(dialog.getByText('Die Passwörter stimmen nicht überein.')).toBeVisible();

    // Dialog can't be dismissed without going through the flow above.
    await page.keyboard.press('Escape');
    await expect(dialog).toBeVisible();

    // Valid change
    await page.route('**/api/users/account', async (route) => {
      if (route.request().method() !== 'PATCH') {
        await route.continue();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: '', username: 'Admin', email: '', role: 'ADMIN' }),
      });
    });

    await newPasswordField.fill('NewSecurePassword1');
    await confirmField.fill('NewSecurePassword1');
    await submit.click();

    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Abmelden', exact: true })).toBeVisible({
      timeout: 8000,
    });
    await expect(page.getByText('ADMIN', { exact: true })).toBeVisible();
  });
});
