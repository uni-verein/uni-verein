import { test, expect, Page } from '@playwright/test';
import { authenticate, BackendClient, APP_BASE } from './BackendClient';
import { IncomeCategory, Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

async function openDashboard(page: Page, token: string) {
  await authenticate(page, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

async function goToIncome(page: Page) {
  await page.getByRole('button', { name: 'Einnahmen', exact: true }).first().click();
  await expect(page.getByRole('heading', { name: 'Einnahmen', level: 5 })).toBeVisible();
}

function thisYear(month: string, day = '15') {
  return `${new Date().getFullYear()}-${month}-${day}`;
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

test.describe('Income page', () => {
  const tc = makeTestContext();
  let donations: IncomeCategory;

  test.beforeEach(async () => {
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
    donations = await backend.createIncomeCategory('Spenden');
  });
  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
  });

  test('Shows fixed tabs, configured categories and no SEPA export button', async ({ page }) => {
    await backend.createIncomeCategory('Sponsoring');
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);

    await expect(page.getByRole('tab', { name: 'Alle' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Mitgliedsbeiträge' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Spenden' })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Sponsoring' })).toBeVisible();
    await expect(page.getByRole('main').getByRole('link', { name: 'SEPA Export' })).toHaveCount(0);
    await expect(page.getByRole('main').getByRole('button', { name: 'SEPA Export' })).toHaveCount(
      0,
    );
  });

  test('Membership fees tab offers the membership status filter', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);

    await expect(page.getByRole('combobox', { name: 'Mitgliedsstatus' })).toHaveCount(0);
    await page.getByRole('tab', { name: 'Mitgliedsbeiträge' }).click();
    await page.getByRole('combobox', { name: 'Mitgliedsstatus' }).click();
    await expect(page.getByRole('option', { name: 'Fördermitglied' })).toBeVisible();
    await expect(page.getByRole('option', { name: 'Mitglied', exact: true })).toBeVisible();
  });

  test('Dialog asks for a category first when none exists', async ({ page }) => {
    await backend.deleteAllIncomeCategories();
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('button', { name: 'Einnahme erfassen' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Es gibt noch keine Einnahmekategorie.')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Speichern' })).toBeDisabled();
  });

  test('Create donation via dialog', async ({ page }) => {
    await tc.setup(Role.FINANCIAL_MANAGER);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('button', { name: 'Einnahme erfassen' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Speichern' }).click();
    await expect(dialog.getByText('Zahler / Quelle ist erforderlich.')).toBeVisible();

    await dialog.getByRole('textbox', { name: 'Zahler / Quelle' }).fill('Familie Krause');
    await dialog.getByRole('spinbutton', { name: 'Betrag (€)' }).fill('250');
    await dialog.getByRole('button', { name: 'Speichern' }).click();

    await expect(dialog).not.toBeVisible();
    await page.getByRole('tab', { name: 'Spenden' }).click();
    await expect(page.getByRole('cell', { name: 'Familie Krause Manuell erfasst' })).toBeVisible();
    await expect(page.getByRole('cell', { name: '250,00 €' })).toBeVisible();
  });

  test('Create income in a configured category', async ({ page }) => {
    await backend.createIncomeCategory('Sponsoring');
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('button', { name: 'Einnahme erfassen' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('combobox', { name: 'Art' }).click();
    await page.getByRole('option', { name: 'Sponsoring' }).click();
    await dialog.getByRole('textbox', { name: 'Zahler / Quelle' }).fill('Bäckerei Sonnenschein');
    await dialog.getByRole('spinbutton', { name: 'Betrag (€)' }).fill('1500');
    await dialog.getByRole('button', { name: 'Speichern' }).click();
    await expect(dialog).not.toBeVisible();

    await page.getByRole('tab', { name: 'Sponsoring' }).click();
    await expect(page.getByRole('cell', { name: /Bäckerei Sonnenschein/ })).toBeVisible();
    await page.getByRole('tab', { name: 'Spenden' }).click();
    await expect(page.getByRole('cell', { name: /Bäckerei Sonnenschein/ })).toHaveCount(0);
  });

  test('Mark income as paid and open again', async ({ page }) => {
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Spendenbox',
      amount: 86.5,
      incomeDate: thisYear('03'),
      paid: false,
    });
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('tab', { name: 'Spenden' }).click();

    const row = page.getByRole('row', { name: /Spendenbox/ });
    await expect(row.getByText('Offen', { exact: true })).toBeVisible();
    await row.getByRole('button', { name: 'Zahlung erhalten' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Zahlung buchen' })).toBeVisible();
    await dialog.getByRole('button', { name: 'Als bezahlt markieren' }).click();
    await expect(row.getByText('Bezahlt', { exact: true })).toBeVisible();
    await expect(row.getByText(/^E-\d{4}-\d{4}/)).toBeVisible();
    await row.getByRole('button', { name: 'Zahlung offen' }).click();
    await expect(row.getByText('Offen', { exact: true })).toBeVisible();
  });

  test('Create acknowledgement for a paid income and lock it afterwards', async ({ page }) => {
    const category = await backend.createIncomeCategory(`Quittung ${Date.now()}`);
    await backend.createIncome({
      categoryId: category.id,
      payer: 'Familie Krause',
      amount: 250,
      incomeDate: thisYear('03'),
      paid: true,
    });
    await tc.setup(Role.FINANCIAL_MANAGER);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('tab', { name: category.name }).click();

    const row = page.getByRole('row', { name: /Familie Krause/ });
    const downloadPromise = page.waitForEvent('download');
    await row.getByRole('button', { name: 'Quittung erzeugen' }).click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toMatch(/^Quittung-\d{4}-\d{4}\.pdf$/);
    await expect(row.getByText(/Quittung \d{4}-\d{4}/)).toBeVisible();
    await expect(row.getByRole('button', { name: 'Zahlung offen' })).toBeDisabled();
    await expect(row.getByLabel('Bearbeiten')).toHaveCount(0);
  });

  test('Open income has no acknowledgement button', async ({ page }) => {
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Spendenbox',
      amount: 20,
      incomeDate: thisYear('03'),
      paid: false,
    });
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('tab', { name: 'Spenden' }).click();

    await expect(page.getByRole('row', { name: /Spendenbox/ })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Quittung erzeugen' })).toHaveCount(0);
  });

  test('Delete open income', async ({ page }) => {
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Spendenbox',
      amount: 20,
      incomeDate: thisYear('03'),
      paid: false,
    });
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);
    await page.getByRole('tab', { name: 'Spenden' }).click();

    await page
      .getByRole('row', { name: /Spendenbox/ })
      .getByLabel('Löschen')
      .click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(page.getByRole('row', { name: /Spendenbox/ })).toHaveCount(0);
  });

  test('Status filter only lists matching entries', async ({ page }) => {
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Offene Spende',
      amount: 10,
      incomeDate: thisYear('03'),
      paid: false,
    });
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Erhaltene Spende',
      amount: 20,
      incomeDate: thisYear('03'),
      paid: true,
    });
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);

    await page.getByRole('combobox', { name: 'Beitragsstatus' }).click();
    await page.getByRole('option', { name: 'Offen' }).click();

    await expect(page.getByRole('row', { name: /Offene Spende/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Erhaltene Spende/ })).toHaveCount(0);
  });

  test('Date range defaults to the current year and can be widened', async ({ page }) => {
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Alte Spende',
      amount: 10,
      incomeDate: '2020-05-05',
      paid: true,
    });
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Aktuelle Spende',
      amount: 20,
      incomeDate: thisYear('03'),
      paid: true,
    });
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);

    await expect(page.getByRole('row', { name: /Aktuelle Spende/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Alte Spende/ })).toHaveCount(0);

    await page.getByRole('button', { name: 'Alle Zeiten' }).click();

    await expect(page.getByRole('row', { name: /Alte Spende/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Aktuelle Spende/ })).toBeVisible();
  });

  test('Year dropdown switches between years and all years', async ({ page }) => {
    const lastYear = new Date().getFullYear() - 1;
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Vorjahresspende',
      amount: 10,
      incomeDate: `${lastYear}-06-01`,
      paid: true,
    });
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Aktuelle Spende',
      amount: 20,
      incomeDate: thisYear('03'),
      paid: true,
    });
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);

    await expect(page.getByRole('row', { name: /Aktuelle Spende/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Vorjahresspende/ })).toHaveCount(0);

    await page.getByRole('combobox', { name: 'Jahr' }).click();
    await page.getByRole('option', { name: String(lastYear) }).click();
    await expect(page.getByRole('row', { name: /Vorjahresspende/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Aktuelle Spende/ })).toHaveCount(0);

    await page.getByRole('combobox', { name: 'Jahr' }).click();
    await page.getByRole('option', { name: 'Alle Jahre' }).click();
    await expect(page.getByRole('row', { name: /Vorjahresspende/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /Aktuelle Spende/ })).toBeVisible();
  });

  test('Regular user can read but not manage income', async ({ page }) => {
    await backend.createIncome({
      categoryId: donations.id,
      payer: 'Spendenbox',
      amount: 20,
      incomeDate: thisYear('03'),
      paid: false,
    });
    await tc.setup(Role.USER);
    await openDashboard(page, tc.get().token);
    await goToIncome(page);

    await expect(page.getByRole('row', { name: /Spendenbox/ })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Einnahme erfassen' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Zahlung erhalten' })).toHaveCount(0);
    await expect(page.getByRole('columnheader', { name: 'Zahlung' })).toHaveCount(0);
    await expect(page.getByRole('columnheader', { name: 'Aktion' })).toHaveCount(0);
  });
});
