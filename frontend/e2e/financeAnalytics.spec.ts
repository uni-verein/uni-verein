import { test, expect, Page } from '@playwright/test';
import { authenticate, BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();
const currentYear = new Date().getFullYear();
const lastYear = currentYear - 1;

async function createPaidReceipt(token: string, overrides: Record<string, string>) {
  const receipt = await backend.createTestReceipt(token, overrides);
  await backend.payReceipt(token, receipt.id, 'CASH', overrides.receiptDate.slice(0, 10));
}

async function openDashboard(page: Page, token: string) {
  await authenticate(page, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

async function goToFinanceAnalytics(page: Page) {
  await page.getByRole('button', { name: 'Finanzauswertung', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Finanzauswertung' })).toBeVisible();
}

async function openTab(page: Page, name: string) {
  await page.getByRole('tab', { name, exact: true }).click();
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

test.describe('Finance analytics page', () => {
  const tc = makeTestContextOnlyUser();

  async function cleanup() {
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
  }

  test.beforeEach(cleanup);

  test.afterEach(async () => {
    await tc.teardown();
    await cleanup();
  });

  test('Open finance analytics page as ADMIN', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);

    await expect(page.getByRole('combobox', { name: 'Jahr' })).toBeVisible();
    for (const name of ['Übersicht', 'Einnahmen', 'Ausgaben', 'Kategorien']) {
      await expect(page.getByRole('tab', { name, exact: true })).toBeVisible();
    }
    await expect(page.getByText('Saldo', { exact: true })).toBeVisible();
    await expect(page.getByText(`Einnahmen und Ausgaben pro Monat (${currentYear})`)).toBeVisible();
    await expect(page.getByText('Einnahmen und Ausgaben pro Jahr')).toBeVisible();
  });

  test('FINANCIAL_MANAGER can access the page', async ({ page }) => {
    await tc.setup(Role.FINANCIAL_MANAGER);
    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);
  });

  test('Export the EÜR as an Excel file', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'EÜR exportieren' }).click(),
    ]);

    expect(download.suggestedFilename()).toBe(`EUER_${currentYear}.xlsx`);
    await expect(page.getByText(`EÜR ${currentYear} wurde exportiert.`)).toBeVisible();
  });

  test('USER does not see the Finanzauswertung nav item', async ({ page }) => {
    await tc.setup(Role.USER);
    await openDashboard(page, tc.get().token);

    await expect(
      page.getByRole('button', { name: 'Finanzauswertung', exact: true }),
    ).not.toBeVisible();
  });

  test('Balance combines paid income and receipts', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    const incomeCategory = await backend.createIncomeCategory('Spenden');
    await backend.createIncome({
      categoryId: incomeCategory.id,
      payer: 'Spendenbox',
      amount: 200,
      incomeDate: `${currentYear}-03-15`,
      paid: true,
    });
    await backend.createIncome({
      categoryId: incomeCategory.id,
      payer: 'Offene Spende',
      amount: 1000,
      incomeDate: `${currentYear}-03-16`,
      paid: false,
    });
    await createPaidReceipt(tc.get().token, {
      amount: '50.00',
      receiptDate: `${currentYear}-03-10T00:00:00.000Z`,
    });

    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);

    await expect(page.getByText(`Überschuss von 150,00 € im Jahr ${currentYear}.`)).toBeVisible();
    await expect(page.getByText('200,00 €', { exact: true })).toBeVisible();
    await expect(page.getByText('50,00 €', { exact: true })).toBeVisible();
    await expect(page.getByText('150,00 €', { exact: true }).first()).toBeVisible();
  });

  test('Categories are scoped to the selected year', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    const incomeA = await backend.createIncomeCategory(`Fin Einnahme A ${Date.now()}`);
    const receiptB = await backend.createReceiptCategory(`Fin Beleg B ${Date.now()}`);
    await backend.createIncome({
      categoryId: incomeA.id,
      payer: 'Spender',
      amount: 100,
      incomeDate: `${lastYear}-05-10`,
      paid: true,
    });
    await createPaidReceipt(tc.get().token, {
      amount: '50.00',
      receiptDate: `${currentYear}-05-10T00:00:00.000Z`,
      categoryId: receiptB.id,
    });

    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);
    await openTab(page, 'Kategorien');

    await selectYear(page, lastYear);
    await expect(page.getByRole('cell', { name: incomeA.name })).toBeVisible();
    await expect(page.getByRole('cell', { name: receiptB.name })).not.toBeVisible();

    await selectYear(page, currentYear);
    await expect(page.getByRole('cell', { name: receiptB.name })).toBeVisible();
    await expect(page.getByRole('cell', { name: incomeA.name })).not.toBeVisible();
  });

  test('Card titles reflect the selected year', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await createPaidReceipt(tc.get().token, {
      amount: '10.00',
      receiptDate: `${lastYear}-02-10T00:00:00.000Z`,
    });

    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);
    await selectYear(page, lastYear);

    await openTab(page, 'Einnahmen');
    await expect(page.getByText(`Einnahmen pro Monat (${lastYear})`)).toBeVisible();
    await expect(page.getByText(`Einnahmen pro Kategorie (${lastYear})`)).toBeVisible();

    await openTab(page, 'Ausgaben');
    await expect(page.getByText(`Ausgaben pro Monat (${lastYear})`)).toBeVisible();
    await expect(page.getByText(`Ausgaben pro Kategorie (${lastYear})`)).toBeVisible();
  });

  test('Shows empty state for a year without data', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await createPaidReceipt(tc.get().token, {
      amount: '10.00',
      receiptDate: `${lastYear}-02-10T00:00:00.000Z`,
    });

    await openDashboard(page, tc.get().token);
    await goToFinanceAnalytics(page);
    await openTab(page, 'Ausgaben');

    await expect(page.getByText('Keine Daten vorhanden.').first()).toBeVisible();
    await selectYear(page, lastYear);
    await expect(page.getByText('Keine Daten vorhanden.')).toHaveCount(0);
  });
});
