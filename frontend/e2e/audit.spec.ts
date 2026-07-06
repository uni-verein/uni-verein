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

test.describe('Audit page', () => {
  const tc = makeTestContextOnlyUser();
  test.beforeEach(async () => {
    await tc.setup(Role.ADMIN);
  });
  test.afterEach(async () => {
    await tc.teardown();
  });

  test('Open audit page', async ({ page }) => {
    await openDashboard(page, tc.get().token);
    await page.getByRole('button', { name: 'Audit', exact: true }).click();

    await expect(page.getByRole('heading', { name: 'Audit Log' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Zeit' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Nutzer' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Eintrag' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Daten' })).toBeVisible();
    await expect(page.getByText('Zeilen pro Seite:')).toBeVisible();

    await backend.deleteAllMember();
    await tc.setup(Role.USER);
    let user = tc.get().user.username;
    await backend.createMemberAsUser(
      {
        academicDegree: null,
        birthday: '2026-03-14T23:00:00.000Z',
        city: 'Kiel',
        countryCode: 'DE',
        contributionPlanId: null,
        courseOfStudy: '',
        email: 'test_1774694157026@test.de',
        endOfStudies: '2026-02-28T23:00:00.000Z',
        entryDate: '2025-03-20T10:35:53.998Z',
        exitDate: '2026-03-19T23:00:00.000Z',
        firstName: 'Test',
        gender: 'DIVERSE',
        iban: 'DE40998929246819178888',
        bic: 'DEUTDEDEXXX',
        id: '00000000-0000-0000-0000-000000000000',
        lastName: 'Tester',
        memberCategoryId: backend.defaultMemberCategories.other,
        memberNumber: 1,
        middleName: '',
        phone: '',
        postalCode: '24103',
        sepaConsent: '2026-03-15T23:00:00.000Z',
        startOfStudies: '2025-02-28T23:00:00.000Z',
        street: 'Teststr. 3',
        taskWithinTheClub: 'MEMBER',
      },
      tc.get().token,
    );

    await tc.setup(Role.ADMIN);
    await page.reload();
    await page.getByRole('button', { name: 'Audit', exact: true }).click();
    await expect(page.getByRole('cell', { name: user })).toBeVisible();
    await expect(
      page
        .locator('div')
        .filter({ hasText: /^CREATE$/ })
        .first(),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'MemberEntity' }).first()).toBeVisible();
    await page.getByRole('cell').first().click();
    await expect(page.locator('tbody').getByText('Daten')).toBeVisible();
    await expect(page.locator('pre').getByText('{"MemberId":')).toBeVisible();
  });
});
