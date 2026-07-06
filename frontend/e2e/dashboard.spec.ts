import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import { Role, SidebarSettings, TestContext } from '../src/types';

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

function makeTestContext() {
  let ctx: TestContext;
  return {
    get: () => ctx,
    setup: async (
      role: Role,
      sidebar: SidebarSettings = { showMail: true, showSepa: true, links: [] },
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

test.describe('Dashboard – Sidebar-Texte and Navigation', () => {
  test.describe('Branding', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test("Show 'Vereinsverwaltung' if pageName is empty", async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByText('Vereinsverwaltung')).toBeVisible();
    });

    test("Show 'V'-Badge in Drawer-Header", async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.locator('.MuiDrawer-root').getByText('V', { exact: true })).toBeVisible();
    });
  });

  test.describe('AppBar', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Show logout button', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /Abmelden/i })).toBeVisible();
    });
  });

  test.describe('User-Profile (Sidebar-Footer)', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Displays the username from the token.', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByText(tc.get().user.username)).toBeVisible();
    });

    test("Shows the user's role.", async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByText('ADMIN', { exact: true })).toBeVisible();
    });

    test('The avatar displays the first letter of the username.', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      const initial = tc.get().user.username.charAt(0).toUpperCase();
      await expect(page.locator('.MuiAvatar-root').getByText(initial)).toBeVisible();
    });
  });

  test.describe('Sidebar Visibility Based on Settings', () => {
    test('Bulk email visible when showMail=true', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: true, showSepa: true, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /Rundmail/i })).toBeVisible();
      await tc.teardown();
    });

    test('Bulk email hidden when showMail=false', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: false, showSepa: true, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /Rundmail/i })).not.toBeVisible();
      await tc.teardown();
    });

    test('SEPA visible when showSepa=true', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: true, showSepa: true, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^SEPA Export$/i })).toBeVisible();
      await tc.teardown();
    });

    test('SEPA hidden when showSepa=false', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: true, showSepa: false, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^SEPA Export$/i })).not.toBeVisible();
      await tc.teardown();
    });

    test('Contributions hidden when showSepa=false', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: true, showSepa: false, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /Beiträge/i })).not.toBeVisible();
      await tc.teardown();
    });

    test('File link visible and href correct when dataLink is set.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: true, showSepa: true, links: [{link: 'example.com/files', name: 'Dateien', icon: '', id: null}]});
      await openDashboard(page, tc.get().token);
      const link = page.getByRole('link', { name: /Dateien/i });
      await expect(link).toBeVisible();
      await expect(link).toHaveAttribute('target', '_blank');
      await expect(link).toHaveAttribute('href', 'https://example.com/files');
      await tc.teardown();
    });

    test('File link is missing when dataLink is empty.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN, { showMail: true, showSepa: true, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('link', { name: /Dateien/i })).not.toBeVisible();
      await tc.teardown();
    });
  });

  test.describe('Audit & Backup – only Admin', () => {
    test('Admin sees audit and backup.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.ADMIN);
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^Audit$/i })).toBeVisible();
      await expect(page.getByRole('button', { name: /^Backup$/i })).toBeVisible();
      await tc.teardown();
    });

    test('User sees neither audit nor backup.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.USER);
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^Audit$/i })).not.toBeVisible();
      await expect(page.getByRole('button', { name: /^Backup$/i })).not.toBeVisible();
      await tc.teardown();
    });

    test('FinancialManager detects neither audit nor backup.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.FINANCIAL_MANAGER);
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^Audit$/i })).not.toBeVisible();
      await expect(page.getByRole('button', { name: /^Backup$/i })).not.toBeVisible();
      await tc.teardown();
    });
  });

  test.describe('Settings Submenu', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Settings visible only to admins', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: 'Einstellungen', exact: true })).toBeVisible();
    });

    test('The submenu is initially collapsed.', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(
        page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }),
      ).not.toBeVisible();
    });

    test('Click opens all seven submenu items.', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();

      for (const label of [
        'Nutzerverwaltung',
        'Email konfigurieren',
        'Externe Links konfigurieren',
        'Beitragstarifverwaltung',
        'Mitgliederkategorien verwalten',
        'Bankverbindung konfigurieren',
        'Webseiteneinstellungen',
      ]) {
        await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible();
      }
    });

    test('A second click collapses the submenu again.', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      const btn = page.getByRole('button', { name: 'Einstellungen', exact: true });
      await btn.click();
      await btn.click();
      await expect(
        page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }),
      ).not.toBeVisible();
    });

    const subPages = [
      { label: 'Nutzerverwaltung' },
      { label: 'Email konfigurieren' },
      { label: 'Externe Links konfigurieren' },
      { label: 'Beitragstarifverwaltung' },
      { label: 'Mitgliederkategorien verwalten' },
      { label: 'Bankverbindung konfigurieren' },
      { label: 'Webseiteneinstellungen' },
    ];

    for (const { label } of subPages) {
      test(`"${label}" is marked as active upon clicking.`, async ({ page }) => {
        await openDashboard(page, tc.get().token);
        await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
        const btn = page.getByRole('button', { name: label, exact: true });
        await btn.click();
        await expect(btn).toHaveClass(/Mui-selected/);
      });
    }
  });

  test.describe('Role-based Visibility', () => {
    test('User cannot see SEPA (Role not authorized)', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.USER, { showMail: true, showSepa: true, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^SEPA Export$/i })).not.toBeVisible();
      await tc.teardown();
    });

    test('FinancialManager shows SEPA', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.FINANCIAL_MANAGER, { showMail: true, showSepa: true, links: [] });
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: /^SEPA Export$/i })).toBeVisible();
      await tc.teardown();
    });

    test('FinancialManager shows no settings.', async ({ page }) => {
      const tc = makeTestContext();
      await tc.setup(Role.FINANCIAL_MANAGER);
      await openDashboard(page, tc.get().token);
      await expect(
        page.getByRole('button', { name: 'Einstellungen', exact: true }),
      ).not.toBeVisible();
      await tc.teardown();
    });
  });

  test.describe('Log out', () => {
    const tc = makeTestContext();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Clicking "Log Out" displays the login form.', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: /Abmelden/i }).click();
      await expect(page.getByRole('button', { name: /Abmelden/i })).not.toBeVisible({
        timeout: 3000,
      });
    });
  });
});
