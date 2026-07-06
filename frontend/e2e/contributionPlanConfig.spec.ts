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

test.describe('Contribution plan config page', () => {
  test.describe('Contribution plan config', () => {
    const tc = makeTestContextOnlyUser();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Open contribution plan config page', async ({ page }) => {
      await backend.deleteAllContributionPlans();
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await expect(
        page.getByRole('button', { name: 'Beitragstarifverwaltung', exact: true }),
      ).toBeVisible();
      await page.getByRole('button', { name: 'Beitragstarifverwaltung', exact: true }).click();
      await expect(page.getByRole('heading', { name: 'Beitragstarifverwaltung' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Betrag' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Interval' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Aktionen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Neuer Beitragstarif' })).toBeVisible();
      await expect(page.getByRole('cell', { name: 'Default', exact: true })).toBeVisible();
      await expect(page.getByRole('cell', { name: '12', exact: true })).toBeVisible();
      await expect(page.getByRole('cell', { name: 'Jährlich' }).nth(0)).toBeVisible();
      await expect(page.getByRole('button', { name: 'Bearbeiten' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    });

    test('Create contribution plan', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await backend.deleteAllContributionPlans();
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await page.getByRole('button', { name: 'Beitragstarifverwaltung', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Neuer Beitragstarif' })).toBeVisible();
      await page.getByRole('button', { name: 'Neuer Beitragstarif' }).click();
      await expect(page.getByRole('heading', { name: 'Neuer Beitragstarif' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Name' })).toBeVisible();
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(page.getByText('Name darf nicht leer sein.')).toBeVisible();
      await expect(page.getByText('Beitrag muss größer als 0')).toBeVisible();
      await expect(page.getByText('Intervall muss ausgewählt werden.')).toBeVisible();
      await page.getByRole('textbox', { name: 'Name' }).fill('3000');
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(page.getByText('Name darf nicht leer sein.')).not.toBeVisible();

      await expect(page.getByRole('textbox', { name: 'Betrag (€)' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Betrag (€)' }).fill(`3000`);
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(page.getByText('Beitrag muss größer als 0 sein.')).not.toBeVisible();

      await expect(page.getByRole('combobox')).toBeVisible();
      await page.getByRole('combobox').click();
      await expect(page.getByRole('option', { name: 'Monatlich' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Jährlich' })).toBeVisible();
      await page.getByRole('option', { name: 'Monatlich' }).click();
      await expect(page.getByText('Intervall muss ausgewählt werden.')).not.toBeVisible();

      await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Speichern' })).toBeVisible();
      await page.getByRole('button', { name: 'Speichern' }).click();

      await expect(
        page.getByRole('alert').filter({ hasText: 'Beitragsplan erfolgreich erstellt.' }),
      ).toBeVisible();
      await expect(page.getByRole('cell', { name: '3000' }).first()).toBeVisible();
      await expect(page.getByRole('cell', { name: '3000' }).nth(1)).toBeVisible();

      await expect(
        page.getByRole('row', { name: '3000 3000 Monatlich' }).getByLabel('Bearbeiten'),
      ).toBeVisible();
      await expect(
        page.getByRole('row', { name: '3000 3000 Monatlich' }).getByLabel('Löschen'),
      ).toBeVisible();

      await page.getByRole('row', { name: '3000 3000 Monatlich' }).getByLabel('Löschen').click();
      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await expect(page.getByText('Beitragstarif wirklich löschen?')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();

      await expect(
        page.getByRole('alert').filter({ hasText: 'Beitragsplan erfolgreich gelöscht.' }),
      ).toBeVisible();
      await expect(page.getByRole('cell', { name: '3000', exact: true })).not.toBeVisible();
    });

    test('Try to create duplicate contribution plan', async ({ page }) => {
      await backend.deleteAllContributionPlans();
      let plan = await backend.createContributionPlan();

      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await page.getByRole('button', { name: 'Beitragstarifverwaltung', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Neuer Beitragstarif' })).toBeVisible();
      await page.getByRole('button', { name: 'Neuer Beitragstarif' }).click();
      await page.getByRole('textbox', { name: 'Name' }).fill(plan);
      await page.getByRole('textbox', { name: 'Betrag (€)' }).fill(`8000`);
      await page.getByRole('combobox').click();
      await page.getByRole('option', { name: 'Monatlich' }).click();
      await page.getByRole('button', { name: 'Speichern' }).click();

      await expect(
        page.getByText('Beitragsplan kann nicht erstellt werden. Beitragsplan existiert bereits.', {
          exact: true,
        }),
      ).toBeVisible();
      await expect(
        page.getByRole('alert').filter({ hasText: /^Beitragsplan existiert bereits\.$/ }),
      ).toBeVisible();

      await page
        .getByRole('row', { name: `${plan} 8000` })
        .getByLabel('Löschen')
        .click();
      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();
    });

    test('Edit contribution plan', async ({ page }) => {
      await backend.deleteAllContributionPlans();
      let plan = await backend.createContributionPlan();
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await page.getByRole('button', { name: 'Beitragstarifverwaltung', exact: true }).click();
      await page
        .getByRole('row', { name: `${plan} 8000` })
        .getByLabel('Bearbeiten')
        .click();
      expect(await page.getByRole('textbox', { name: 'Name' }).inputValue()).toEqual(plan);
      await expect(page.getByRole('textbox', { name: 'Betrag (€)' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Betrag (€)' }).fill(`5000`);
      await page.getByRole('combobox').click();
      await page.getByRole('option', { name: 'Jährlich' }).click();
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(
        page
          .getByRole('alert')
          .filter({ hasText: 'Beitragsplanänderungen erfolgreich gespeichert.' }),
      ).toBeVisible();

      await page
        .getByRole('row', { name: `${plan} 5000` })
        .getByLabel('Löschen')
        .click();
      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();
    });
  });
});
