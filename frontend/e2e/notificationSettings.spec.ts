import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const backend = new BackendClient();

async function openDashboard(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

async function openNotificationSettingsTab(page: Page) {
  await expect(page.getByLabel('Profileinstellungen')).toBeVisible();
  await page.getByLabel('Profileinstellungen').click();
  await expect(page.getByRole('heading', { name: 'Nutzerverwaltung' })).toBeVisible();
  await page.getByRole('tab', { name: 'Benachrichtigungseinstellungen' }).click();
}

function makeTestContextOnlyUser() {
  let ctx: { user: TestUser; token: string };
  return {
    get: () => ctx,
    setup: async (role: Role) => {
      const user = await backend.createUser(role);
      const token = await backend.loginUser(user.username, user.password);
      ctx = { user, token };
    },
    teardown: async () => {
      await backend.deleteUser(ctx.user.id);
    },
  };
}

test.beforeAll(async () => {
  await backend.init();
});

test.describe('Notification settings tab', () => {
  const tc = makeTestContextOnlyUser();
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Financial manager sees the receipt notification setting on by default and can turn it off', async ({
    page,
  }) => {
    await tc.setup(Role.FINANCIAL_MANAGER);
    await openDashboard(page, tc.get().token);
    await openNotificationSettingsTab(page);

    const toggle = page.getByRole('switch', {
      name: 'Bei neuem Beleg per E-Mail benachrichtigen',
    });
    await expect(toggle).toBeVisible();
    await expect(toggle).toBeChecked();

    await toggle.click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Einstellung erfolgreich gespeichert.' }),
    ).toBeVisible();
    await expect(toggle).not.toBeChecked();

    await page.reload();
    await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
    await openNotificationSettingsTab(page);
    await expect(
      page.getByRole('switch', { name: 'Bei neuem Beleg per E-Mail benachrichtigen' }),
    ).not.toBeChecked();
  });

  test('Regular user sees the tab but no receipt notification toggle', async ({ page }) => {
    await tc.setup(Role.USER);
    await openDashboard(page, tc.get().token);
    await openNotificationSettingsTab(page);

    await expect(
      page.getByText('Für Ihre Rolle sind aktuell keine Benachrichtigungseinstellungen verfügbar.'),
    ).toBeVisible();
    await expect(
      page.getByRole('switch', { name: 'Bei neuem Beleg per E-Mail benachrichtigen' }),
    ).not.toBeVisible();
  });

  test('Admin sees the tab but no receipt notification toggle', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await openNotificationSettingsTab(page);

    await expect(
      page.getByText('Für Ihre Rolle sind aktuell keine Benachrichtigungseinstellungen verfügbar.'),
    ).toBeVisible();
    await expect(
      page.getByRole('switch', { name: 'Bei neuem Beleg per E-Mail benachrichtigen' }),
    ).not.toBeVisible();
  });
});

test.describe('Receipt notification email delivery', () => {
  let createdUserIds: string[] = [];

  test.beforeEach(async () => {
    createdUserIds = [];
    await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
    await backend.updateMailSettings();
  });

  test.afterEach(async () => {
    for (const id of createdUserIds) {
      await backend.deleteUser(id);
    }
    await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
    await backend.deleteMailSettings();
    await backend.deleteAllReceipts();
  });

  test('Opted-in financial manager receives an email when a user submits a receipt', async ({
    page,
  }) => {
    const financialManager = await backend.createUser(
      Role.FINANCIAL_MANAGER,
      `fm_${Date.now()}@test.de`,
    );
    createdUserIds.push(financialManager.id);
    const financialManagerToken = await backend.loginUser(
      financialManager.username,
      financialManager.password,
    );
    await backend.setUserSetting(financialManagerToken, 'RECEIPT_NOTIFICATION', true);

    const submitter = await backend.createUser(Role.USER);
    createdUserIds.push(submitter.id);
    const submitterToken = await backend.loginUser(submitter.username, submitter.password);
    await backend.createTestReceipt(submitterToken);

    await page.goto('http://localhost:8080');
    await expect(page.getByRole('link', { name: 'Inbox (1)' })).toBeVisible({ timeout: 10000 });
  });

  test('Financial manager submitting their own receipt notifies nobody', async ({ page }) => {
    const financialManagerA = await backend.createUser(
      Role.FINANCIAL_MANAGER,
      `fm-a_${Date.now()}@test.de`,
    );
    createdUserIds.push(financialManagerA.id);
    const tokenA = await backend.loginUser(financialManagerA.username, financialManagerA.password);
    await backend.setUserSetting(tokenA, 'RECEIPT_NOTIFICATION', true);

    const financialManagerB = await backend.createUser(
      Role.FINANCIAL_MANAGER,
      `fm-b_${Date.now()}@test.de`,
    );
    createdUserIds.push(financialManagerB.id);
    const tokenB = await backend.loginUser(financialManagerB.username, financialManagerB.password);
    await backend.setUserSetting(tokenB, 'RECEIPT_NOTIFICATION', true);

    await backend.createTestReceipt(tokenB);

    await page.goto('http://localhost:8080');
    await expect(page.getByRole('link', { name: 'Inbox (0)' })).toBeVisible({ timeout: 10000 });
  });
});
