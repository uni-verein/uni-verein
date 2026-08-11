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

test.describe('Receipt category config page', () => {
  const tc = makeTestContextOnlyUser();

  test.beforeEach(async () => {
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test('Open receipt category config page', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Belegkategorien verwalten', exact: true }).click();

    await expect(page.getByRole('heading', { name: 'Belegkategorien verwalten' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Neue Belegkategorie' })).toBeVisible();
  });

  test('Create receipt category', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Belegkategorien verwalten', exact: true }).click();
    await page.getByRole('button', { name: 'Neue Belegkategorie' }).click();

    await expect(page.getByRole('heading', { name: 'Neue Belegkategorie' })).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Name darf nicht leer sein.')).toBeVisible();

    await page.getByRole('textbox', { name: 'Name' }).fill('Büromaterial');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Belegkategorie erfolgreich erstellt.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Büromaterial' })).toBeVisible();
  });

  test('Try to create duplicate receipt category', async ({ page }) => {
    await backend.createReceiptCategory('Duplicate Category');
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Belegkategorien verwalten', exact: true }).click();
    await page.getByRole('button', { name: 'Neue Belegkategorie' }).click();
    await page.getByRole('textbox', { name: 'Name' }).fill('Duplicate Category');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page
        .getByRole('alert')
        .filter({ hasText: 'Belegkategorie kann nicht erstellt werden. Belegkategorie existiert bereits.' }),
    ).toBeVisible();
  });

  test('Delete receipt category', async ({ page }) => {
    await backend.createReceiptCategory('To Delete');
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Belegkategorien verwalten', exact: true }).click();

    await expect(page.getByRole('cell', { name: 'To Delete' })).toBeVisible();
    await page.getByRole('row', { name: 'To Delete' }).getByLabel('Löschen').click();
    await expect(page.getByText('Belegkategorie wirklich löschen?')).toBeVisible();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Belegkategorie erfolgreich gelöscht.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'To Delete' })).not.toBeVisible();
  });

  test('Cannot delete a category assigned to a receipt', async ({ page }) => {
    const category = await backend.createReceiptCategory(`In Use ${Date.now()}`);
    await backend.createTestReceipt(tc.get().token, { categoryId: category.id });

    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Belegkategorien verwalten', exact: true }).click();
    await page.getByRole('row', { name: category.name }).getByLabel('Löschen').click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(
      page
        .getByRole('alert')
        .filter({
          hasText: 'Belegkategorie kann nicht gelöscht werden, da sie einem Beleg zugewiesen ist.',
        })
        .first(),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: category.name })).toBeVisible();
  });
});
