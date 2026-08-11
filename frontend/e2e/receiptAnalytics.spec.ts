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

async function goToReceiptAnalytics(page: Page) {
  await page.getByRole('button', { name: 'Belegauswertung', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Belegauswertung' })).toBeVisible();
}

async function selectYear(page: Page, year: number) {
  await page.getByRole('combobox', { name: 'Jahr' }).click();
  await page.getByRole('option', { name: year.toString(), exact: true }).click();
}

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

test.describe('Receipt analytics page', () => {
  const tc = makeTestContextOnlyUser();

  test.beforeEach(async () => {
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test('Open receipt analytics page as ADMIN', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceiptAnalytics(page);

    await expect(page.getByText('Ausgaben pro Monat', { exact: false })).toBeVisible();
    await expect(page.getByText('Ausgaben pro Kategorie', { exact: false })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Ausgaben pro Jahr' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Jahr' })).toBeVisible();
  });

  test('FINANCIAL_MANAGER can access the page', async ({ page }) => {
    await tc.setup(Role.FINANCIAL_MANAGER);
    await openDashboard(page, tc.get().token);
    await goToReceiptAnalytics(page);

    await expect(page.getByRole('heading', { name: 'Belegauswertung' })).toBeVisible();
  });

  test('USER does not see the Belegauswertung nav item', async ({ page }) => {
    await tc.setup(Role.USER);
    await openDashboard(page, tc.get().token);

    await expect(
      page.getByRole('button', { name: 'Belegauswertung', exact: true }),
    ).not.toBeVisible();
  });

  test('Category breakdown is scoped to the selected year', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    const catA = await backend.createReceiptCategory(`Analytics A ${Date.now()}`);
    const catB = await backend.createReceiptCategory(`Analytics B ${Date.now()}`);
    await backend.createTestReceipt(tc.get().token, {
      amount: '100.00',
      receiptDate: '2025-05-10T00:00:00.000Z',
      categoryId: catA.id,
    });
    await backend.createTestReceipt(tc.get().token, {
      amount: '50.00',
      receiptDate: '2026-05-10T00:00:00.000Z',
      categoryId: catB.id,
    });

    await openDashboard(page, tc.get().token);
    await goToReceiptAnalytics(page);
    const categoryCard = page.locator('.MuiPaper-outlined', { hasText: 'Ausgaben pro Kategorie' });

    await selectYear(page, 2025);
    await expect(categoryCard.getByText(catA.name)).toBeVisible();
    await expect(categoryCard.getByText(catB.name)).not.toBeVisible();
    await expect(page.getByText('Gesamt: 100,00 €').first()).toBeVisible();

    await selectYear(page, 2026);
    await expect(categoryCard.getByText(catB.name)).toBeVisible();
    await expect(categoryCard.getByText(catA.name)).not.toBeVisible();
    await expect(page.getByText('Gesamt: 50,00 €').first()).toBeVisible();
  });

  test('Card titles reflect the selected year', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '10.00',
      receiptDate: '2025-02-10T00:00:00.000Z',
    });

    await openDashboard(page, tc.get().token);
    await goToReceiptAnalytics(page);

    await selectYear(page, 2025);

    await expect(page.getByText('Ausgaben pro Monat (2025)')).toBeVisible();
    await expect(page.getByText('Ausgaben pro Kategorie (2025)')).toBeVisible();
  });

  test('Shows empty state for a year without receipts', async ({ page }) => {
    const currentYear = new Date().getFullYear();
    const lastYear = currentYear - 1;

    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '10.00',
      receiptDate: `${lastYear}-02-10T00:00:00.000Z`,
    });

    await openDashboard(page, tc.get().token);
    await goToReceiptAnalytics(page);

    await expect(page.getByText('Keine Daten vorhanden.').first()).toBeVisible();
    await selectYear(page, lastYear);
    await expect(page.getByText(`Ausgaben pro Monat (${lastYear})`)).toBeVisible();
    await expect(page.getByText('Keine Daten vorhanden.')).toHaveCount(0);
  });
});
