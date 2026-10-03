import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role, SidebarSettings, TestContext } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();
const MAIL_CHUNK = '**/assets/Mail-*.js';
const CHUNK_RELOAD_KEY = 'chunk-reload-at';
const ERROR_TITLE = 'Hier ist etwas schiefgelaufen';

// The PWA service worker would serve the chunks from its cache and bypass page.route.
test.use({ serviceWorkers: 'block' });

async function openDashboard(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

/** Pretend the app was just reloaded, so a failing chunk is not retried by a reload. */
async function disableChunkReload(page: Page) {
  await page.addInitScript((key) => {
    sessionStorage.setItem(key, String(Date.now()));
  }, CHUNK_RELOAD_KEY);
}

function errorMessage(page: Page) {
  return page.getByRole('alert').filter({ hasText: ERROR_TITLE });
}

test.beforeAll(async () => {
  await backend.init();
});

function makeTestContext() {
  let ctx: TestContext;
  return {
    get: () => ctx,
    setup: async (
      role: Role,
      sidebar: SidebarSettings = { showMail: true, showSepa: false, links: [] },
    ) => {
      const originalSidebar = await backend.getSidebarSettings();

      const user = await backend.createUser(role);
      const token = await backend.loginUser(user.username, user.password);

      createdUserIds.add(user.id);
      await backend.setSidebarSettings(sidebar);

      ctx = { user, token, originalSidebar };
    },
    teardown: async () => {
      await backend.setSidebarSettings(ctx.originalSidebar);
      await backend.deleteUser(ctx.user.id);
      createdUserIds.delete(ctx.user.id);
    },
  };
}

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

test.describe('Error handling – a failing page does not blank the app', () => {
  const tc = makeTestContext();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('A page that fails shows an error message and the sidebar stays usable', async ({
    page,
  }) => {
    await disableChunkReload(page);
    await page.route(MAIL_CHUNK, (route) => route.abort());
    await openDashboard(page, tc.get().token);

    await page.getByRole('button', { name: 'Rundmail' }).click();

    await expect(errorMessage(page)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Erneut versuchen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Neu laden' })).toBeVisible();

    await page.getByRole('button', { name: 'Mitgliederverwaltung', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Mitgliederverwaltung' })).toBeVisible();
    await expect(errorMessage(page)).not.toBeVisible();
  });

  test('"Neu laden" recovers once the page works again', async ({ page }) => {
    await disableChunkReload(page);
    await page.route(MAIL_CHUNK, (route) => route.abort());
    await openDashboard(page, tc.get().token);

    await page.getByRole('button', { name: 'Rundmail' }).click();
    await expect(errorMessage(page)).toBeVisible();

    await page.unroute(MAIL_CHUNK);
    await page.getByRole('button', { name: 'Neu laden' }).click();
    await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });

    await page.getByRole('button', { name: 'Rundmail' }).click();
    await expect(
      page.getByRole('heading', { name: 'Rundmail versenden', exact: true }),
    ).toBeVisible();
  });

  test('On mobile the bottom navigation stays usable after an error', async ({ page }) => {
    await disableChunkReload(page);
    await page.route(MAIL_CHUNK, (route) => route.abort());
    await openDashboard(page, tc.get().token);
    await page.setViewportSize({ width: 375, height: 812 });

    const bottomNav = page.locator('.MuiBottomNavigation-root');
    await bottomNav.getByRole('button', { name: 'Mail' }).click();
    await expect(errorMessage(page)).toBeVisible();

    await bottomNav.getByRole('button', { name: 'Mitglieder' }).click();
    await expect(page.getByRole('heading', { name: 'Mitgliederverwaltung' })).toBeVisible();
  });
});

test.describe('Error handling – missing page file after a deploy', () => {
  const tc = makeTestContext();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('The app reloads once and then loads the current page file', async ({ page }) => {
    await page.route(MAIL_CHUNK, (route) => route.abort(), { times: 1 });
    await openDashboard(page, tc.get().token);

    const reloaded = page.waitForEvent('load');
    await page.getByRole('button', { name: 'Rundmail' }).click();
    await reloaded;
    await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });

    await page.getByRole('button', { name: 'Rundmail' }).click();
    await expect(
      page.getByRole('heading', { name: 'Rundmail versenden', exact: true }),
    ).toBeVisible();
    await expect(errorMessage(page)).not.toBeVisible();
  });

  test('The app does not reload in a loop if the page file stays missing', async ({ page }) => {
    await page.route(MAIL_CHUNK, (route) => route.abort());
    await openDashboard(page, tc.get().token);

    let loads = 0;
    page.on('load', () => loads++);

    const reloaded = page.waitForEvent('load');
    await page.getByRole('button', { name: 'Rundmail' }).click();
    await reloaded;
    await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });

    await page.getByRole('button', { name: 'Rundmail' }).click();
    await expect(errorMessage(page)).toBeVisible();
    expect(loads).toBe(1);
  });
});
