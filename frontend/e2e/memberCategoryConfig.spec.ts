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

test.beforeAll(async () => {
  await backend.init();
});

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

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

test.describe('Member category config page', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Open member category config page', async ({ page }) => {
    await backend.deleteTestMemberCategory();
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Mitgliederkategorien verwalten', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Mitgliederkategorien verwalten', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Mitgliederkategorien verwalten' }),
    ).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Kategorie' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Aktionen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Neue Mitgliederkategorie' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'ALUMNI' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'ALUMNI' }).nth(1)).toBeVisible();
    await expect(page.getByRole('cell', { name: 'BOARD_OF_DIRECTORS' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'BOARD_OF_DIRECTORS' }).nth(1)).toBeVisible();
    await expect(page.getByRole('cell', { name: 'STUDENT' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'STUDENT' }).nth(1)).toBeVisible();
    await expect(page.getByRole('cell', { name: 'OTHER' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'OTHER' }).nth(1)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bearbeiten' })).toHaveCount(4);
    await expect(page.getByRole('button', { name: 'Löschen' })).toHaveCount(4);
  });

  test('Create member category', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await backend.deleteTestMemberCategory();
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Mitgliederkategorien verwalten', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Neue Mitgliederkategorie' })).toBeVisible();
    await page.getByRole('button', { name: 'Neue Mitgliederkategorie' }).click();
    await expect(page.getByRole('heading', { name: 'Neue Mitgliederkategorie' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Name' })).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Name darf nicht leer sein.')).toBeVisible();
    await page.getByRole('textbox', { name: 'Name' }).fill('Test');
    await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Speichern' })).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(page.getByText('Name darf nicht leer sein.')).not.toBeVisible();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Mitgliederkategorie erfolgreich erstellt.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Test' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'TEST' }).nth(1)).toBeVisible();

    await expect(
      page.getByRole('row', { name: 'Test TEST' }).getByLabel('Bearbeiten'),
    ).toBeVisible();
    await expect(page.getByRole('row', { name: 'Test TEST' }).getByLabel('Löschen')).toBeVisible();

    await page.getByRole('row', { name: 'Test TEST' }).getByLabel('Löschen').click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await expect(page.getByText('Mitgliederkategorie wirklich löschen?')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Mitgliederkategorie erfolgreich gelöscht.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Test', exact: true })).not.toBeVisible();
  });

  test('Try to create duplicate member category', async ({ page }) => {
    await backend.deleteTestMemberCategory();
    let category = await backend.createTestMemberCategory();
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Mitgliederkategorien verwalten', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Neue Mitgliederkategorie' })).toBeVisible();
    await page.getByRole('button', { name: 'Neue Mitgliederkategorie' }).click();
    await page.getByRole('textbox', { name: 'Name' }).fill(category);
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(
      page.getByText(
        'Mitgliederkategorie kann nicht erstellt werden. Mitgliederkategorie existiert bereits.',
        {
          exact: true,
        },
      ),
    ).toBeVisible();
    await expect(
      page.getByRole('alert').filter({ hasText: /^Mitgliederkategorie existiert bereits\.$/ }),
    ).toBeVisible();

    await page
      .getByRole('row', { name: `${category} ${category.toUpperCase()}` })
      .getByLabel('Löschen')
      .click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
  });

  test('Edit member category', async ({ page }) => {
    await backend.deleteTestMemberCategory();
    let category = await backend.createTestMemberCategory();
    let testValue = 'main';
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Mitgliederkategorien verwalten', exact: true }).click();
    await page
      .getByRole('row', { name: `${category} ${category.toUpperCase()}` })
      .getByLabel('Bearbeiten')
      .click();
    expect(await page.getByRole('textbox', { name: 'Name' }).inputValue()).toEqual(category);
    await page.getByRole('textbox', { name: 'Name' }).fill(testValue);
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(
      page
        .getByRole('alert')
        .filter({ hasText: 'Mitgliederkategorie änderungen erfolgreich gespeichert.' }),
    ).toBeVisible();

    await page
      .getByRole('row', { name: `${testValue} ${testValue.toUpperCase()}` })
      .getByLabel('Löschen')
      .click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
  });
});
