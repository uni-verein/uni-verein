import { test, expect, Page } from '@playwright/test';
import { authenticate, BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

async function openDashboard(page: Page, token: string) {
  await authenticate(page, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

async function goToIncomeCategoryConfig(page: Page) {
  await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
  await page.getByRole('button', { name: 'Einnahmekategorien verwalten', exact: true }).click();
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

test.describe('Income category config page', () => {
  const tc = makeTestContext();

  test.beforeEach(async () => {
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
  });

  test('Open income category config page', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await goToIncomeCategoryConfig(page);

    await expect(page.getByRole('heading', { name: 'Einnahmekategorien verwalten' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Neue Einnahmekategorie' })).toBeVisible();
  });

  test('Create income category validates and shows the new category', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await goToIncomeCategoryConfig(page);
    await page.getByRole('button', { name: 'Neue Einnahmekategorie' }).click();

    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Name darf nicht leer sein.')).toBeVisible();

    await page.getByRole('textbox', { name: 'Name' }).fill('Sponsoring');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(page.getByRole('cell', { name: 'Sponsoring', exact: true })).toBeVisible();
  });

  test('Created category appears as tab on the income page', async ({ page }) => {
    await backend.createIncomeCategory('Zuschüsse');
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einnahmen', exact: true }).first().click();

    await expect(page.getByRole('tab', { name: 'Zuschüsse' })).toBeVisible();
  });

  test('Delete income category', async ({ page }) => {
    await backend.createIncomeCategory('Zuschüsse');
    await openDashboard(page, tc.get().token);
    await goToIncomeCategoryConfig(page);

    await page
      .getByRole('row', { name: /Zuschüsse/ })
      .getByLabel('Löschen')
      .click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(page.getByRole('cell', { name: 'Zuschüsse', exact: true })).not.toBeVisible();
  });

  test('Category in use cannot be deleted', async ({ page }) => {
    const category = await backend.createIncomeCategory('Zuschüsse');
    await backend.createIncome({
      categoryId: category.id,
      payer: 'Stadt',
      amount: 100,
      incomeDate: '2026-03-01',
      paid: false,
    });
    await openDashboard(page, tc.get().token);
    await goToIncomeCategoryConfig(page);

    await page
      .getByRole('row', { name: /Zuschüsse/ })
      .getByLabel('Löschen')
      .click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(
      page
        .getByText(
          'Einnahmekategorie kann nicht gelöscht werden, da ihr Einnahmen zugewiesen sind.',
        )
        .first(),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Zuschüsse', exact: true })).toBeVisible();
  });
});

test.describe('Income category config page as financial manager', () => {
  const tc = makeTestContext();

  test.beforeEach(async () => {
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
    await tc.setup(Role.FINANCIAL_MANAGER);
  });
  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllIncome();
    await backend.deleteAllIncomeCategories();
  });

  test('Create and delete income category', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await goToIncomeCategoryConfig(page);
    await page.getByRole('button', { name: 'Neue Einnahmekategorie' }).click();
    await page.getByRole('textbox', { name: 'Name' }).fill('Zuschüsse');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(page.getByRole('cell', { name: 'Zuschüsse', exact: true })).toBeVisible();

    await page
      .getByRole('row', { name: /Zuschüsse/ })
      .getByLabel('Löschen')
      .click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(page.getByRole('cell', { name: 'Zuschüsse', exact: true })).not.toBeVisible();
  });
});
