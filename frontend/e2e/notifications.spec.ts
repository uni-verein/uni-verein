import { test, expect, Page, Route } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

const FIRMWARE_UPDATE_ROUTE = '**/api/notifications/firmware-update';

async function mockFirmwareUpdate(
  page: Page,
  body: { newFirmwareAvailable: boolean; currentVersion?: string; latestVersion?: string } | null,
) {
  await page.route(FIRMWARE_UPDATE_ROUTE, (route: Route) => {
    if (body === null) {
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(body),
    });
  });
}

async function openDashboard(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

function makeTestContext() {
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

test.describe('Dashboard – Firmware-Update-Notification', () => {
  test.describe('Admin', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Shows a badge on the notification bell when a new firmware version is available.', async ({
      page,
    }) => {
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
      });
      await openDashboard(page, tc.get().token);

      const bell = page.getByRole('button', { name: 'Benachrichtigungen' });
      await expect(bell).toBeVisible();
      await expect(bell.locator('.MuiBadge-dot')).toBeVisible();
    });

    test('Clicking the bell shows the firmware update text with current and latest version.', async ({
      page,
    }) => {
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
      });
      await openDashboard(page, tc.get().token);

      await page.getByRole('button', { name: 'Benachrichtigungen' }).click();
      await expect(page.getByText('Neue Firmware verfügbar')).toBeVisible();
      await expect(
        page.getByText(
          'Es ist eine neue Firmware-Version verfügbar: 1.1.0 (aktuell installiert: 1.0.0).',
        ),
      ).toBeVisible();
    });

    test('Does not show a badge when no firmware update is available.', async ({ page }) => {
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: false,
        currentVersion: '1.0.0',
        latestVersion: '1.0.0',
      });
      await openDashboard(page, tc.get().token);

      const bell = page.getByRole('button', { name: 'Benachrichtigungen' });
      await expect(bell).toBeVisible();
      await expect(bell.locator('.MuiBadge-dot')).not.toBeVisible();
    });

    test('Clicking the bell shows a placeholder text when no firmware update is available.', async ({
      page,
    }) => {
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: false,
        currentVersion: '1.0.0',
        latestVersion: '1.0.0',
      });
      await openDashboard(page, tc.get().token);

      await page.getByRole('button', { name: 'Benachrichtigungen' }).click();
      await expect(page.getByText('Keine neuen Benachrichtigungen')).toBeVisible();
    });

    test('Does not show a badge when the backend has no firmware version on record.', async ({
      page,
    }) => {
      await mockFirmwareUpdate(page, null);
      await openDashboard(page, tc.get().token);

      const bell = page.getByRole('button', { name: 'Benachrichtigungen' });
      await expect(bell).toBeVisible();
      await expect(bell.locator('.MuiBadge-dot')).not.toBeVisible();
    });

    test('Closing the popup hides the firmware update text again.', async ({ page }) => {
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
      });
      await openDashboard(page, tc.get().token);

      await page.getByRole('button', { name: 'Benachrichtigungen' }).click();
      await expect(page.getByText('Neue Firmware verfügbar')).toBeVisible();

      await page.keyboard.press('Escape');
      await expect(page.getByText('Neue Firmware verfügbar')).not.toBeVisible();
    });
  });

  test.describe('Non-Admin roles', () => {
    test('User does not see the notification bell.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.USER);
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
      });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: 'Benachrichtigungen' })).not.toBeVisible();
      await tc.teardown();
    });

    test('FinancialManager does not see the notification bell.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.FINANCIAL_MANAGER);
      await mockFirmwareUpdate(page, {
        newFirmwareAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
      });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: 'Benachrichtigungen' })).not.toBeVisible();
      await tc.teardown();
    });
  });
});
