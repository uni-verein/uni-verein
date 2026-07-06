import { expect, Page, test } from '@playwright/test';
import { APP_BASE, BackendClient } from './BackendClient';
import { generateIBAN } from './utils';
import { BulkMail, Gender, MemberCategory, Role, TaskWithinTheClub, TestUser } from '../src/types';

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

test.describe('Member – create new member', () => {
  test.describe('Check empty member page', () => {
    const tc = makeTestContextOnlyUser();
    test.beforeEach(async () => {
      await tc.setup(Role.USER);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Page header show title', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('heading', { name: 'Mitgliederverwaltung' })).toBeVisible();
    });

    test('Page header show filter', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('textbox', { name: 'Name suche' })).toBeVisible();
      await expect(page.getByText('Alle Aufgaben')).toBeVisible();
      await expect(page.getByText('Alle', { exact: true })).toBeVisible();
    });

    test('Page header show refresh button', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: 'Aktualisieren' })).toBeVisible();
    });

    test('Page header show add button', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('button', { name: 'Mitglied hinzufügen' })).toBeVisible();
    });

    test('Show table header', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('columnheader', { name: 'Nr.' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Aufgabe im Verein' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'E-Mail' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Beitrag' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Mitgliederkategorie' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Aktionen' })).toBeVisible();
    });

    test('Show empty page', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByText('Keine Mitglieder gefunden.')).toBeVisible();
    });

    test('Show add member dialog', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Mitglied hinzufügen' }).click();
      await expect(page.getByText('Neues Mitglied anlegen', { exact: true })).toBeVisible();
      await expect(
        page.getByText('Geben Sie hier die Stammdaten des Vereinsmitglieds ein.', { exact: true }),
      ).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Mitgliedsnummer' })).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Mann' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Vorname' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Zweitname' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Nachname' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'Geburtsdatum' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Straße & Haus-Nr.' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'PLZ' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Stadt' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'E-Mail' })).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Rundmail Erlaubt' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Telefon-Nr.' })).toBeVisible();
      await page.getByRole('dialog').evaluate((el) => {
        el.scrollTop = el.scrollHeight;
      });
      await expect(page.getByRole('group', { name: 'Studienbeginn' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'Studienende' })).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Akademischer Grad' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Studiengang' })).toBeVisible();
      await expect(
        page.getByRole('combobox', { name: 'Aufgabe im Verein Mitglied' }),
      ).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Mitgliederkategorie' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'Eintrittsdatum' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'AustrittsDatum' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'IBAN' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'BIC' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'SEPA-Zustimmung' })).toBeVisible();
      await expect(
        page.getByRole('combobox', { name: 'Beitragstarif Keinen Beitrag' }),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Speichern' })).toBeVisible();
    });

    test('Create member by add member dialog', async ({ page }) => {
      await backend.deleteAllMember();
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Mitglied hinzufügen' }).click();
      await expect(page.getByText('Neues Mitglied anlegen', { exact: true })).toBeVisible();
      await expect(
        page.getByText('Geben Sie hier die Stammdaten des Vereinsmitglieds ein.', { exact: true }),
      ).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Mitgliedsnummer' })).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Mann' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Geschlecht Mann' }).click();
      await expect(page.getByRole('option', { name: 'Mann' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Frau' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Diverse' })).toBeVisible();
      await page.getByRole('option', { name: 'Frau' }).click();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Frau' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Geschlecht Frau' }).click();
      await page.getByRole('option', { name: 'Diverse' }).click();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Diverse' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Vorname' }).click();
      await page.getByRole('textbox', { name: 'Vorname' }).fill('Test');
      await page.getByRole('textbox', { name: 'Nachname' }).click();
      await page.getByRole('textbox', { name: 'Nachname' }).fill('Tester');
      await page.setViewportSize({ width: 1280, height: 1200 });
      const birthdayGroup = page.getByRole('group', { name: 'Geburtsdatum' });
      await birthdayGroup.getByRole('spinbutton', { name: 'Day' }).click();
      await page.keyboard.type('15032026');

      await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).click();
      await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).fill('Teststr. 3');

      await page.getByRole('textbox', { name: 'PLZ' }).click();
      await page.getByRole('textbox', { name: 'PLZ' }).fill('24103');

      await page.getByRole('textbox', { name: 'Stadt' }).click();
      await page.getByRole('textbox', { name: 'Stadt' }).fill('Kiel');

      await expect(page.getByRole('combobox', { name: 'Ländercode' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Ländercode' }).click();
      await page.getByRole('option', { name: 'Angola (AO)' }).click();

      await page.getByRole('textbox', { name: 'E-Mail' }).click();
      await page.getByRole('textbox', { name: 'E-Mail' }).fill(`test_${Date.now()}@test.de`);

      await expect(page.getByRole('combobox', { name: 'Rundmail Erlaubt' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Rundmail Erlaubt' }).click();
      await expect(page.getByRole('option', { name: 'Erlaubt', exact: true })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Nicht erlaubt' })).toBeVisible();
      await page.getByRole('option', { name: 'Nicht erlaubt' }).click();
      await expect(page.getByRole('combobox', { name: 'Rundmail Nicht Erlaubt' })).toBeVisible();

      await page.getByRole('group', { name: 'Studienbeginn' }).getByLabel('Choose date').click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2025', exact: true }).click();

      await page.getByRole('group', { name: 'Studienende' }).getByLabel('Choose date').click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page
        .getByLabel('März 2026', { exact: true })
        .getByRole('radio', { name: '2026' })
        .click();
      await page.getByRole('combobox', { name: 'Aufgabe im Verein Mitglied' }).click();
      await expect(page.getByRole('option', { name: '1. Vorstand' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alumni-Beauftragter' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Mitglied' })).toBeVisible();
      await page.getByRole('option', { name: 'Mitglied' }).click();

      await page.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
      await expect(page.getByRole('option', { name: 'Studierende' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alumni' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Anderes' })).toBeVisible();
      await page.getByRole('option', { name: 'Anderes' }).click();

      await page.getByRole('group', { name: 'Eintrittsdatum' }).getByLabel('Choose date').click();
      await page.getByRole('gridcell', { name: '20', exact: true }).click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2025', exact: true }).click();

      await page.getByRole('group', { name: 'AustrittsDatum' }).getByLabel('Choose date').click();
      await page.getByRole('gridcell', { name: '20', exact: true }).click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2026', exact: true }).click();

      await page.getByRole('textbox', { name: 'IBAN' }).click();
      await page.getByRole('textbox', { name: 'IBAN' }).fill('DE5150010517981387155');
      await expect(page.getByText('IBAN ungültig')).toBeVisible();
      await expect(page.getByRole('alert')).toBeVisible();
      await expect(page.getByText('Fehlerhafte eingaben')).toBeVisible();
      await page.getByRole('textbox', { name: 'IBAN' }).fill(generateIBAN({ countryCode: 'DE' }));
      await expect(page.getByRole('alert')).toBeHidden();
      await expect(page.getByText('Fehlerhafte eingaben')).toBeHidden();

      await page.getByRole('textbox', { name: 'BIC' }).click();
      await page.getByRole('textbox', { name: 'BIC' }).fill('6287683r2777');
      await expect(page.getByText('BIC ungültig')).toBeVisible();
      await expect(page.getByRole('alert')).toBeVisible();
      await expect(page.getByText('Fehlerhafte eingaben')).toBeVisible();
      await page.getByRole('textbox', { name: 'BIC' }).fill('DEUTDEDEXXX');
      await expect(page.getByRole('alert')).toBeHidden();
      await expect(page.getByText('Fehlerhafte eingaben')).not.toBeVisible();

      await page.getByRole('group', { name: 'SEPA-Zustimmung' }).getByLabel('Choose date').click();
      await page.getByRole('gridcell', { name: '16', exact: true }).click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2026', exact: true }).click();

      await page.getByRole('combobox', { name: 'Beitragstarif' }).click();
      await expect(page.getByRole('option', { name: 'Keinen Beitrag' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Default' })).toBeVisible();
      await page.getByRole('option', { name: 'Default' }).click();
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(
        page.getByRole('alert').filter({ hasText: 'Nutzer erfolgreich erstellt' }),
      ).toBeVisible();
    });

    test('Delete test member on overview', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await expect(page.getByRole('textbox', { name: 'Name suche' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Name suche' }).fill('Test');
      await expect(page.getByRole('cell', { name: 'Test Tester' })).toBeVisible();
      await page.getByRole('row', { name: 'Test Tester Mitglied' }).getByLabel('Löschen').click();

      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await expect(page.getByText('Dieses Mitglied wirklich löschen?')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();
      await expect(
        page.getByRole('alert').filter({ hasText: 'Mitglied erfolgreich gelöscht' }),
      ).toBeVisible();
      await expect(page.getByText('Keine Mitglieder gefunden.')).toBeVisible();
    });

    test('Edit test member', async ({ page }) => {
      await backend.deleteAllMember();
      await backend.createMember({
        academicDegree: null,
        birthday: '2026-03-14T23:00:00.000Z',
        city: 'Kiel',
        countryCode: 'DE',
        contributionPlanId: null,
        courseOfStudy: '',
        email: 'test_1774694157026@test.de',
        bulkMail: BulkMail.ALLOWED,
        endOfStudies: '2026-02-28T23:00:00.000Z',
        entryDate: '2025-03-20T10:35:53.998Z',
        exitDate: '2026-03-19T23:00:00.000Z',
        firstName: 'Test',
        gender: Gender.DIVERSE,
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
        taskWithinTheClub: TaskWithinTheClub.MEMBER,
      });
      await openDashboard(page, tc.get().token);
      await page.getByRole('textbox', { name: 'Name suche' }).fill('Test');
      await expect(page.getByRole('cell', { name: 'Test Tester' })).toBeVisible();
      await page
        .getByRole('row', { name: 'Test Tester Mitglied' })
        .getByLabel('Bearbeiten')
        .click();
      await page.getByRole('textbox', { name: 'Vorname' }).click();
      await page.getByRole('textbox', { name: 'Vorname' }).fill('Susanne');
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(
        page.getByRole('alert').filter({ hasText: 'Nutzeränderungen erfolgreich gespeichert.' }),
      ).toBeVisible();

      await page.getByRole('textbox', { name: 'Name suche' }).fill('Susanne');
      await expect(page.getByRole('cell', { name: 'Susanne Tester' })).toBeVisible();
      await backend.deleteAllMember();
    });

    test('Try to create duplicated member by add member dialog', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await backend.deleteAllMember();
      await backend.createMember({
        academicDegree: null,
        birthday: '2026-03-14T23:00:00.000Z',
        city: 'Kiel',
        countryCode: 'DE',
        contributionPlanId: null,
        courseOfStudy: '',
        email: 'test_1774694157026@test.de',
        bulkMail: BulkMail.ALLOWED,
        endOfStudies: '2026-02-28T23:00:00.000Z',
        entryDate: '2025-03-20T10:35:53.998Z',
        exitDate: '2026-03-19T23:00:00.000Z',
        firstName: 'Test',
        gender: Gender.DIVERSE,
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
        taskWithinTheClub: TaskWithinTheClub.MEMBER,
      });
      await page.reload();
      await page.getByRole('button', { name: 'Mitglied hinzufügen' }).click();
      await expect(page.getByText('Neues Mitglied anlegen', { exact: true })).toBeVisible();
      await expect(
        page.getByText('Geben Sie hier die Stammdaten des Vereinsmitglieds ein.', { exact: true }),
      ).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Mitgliedsnummer' })).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Mann' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Geschlecht Mann' }).click();
      await expect(page.getByRole('option', { name: 'Mann' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Frau' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Diverse' })).toBeVisible();
      await page.getByRole('option', { name: 'Frau' }).click();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Frau' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Geschlecht Frau' }).click();
      await page.getByRole('option', { name: 'Diverse' }).click();
      await expect(page.getByRole('combobox', { name: 'Geschlecht Diverse' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Vorname' }).click();
      await page.getByRole('textbox', { name: 'Vorname' }).fill('Test');
      await page.getByRole('textbox', { name: 'Nachname' }).click();
      await page.getByRole('textbox', { name: 'Nachname' }).fill('Tester');
      await page.setViewportSize({ width: 1280, height: 1200 });

      const birthdayGroup = page.getByRole('group', { name: 'Geburtsdatum' });
      await birthdayGroup.getByRole('spinbutton', { name: 'Day' }).click();
      await page.keyboard.type('15032026');

      await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).click();
      await page.getByRole('textbox', { name: 'Straße & Haus-Nr.' }).fill('Teststr. 3');

      await page.getByRole('textbox', { name: 'PLZ' }).click();
      await page.getByRole('textbox', { name: 'PLZ' }).fill('24103');

      await page.getByRole('textbox', { name: 'Stadt' }).click();
      await page.getByRole('textbox', { name: 'Stadt' }).fill('Kiel');

      await expect(page.getByRole('combobox', { name: 'Ländercode' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Ländercode' }).click();
      await page.getByRole('option', { name: 'Angola (AO)' }).click();

      await page.getByRole('textbox', { name: 'E-Mail' }).click();
      await page.getByRole('textbox', { name: 'E-Mail' }).fill(`test_${Date.now()}@test.de`);

      await page.getByRole('group', { name: 'Studienbeginn' }).getByLabel('Choose date').click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2025', exact: true }).click();

      await page.getByRole('group', { name: 'Studienende' }).getByLabel('Choose date').click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2026', exact: true }).last().click();

      await page.getByRole('combobox', { name: 'Aufgabe im Verein Mitglied' }).click();
      await expect(page.getByRole('option', { name: '1. Vorstand' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alumni-Beauftragter' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Mitglied' })).toBeVisible();
      await page.getByRole('option', { name: 'Mitglied' }).click();

      await page.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
      await expect(page.getByRole('option', { name: 'Studierende' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alumni' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Anderes' })).toBeVisible();
      await page.getByRole('option', { name: 'Anderes' }).click();

      await page.getByRole('group', { name: 'Eintrittsdatum' }).getByLabel('Choose date').click();
      await page.getByRole('gridcell', { name: '20', exact: true }).click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2025', exact: true }).click();

      await page.getByRole('group', { name: 'AustrittsDatum' }).getByLabel('Choose date').click();
      await page.getByRole('gridcell', { name: '20', exact: true }).click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2026', exact: true }).click();

      await page.getByRole('textbox', { name: 'IBAN' }).click();
      await page.getByRole('textbox', { name: 'IBAN' }).fill('DE40998929246819178888');

      await page.getByRole('textbox', { name: 'BIC' }).click();
      await page.getByRole('textbox', { name: 'BIC' }).fill('DEUTDEDEXXX');

      await page.getByRole('group', { name: 'SEPA-Zustimmung' }).getByLabel('Choose date').click();
      await page.getByRole('gridcell', { name: '16', exact: true }).click();
      await page.getByRole('radio', { name: 'März', exact: true }).click();
      await page.getByRole('radio', { name: '2026', exact: true }).click();

      await page.getByRole('combobox', { name: 'Beitragstarif' }).click();
      await expect(page.getByRole('option', { name: 'Keinen Beitrag' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Default' })).toBeVisible();
      await page.getByRole('option', { name: 'Default' }).click();
      await page.getByRole('button', { name: 'Speichern' }).click();
      await expect(
        page.getByRole('dialog').getByRole('alert').filter({
          hasText:
            'Speichern fehlgeschlagen. Mitglied mit gleicher IBAN oder E-Mail existiert bereits.',
        }),
      ).toBeVisible();
      await expect(
        page
          .getByRole('alert')
          .filter({ hasText: /^Mitglied mit gleicher IBAN oder E-Mail existiert bereits\.$/ }),
      ).toBeVisible();
      await page.getByRole('button', { name: 'Abbrechen' }).click();

      await expect(page.getByRole('textbox', { name: 'Name suche' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Name suche' }).fill('Test');
      await expect(page.getByRole('cell', { name: 'Test Tester' })).toBeVisible();
      await page.getByRole('row', { name: 'Test Tester Mitglied' }).getByLabel('Löschen').click();
      await page.getByRole('button', { name: 'Löschen' }).click();
      await expect(page.getByText('Keine Mitglieder gefunden.')).toBeVisible();
      await backend.deleteAllMember();
    });

    test('Recover soft deleted member', async ({ page }) => {
      await backend.deleteAllMember();
      await backend.createMember({
        academicDegree: null,
        birthday: '2026-03-14T23:00:00.000Z',
        city: 'Kiel',
        countryCode: 'DE',
        contributionPlanId: null,
        courseOfStudy: '',
        email: 'test_1774694157026@test.de',
        bulkMail: BulkMail.ALLOWED,
        endOfStudies: '2026-02-28T23:00:00.000Z',
        entryDate: '2025-03-20T10:35:53.998Z',
        exitDate: '2026-03-19T23:00:00.000Z',
        firstName: 'Test',
        gender: Gender.DIVERSE,
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
        taskWithinTheClub: TaskWithinTheClub.MEMBER,
      });
      await backend.softDeleteMember(tc.get().token);
      await tc.setup(Role.ADMIN);
      await openDashboard(page, tc.get().token);
      await page.reload();

      await expect(page.getByRole('textbox', { name: 'Name suche' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Name suche' }).fill('Test');
      await expect(page.getByRole('cell', { name: 'Test Tester' })).toBeVisible();

      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Wiederherstellen' }),
      ).toBeVisible();
      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Löschen' }),
      ).toBeVisible();
      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Bearbeiten' }),
      ).not.toBeVisible();

      await page
        .getByRole('row', { name: 'Test Tester Mitglied' })
        .getByRole('button', { name: 'Wiederherstellen' })
        .click();
      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await expect(page.getByText('Dieses Mitglied wirklich')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Wiederherstellen' })).toBeVisible();
      await page.getByRole('button', { name: 'Wiederherstellen' }).click();

      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Wiederherstellen' }),
      ).not.toBeVisible();
      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Ansehen' }),
      ).toBeVisible();
      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Bearbeiten' }),
      ).toBeVisible();
      await expect(
        page
          .getByRole('row', { name: 'Test Tester Mitglied' })
          .getByRole('button', { name: 'Löschen' }),
      ).toBeVisible();

      await page.getByRole('button', { name: 'Löschen' }).click();
      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await expect(page.getByText('Dieses Mitglied wirklich löschen?')).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();
      await expect(page.getByText('Keine Mitglieder gefunden.')).toBeVisible();
      await backend.deleteAllMember();
    });

    test('Show only soft deleted member', async ({ page }) => {
      await backend.deleteAllMember();
      await backend.createMember({
        academicDegree: null,
        birthday: '2026-03-14T23:00:00.000Z',
        city: 'Kiel',
        countryCode: 'DE',
        contributionPlanId: null,
        courseOfStudy: '',
        email: 'test_1774694157026@test.de',
        bulkMail: BulkMail.ALLOWED,
        endOfStudies: '2026-02-28T23:00:00.000Z',
        entryDate: '2025-03-20T10:35:53.998Z',
        exitDate: '2026-03-19T23:00:00.000Z',
        firstName: 'Test1',
        gender: Gender.DIVERSE,
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
        taskWithinTheClub: TaskWithinTheClub.MEMBER,
      });
      await backend.softDeleteMember(tc.get().token);
      await backend.createMember({
        academicDegree: null,
        birthday: '2026-03-14T23:00:00.000Z',
        city: 'Kiel',
        countryCode: 'DE',
        contributionPlanId: null,
        courseOfStudy: '',
        email: 'test_1774694157022@test.de',
        bulkMail: BulkMail.ALLOWED,
        endOfStudies: '2026-02-28T23:00:00.000Z',
        entryDate: '2025-03-20T10:35:53.998Z',
        exitDate: '2026-03-19T23:00:00.000Z',
        firstName: 'Test2',
        gender: 'DIVERSE',
        iban: 'DE40998929246819178777',
        bic: 'DEUTDEDEXXX',
        id: '00000000-0000-0000-0000-000000000000',
        lastName: 'Tester',
        memberCategoryId: backend.defaultMemberCategories.other,
        memberNumber: 2,
        middleName: '',
        phone: '',
        postalCode: '24103',
        sepaConsent: '2026-03-15T23:00:00.000Z',
        startOfStudies: '2025-02-28T23:00:00.000Z',
        street: 'Teststr. 3',
        taskWithinTheClub: TaskWithinTheClub.MEMBER,
      });
      await tc.setup(Role.ADMIN);
      await openDashboard(page, tc.get().token);
      await page.reload();
      await expect(page.getByRole('cell', { name: '1', exact: true })).toBeVisible();
      await expect(page.getByRole('cell', { name: '2', exact: true })).toBeVisible();
      await expect(page.getByRole('checkbox', { name: 'Gelöschte Mitglieder' })).toBeVisible();
      await page.getByRole('checkbox', { name: 'Gelöschte Mitglieder' }).click();
      await expect(page.getByRole('cell', { name: '1', exact: true })).toBeVisible();
      await expect(page.getByRole('cell', { name: '2', exact: true })).not.toBeVisible();
      await backend.deleteAllMember();
    });
  });
});
