import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

async function openDashboard(
  page: Page,
  token: string,
  pageName: string | undefined = undefined,
): Promise<any> {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText(pageName ?? 'Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
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

test.describe('Link – Configure', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Open link config page', async ({ page }) => {
    await backend.deleteLinkSettings();
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Externe Links konfigurieren', exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText(
        'Fügen Sie hier verschiedene Links hinzu, die im Menü angezeigt werden sollen.',
      ),
    ).toBeVisible();

    await expect(page.getByRole('columnheader', { name: 'Link' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Symbol' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Aktionen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Link hinzufügen' })).toBeVisible();

    await backend.deleteLinkSettings();
  });

  test('Create link config', async ({ page }) => {
    await backend.deleteLinkSettings();
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Link hinzufügen' })).toBeVisible();
    await page.getByRole('button', { name: 'Link hinzufügen' }).click();

    await expect(page.getByRole('heading', { name: 'Link hinzufügen' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Linkadresse' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Linkadresse' }).fill('http://localhost:8080');
    await expect(page.getByRole('textbox', { name: 'Anzeigename' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Anzeigename' }).fill('Dateien');
    await expect(page.getByRole('textbox', { name: 'Symbol' })).toBeVisible();
    await page.getByRole('button').filter({ hasText: /^$/ }).click();
    await expect(page.getByRole('textbox', { name: 'Symbol suchen...' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Symbol suchen...' }).fill('AddCardTwoTone');
    await expect(page.getByRole('button', { name: 'AddCardTwoTone' })).toBeVisible({
      timeout: 10000,
    });
    await page.getByRole('button', { name: 'AddCardTwoTone' }).click();
    await expect(page.getByLabel('Symbol auswählen').getByText('AddCardTwoTone')).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByRole('button', { name: 'Übernehmen' })).toBeVisible();
    await page.getByRole('button', { name: 'Übernehmen' }).click();
    await expect(page.getByRole('button', { name: 'Speichern' })).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Link erfolgreich hinzugefügt.' }),
    ).toBeVisible();

    await expect(page.getByRole('cell', { name: 'http://localhost:' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Dateien' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'AddCardTwoTone' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bearbeiten' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await backend.deleteLinkSettings();
  });

  test('Update link config', async ({ page }) => {
    await backend.deleteLinkSettings();
    await backend.createLinkSettings({
      id: null,
      link: 'http://localhost:8085',
      name: 'Dateien',
      icon: '',
    });
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }).click();
    await page.getByRole('button', { name: 'Bearbeiten' }).click();

    await page.getByRole('textbox', { name: 'Linkadresse' }).fill('http://localhost:8080');
    await page.getByRole('textbox', { name: 'Anzeigename' }).fill('Test');
    await page.getByRole('button').filter({ hasText: /^$/ }).click();
    await expect(page.getByRole('textbox', { name: 'Symbol suchen...' })).toBeVisible({
      timeout: 10000,
    });
    await page.getByRole('textbox', { name: 'Symbol suchen...' }).fill('AddCardTwoTone');
    await expect(page.getByRole('button', { name: 'AddCardTwoTone' })).toBeVisible({
      timeout: 10000,
    });
    await page.getByRole('button', { name: 'AddCardTwoTone' }).click();
    await expect(page.getByLabel('Symbol auswählen').getByText('AddCardTwoTone')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Übernehmen' })).toBeVisible();
    await page.getByRole('button', { name: 'Übernehmen' }).click();
    await expect(page.getByRole('button', { name: 'Speichern' })).toBeVisible();
    await page.getByRole('button', { name: 'Speichern' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Speichern erfolgreich.' }),
    ).toBeVisible();

    await expect(page.getByRole('cell', { name: 'http://localhost:' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Test' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'AddCardTwoTone' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bearbeiten' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await backend.deleteLinkSettings();
  });

  test('Delete link url', async ({ page }) => {
    await backend.updateLinkSettings();
    await openDashboard(page, tc.get().token);

    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await expect(
      page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Externe Links konfigurieren', exact: true }).click();

    await expect(page.getByRole('cell', { name: 'http://localhost:' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Dateien' })).toBeVisible();
    await expect(page.getByRole('cell', { name: '—' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bearbeiten' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();

    await page.getByRole('button', { name: 'Löschen' }).click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await expect(page.getByText('Link wirklich löschen?')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await page.getByRole('button', { name: 'Löschen' }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'Löschen erfolgreich.' })).toBeVisible();
    await expect(page.getByText('Keine Links gefunden.')).toBeVisible();
  });

  test('Show working link folder on menu', async ({ page }) => {
    await backend.updateLinkSettings();
    await openDashboard(page, tc.get().token);
    await expect(page.getByRole('link', { name: 'Dateien' })).toBeVisible();
    const [newPage] = await Promise.all([
      page.context().waitForEvent('page'),
      page.getByRole('link', { name: 'Dateien' }).click(),
    ]);
    await newPage.waitForLoadState('domcontentloaded');
    expect(newPage.url()).toBe('http://localhost:8080/');
    await backend.deleteLinkSettings();
  });
});
