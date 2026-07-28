import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role } from '../src/types';

// Matches the `background.default` values configured in src/App.tsx for each palette mode.
const LIGHT_BODY_BG = 'rgb(248, 250, 252)';
const DARK_BODY_BG = 'rgb(18, 18, 18)';

const backend = new BackendClient();

async function bodyBackground(page: Page) {
  return page.evaluate(() => getComputedStyle(document.body).backgroundColor);
}

async function themeModeStorage(page: Page) {
  return page.evaluate(() => localStorage.getItem('themeMode'));
}

async function openDashboard(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByRole('button', { name: /Abmelden/i })).toBeVisible({ timeout: 8000 });
}

test.beforeAll(async () => {
  await backend.init();
});

test.describe('Dark mode: system preference (login page)', () => {
  test('defaults to dark styling when the OS prefers dark', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto(APP_BASE);
    await expect(page.getByRole('heading', { name: 'Vereinsverwaltung' })).toBeVisible();
    expect(await bodyBackground(page)).toBe(DARK_BODY_BG);
  });

  test('defaults to light styling when the OS prefers light', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto(APP_BASE);
    await expect(page.getByRole('heading', { name: 'Vereinsverwaltung' })).toBeVisible();
    expect(await bodyBackground(page)).toBe(LIGHT_BODY_BG);
  });

  test('no preference is stored until the user makes a manual choice', async ({ page }) => {
    await page.goto(APP_BASE);
    await expect(page.getByRole('heading', { name: 'Vereinsverwaltung' })).toBeVisible();
    expect(await themeModeStorage(page)).toBeNull();
  });
});

test.describe('Dark mode: manual toggle', () => {
  const createdUserIds = new Set<string>();

  test.afterEach(async () => {
    for (const id of createdUserIds) {
      await backend.deleteUser(id);
    }
    createdUserIds.clear();
  });

  test('toggle cycles System -> Light -> Dark -> System and persists each step', async ({
    page,
  }) => {
    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    await openDashboard(page, token);

    const toggle = page.getByRole('button', { name: 'System', exact: true });
    await expect(toggle).toBeVisible();
    expect(await themeModeStorage(page)).toBeNull();

    await toggle.click();
    await expect(page.getByRole('button', { name: 'Hell', exact: true })).toBeVisible();
    expect(await themeModeStorage(page)).toBe('light');
    expect(await bodyBackground(page)).toBe(LIGHT_BODY_BG);

    await page.getByRole('button', { name: 'Hell', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Dunkel', exact: true })).toBeVisible();
    expect(await themeModeStorage(page)).toBe('dark');
    expect(await bodyBackground(page)).toBe(DARK_BODY_BG);

    await page.getByRole('button', { name: 'Dunkel', exact: true }).click();
    await expect(page.getByRole('button', { name: 'System', exact: true })).toBeVisible();
    expect(await themeModeStorage(page)).toBe('system');
  });

  test('manual dark selection overrides a light system preference and survives a reload', async ({
    page,
  }) => {
    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    await page.emulateMedia({ colorScheme: 'light' });
    await openDashboard(page, token);

    await page.getByRole('button', { name: 'System', exact: true }).click();
    await page.getByRole('button', { name: 'Hell', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Dunkel', exact: true })).toBeVisible();
    expect(await bodyBackground(page)).toBe(DARK_BODY_BG);

    await page.reload();
    await expect(page.getByRole('button', { name: 'Dunkel', exact: true })).toBeVisible();
    expect(await bodyBackground(page)).toBe(DARK_BODY_BG);
  });

  test('manual selection also applies to the login page after logging out', async ({ page }) => {
    const user = await backend.createUser(Role.ADMIN);
    createdUserIds.add(user.id);
    const token = await backend.loginUser(user.username, user.password);

    await page.emulateMedia({ colorScheme: 'light' });
    await openDashboard(page, token);

    await page.getByRole('button', { name: 'System', exact: true }).click();
    await page.getByRole('button', { name: 'Hell', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Dunkel', exact: true })).toBeVisible();

    await page.getByRole('button', { name: /Abmelden/i }).click();
    await expect(page.getByRole('heading', { name: 'Vereinsverwaltung' })).toBeVisible();
    expect(await bodyBackground(page)).toBe(DARK_BODY_BG);
  });
});
