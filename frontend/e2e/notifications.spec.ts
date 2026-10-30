import { test, expect, Page, Route } from '@playwright/test';
import { authenticate, BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

const FIRMWARE_UPDATE_ROUTE = '**/api/notifications/firmware-update';

// The PWA service worker would forward requests to the real backend and bypass page.route.
test.use({ serviceWorkers: 'block' });

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
  await authenticate(page, token);

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

  test.describe('Delete notifications', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.USER);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    function mockNotificationList(page: Page) {
      let items = [
        { id: 'n1', title: 'Gelesener Termin', read: true },
        { id: 'n2', title: 'Ungelesener Termin', read: false },
      ].map((n) => ({
        id: n.id,
        type: 'CALENDAR_INVITATION',
        eventId: null,
        eventTitle: n.title,
        eventStart: null,
        actorUsername: 'anna',
        details: '',
        createdAt: '2026-10-01T10:00:00Z',
        read: n.read,
      }));
      const deleted: string[] = [];

      const body = () => ({
        items,
        total: items.length,
        unread: items.filter((n) => !n.read).length,
      });

      return {
        deleted,
        install: async () => {
          await page.route('**/api/notifications/firmware-update', (route) =>
            route.fulfill({ status: 204 }),
          );
          await page.route('**/api/notifications/unread-count', (route) =>
            route.fulfill({ status: 200, json: { unread: body().unread } }),
          );
          await page.route('**/api/notifications?*', (route) =>
            route.fulfill({ status: 200, json: body() }),
          );
          await page.route('**/api/notifications/read', (route) => {
            if (route.request().method() !== 'DELETE') return route.fallback();
            deleted.push('read');
            items = items.filter((n) => !n.read);
            return route.fulfill({ status: 204 });
          });
          await page.route('**/api/notifications/n*', (route) => {
            if (route.request().method() !== 'DELETE') return route.fallback();
            const id = route.request().url().split('/').pop()!;
            deleted.push(id);
            items = items.filter((n) => n.id !== id);
            return route.fulfill({ status: 204 });
          });
        },
      };
    }

    test('Deleting a single notification removes it from the list.', async ({ page }) => {
      const mock = mockNotificationList(page);
      await mock.install();
      await openDashboard(page, tc.get().token);

      await page.getByRole('button', { name: 'Benachrichtigungen' }).click();
      await expect(page.getByText('„Ungelesener Termin“')).toBeVisible();

      await page.getByRole('button', { name: 'Löschen', exact: true }).first().click();

      await expect.poll(() => mock.deleted).toEqual(['n1']);
      await expect(page.getByText('„Gelesener Termin“')).not.toBeVisible();
      await expect(page.getByText('„Ungelesener Termin“')).toBeVisible();
    });

    test('Delete read removes only read notifications.', async ({ page }) => {
      const mock = mockNotificationList(page);
      await mock.install();
      await openDashboard(page, tc.get().token);

      await page.getByRole('button', { name: 'Benachrichtigungen' }).click();
      await page.getByRole('button', { name: 'Gelesene löschen' }).click();

      await expect.poll(() => mock.deleted).toEqual(['read']);
      await expect(page.getByText('„Gelesener Termin“')).not.toBeVisible();
      await expect(page.getByText('„Ungelesener Termin“')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Gelesene löschen' })).not.toBeVisible();
    });
  });

  test.describe('Non-Admin roles', () => {
    for (const role of [Role.USER, Role.FINANCIAL_MANAGER]) {
      test(`${role} sees the notification bell but no firmware update.`, async ({ page }) => {
        const tc = makeTestContext();
        await tc.setup(role);
        await mockFirmwareUpdate(page, {
          newFirmwareAvailable: true,
          currentVersion: '1.0.0',
          latestVersion: '1.1.0',
        });
        await openDashboard(page, tc.get().token);

        const bell = page.getByRole('button', { name: 'Benachrichtigungen' });
        await expect(bell).toBeVisible();
        await expect(bell.locator('.MuiBadge-dot')).not.toBeVisible();

        await bell.click();
        await expect(page.getByText('Keine neuen Benachrichtigungen')).toBeVisible();
        await expect(page.getByText('Neue Firmware verfügbar')).not.toBeVisible();
        await tc.teardown();
      });
    }
  });
});
