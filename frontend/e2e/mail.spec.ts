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

test.describe('E-Mail – Configure and send e-mail', () => {
  test.describe('configure email', () => {
    const tc = makeTestContextOnlyUser();
    test.beforeEach(async () => {
      await tc.setup(Role.ADMIN);
    });
    test.afterEach(async () => {
      await tc.teardown();
    });

    test('Open mail config', async ({ page }) => {
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 900 });
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await expect(
        page.getByRole('button', { name: 'Email konfigurieren', exact: true }),
      ).toBeVisible();
      await page.getByRole('button', { name: 'Email konfigurieren', exact: true }).click();
      await expect(page.getByRole('heading', { name: 'Email Konfiguration' })).toBeVisible();
      await expect(
        page.getByText('Verwalten Sie hier die SMTP-Zugangsdaten für den Versand der Rundmails.'),
      ).toBeVisible();
      await expect(page.getByRole('heading', { name: 'SMTP Server-Einstellungen' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'SMTP Host / Server' })).toBeVisible();
      await page.getByRole('textbox', { name: 'SMTP Host / Server' }).click();
      await page.getByRole('textbox', { name: 'SMTP Host / Server' }).fill('papercut');

      await expect(page.getByRole('spinbutton', { name: 'SMTP-Port' })).toBeVisible();
      await page.getByRole('spinbutton', { name: 'SMTP-Port' }).click();
      await page.getByRole('spinbutton', { name: 'SMTP-Port' }).fill('2525');

      await expect(page.getByRole('heading', { name: 'IMAP Server-Einstellungen' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'IMAP Host / Server' })).toBeVisible();
      await page.getByRole('textbox', { name: 'IMAP Host / Server' }).click();
      await page.getByRole('textbox', { name: 'IMAP Host / Server' }).fill('papercut');

      await expect(page.getByRole('spinbutton', { name: 'IMAP-Port' })).toBeVisible();
      await page.getByRole('spinbutton', { name: 'IMAP-Port' }).click();
      await page.getByRole('spinbutton', { name: 'IMAP-Port' }).fill('2525');

      await expect(
        page.getByRole('heading', { name: 'Authentifizierung & Absender' }),
      ).toBeVisible();
      await page.getByRole('textbox', { name: 'Benutzername / Email' }).click();
      await page.getByRole('textbox', { name: 'Benutzername / Email' }).fill('test');

      await expect(page.getByRole('textbox', { name: 'Absender Email (From)' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Absender Email (From)' }).click();
      await page.getByRole('textbox', { name: 'Absender Email (From)' }).fill('noreply@test.de');

      await expect(page.getByRole('textbox', { name: 'Passwort' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Passwort' }).click();
      await page.getByRole('textbox', { name: 'Passwort' }).fill('test');

      await expect(page.getByRole('button', { name: 'Konfiguration speichern' })).toBeVisible();
      await page.getByRole('button', { name: 'Konfiguration speichern' }).click();
      await expect(
        page.getByRole('alert').filter({ hasText: 'Speichern erfolgreich.' }),
      ).toBeVisible();

      await expect(page.getByRole('heading', { name: 'Testmodus' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: 'Empfänger-E-Mail' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Empfänger-E-Mail' }).fill('test@example.de');
      await expect(page.getByRole('button', { name: 'Test-E-Mail senden' })).toBeVisible();

      await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
      await backend.deleteMailSettings();
    });

    test('Test mail config', async ({ page }) => {
      await backend.updateMailSettings();
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
      await openDashboard(page, tc.get().token);
      await page.setViewportSize({ width: 1280, height: 900 });
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await page.getByRole('button', { name: 'Email konfigurieren', exact: true }).click();
      await expect(page.getByRole('heading', { name: 'Testmodus' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Empfänger-E-Mail' }).fill('test@example.de');
      await expect(page.getByRole('button', { name: 'Test-E-Mail senden' })).toBeVisible();
      await page.getByRole('button', { name: 'Test-E-Mail senden' }).click();

      await page.goto('http://localhost:8080');
      await expect(page.getByRole('link', { name: 'Inbox (1)' })).toBeVisible();
      await page.getByText('Test mail from noreply@test.de a few seconds ago').click();
      await expect(page.getByText('Test mail from noreply@test.de', { exact: true })).toBeVisible();
      await expect(
        page.locator('#preview-html').contentFrame().getByText('Email successfully configured'),
      ).toBeVisible();
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
      await backend.deleteMailSettings();
    });

    test('Delete mail settings', async ({ page }) => {
      await backend.updateMailSettings();
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
      await page.getByRole('button', { name: 'Email konfigurieren', exact: true }).click();
      await expect(page.getByRole('heading', { name: 'SMTP Server-Einstellungen' })).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();
      await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
      await expect(page.getByText('Mail-Server-Einstellungen wirklich löschen?')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Abbrechen' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
      await page.getByRole('button', { name: 'Löschen' }).click();
      await expect(
        page.getByRole('alert').filter({ hasText: 'Löschen erfolgreich.' }),
      ).toBeVisible();
    });

    test('Show mail page', async ({ page }) => {
      await backend.deleteAllMember();
      await backend.createMember({
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
      });
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
      await backend.updateMailSettings();
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Rundmail' }).click();
      await expect(
        page.getByRole('heading', { name: 'Rundmail versenden', exact: true }),
      ).toBeVisible();

      await expect(
        page.getByText(
          'Erstellen Sie hier eine Nachricht, die an benutzerdefinierte Mitglieder gesendet wird.',
        ),
      ).toBeVisible();

      await expect(page.getByRole('tab', { name: '✏️ Editor' })).toBeVisible();
      await expect(page.getByRole('tab', { name: '👥 Empfänger (0/1)' })).toBeVisible();
      await expect(page.getByRole('tab', { name: '📊 Status' })).toBeVisible();

      await expect(page.getByRole('textbox', { name: 'Betreff' })).toBeVisible();
      await expect(
        page
          .locator('div')
          .filter({ hasText: /^H1H2$/ })
          .first(),
      ).toBeVisible();
      await expect(
        page
          .locator('div')
          .filter({ hasText: /^Hallo \{firstname\},hier ist deine Nachricht\.\.\.$/ })
          .first(),
      ).toBeVisible();
      await expect(page.getByText('0 Empfänger ausgewählt')).toBeVisible();
      await expect(
        page.getByText(
          'Tipp: Nutze {fullname} für die personalisierte Anrede mit Vor- & Nachnamen und {firstname} für nur den Vornamen.',
        ),
      ).toBeVisible();

      await page.getByRole('tab', { name: '👥 Empfänger (0/1)' }).click();
      await expect(page.getByText('Empfängerliste')).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Mitgliederkategorie' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
      await expect(page.getByRole('option', { name: 'Benutzerdefiniert' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alle' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Studierende' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alumni' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Andere' })).toBeVisible();
      await page.getByRole('option', { name: 'Benutzerdefiniert' }).click();

      await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'E-Mail' })).toBeVisible();
      await expect(page.getByRole('cell').filter({ hasText: /^$/ })).toBeVisible();
      await expect(page.getByRole('cell', { name: 'T', exact: true })).toBeVisible();
      await expect(page.getByRole('cell', { name: 'Test Tester' })).toBeVisible();
      await expect(page.getByRole('cell', { name: 'test_1774694157026@test.de' })).toBeVisible();
      await expect(page.getByText('Zeilen pro Seite:')).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Zeilen pro Seite:' })).toBeVisible();
      await expect(page.getByText('1–1 of 1')).toBeVisible();

      await page.getByRole('tab', { name: '📊 Status' }).click();
      await expect(page.getByRole('heading', { name: 'Kein Versand aktiv' })).toBeVisible();
      await expect(page.getByText('Gehe zum Editor und klicke')).toBeVisible();

      await page.getByRole('tab', { name: '👥 Empfänger (0/1)' }).click();
      await page.getByRole('cell').filter({ hasText: /^$/ }).click();
      await expect(page.getByRole('tab', { name: '👥 Empfänger (1/1)' })).toBeVisible();

      await page.getByRole('tab', { name: '✏️ Editor' }).click();
      await expect(page.getByRole('textbox', { name: 'Betreff' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Betreff' }).fill('Mitteilung');

      const editor = page.locator('.ProseMirror');
      await editor.waitFor({ state: 'visible' });
      await editor.click();
      await page.keyboard.type('Test text');

      await expect(page.getByRole('button', { name: 'An 1 Empfänger senden' })).toBeVisible();
      await page.getByRole('button', { name: 'An 1 Empfänger senden' }).click();

      await expect(page.getByRole('heading', { name: 'Versand abgeschlossen' })).toBeVisible();
      await expect(page.getByText('Fortschritt')).toBeVisible();
      await expect(page.getByText('100%')).toBeVisible();
      await expect(page.getByText('1Gesamt')).toBeVisible();
      await expect(page.getByText('1Erfolgreich')).toBeVisible();
      await expect(page.getByText('0Fehlgeschlagen')).toBeVisible();
      await expect(page.getByRole('heading', { name: 'Live Log' })).toBeVisible();
      await expect(
        page.getByRole('listitem').filter({ hasText: 'test_1774694157026@test.de' }),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'Neue E-Mail erstellen' })).toBeVisible();

      await page.goto('http://localhost:8080');
      await expect(page.getByRole('link', { name: 'Inbox (1)' })).toBeVisible();
      await page.getByText('Mitteilung a few seconds ago').click();
      await expect(page.getByText('Mitteilung', { exact: true })).toBeVisible();
      await expect(
        page.locator('#preview-html').contentFrame().getByText('Hallo Test,'),
      ).toBeVisible();
      await expect(
        page
          .locator('#preview-html')
          .contentFrame()
          .getByText('hier ist deine Nachricht...Test text'),
      ).toBeVisible();
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
      await backend.deleteAllMember();
      await backend.deleteMailSettings();
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
    });

    test('Show mail page more then 150 member', async ({ page }) => {
      await backend.deleteAllMember();
      for (let i = 0; i < 151; i++) {
        await backend.createMember({
          academicDegree: null,
          birthday: '2026-03-14T23:00:00.000Z',
          city: 'Kiel',
          countryCode: 'DE',
          contributionPlanId: null,
          courseOfStudy: '',
          email: `${i}_test_1774694157026@test.de`,
          endOfStudies: '2026-02-28T23:00:00.000Z',
          entryDate: '2025-03-20T10:35:53.998Z',
          exitDate: '2026-03-19T23:00:00.000Z',
          firstName: `Test_${i}`,
          gender: 'DIVERSE',
          iban: `DE40998929246819178${i}`,
          id: '00000000-0000-0000-0000-000000000000',
          lastName: 'Tester',
          memberCategoryId: backend.defaultMemberCategories.other,
          memberNumber: i,
          middleName: '',
          phone: '',
          postalCode: '24103',
          sepaConsent: '2026-03-15T23:00:00.000Z',
          startOfStudies: '2025-02-28T23:00:00.000Z',
          street: 'Teststr. 3',
          taskWithinTheClub: 'MEMBER',
        });
      }
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
      await backend.updateMailSettings();
      await openDashboard(page, tc.get().token);
      await page.getByRole('button', { name: 'Rundmail' }).click();
      await expect(
        page.getByRole('heading', { name: 'Rundmail versenden', exact: true }),
      ).toBeVisible();

      await expect(
        page.getByText(
          'Erstellen Sie hier eine Nachricht, die an benutzerdefinierte Mitglieder gesendet wird.',
        ),
      ).toBeVisible();

      await expect(page.getByRole('textbox', { name: 'Betreff' })).toBeVisible();
      await expect(
        page
          .locator('div')
          .filter({ hasText: /^H1H2$/ })
          .first(),
      ).toBeVisible();
      await expect(
        page
          .locator('div')
          .filter({ hasText: /^Hallo \{firstname\},hier ist deine Nachricht\.\.\.$/ })
          .first(),
      ).toBeVisible();
      await expect(
        page.getByText(
          'Tipp: Nutze {fullname} für die personalisierte Anrede mit Vor- & Nachnamen und {firstname} für nur den Vornamen.',
        ),
      ).toBeVisible();

      await page.getByRole('tab', { name: '👥 Empfänger (0/151)' }).click();
      await expect(page.getByText('Empfängerliste')).toBeVisible();
      await expect(page.getByRole('combobox', { name: 'Mitgliederkategorie' })).toBeVisible();
      await page.getByRole('combobox', { name: 'Mitgliederkategorie' }).click();
      await expect(page.getByRole('option', { name: 'Benutzerdefiniert' })).toBeVisible();
      await expect(page.getByRole('option', { name: 'Alle' })).toBeVisible();
      await page.getByRole('option', { name: 'Alle' }).click();

      await expect(page.getByRole('combobox', { name: 'Zeilen pro Seite:' })).toBeVisible();
      await expect(page.getByText('1–10 of 151')).toBeVisible();

      await page.getByRole('tab', { name: '✏️ Editor' }).click();
      await expect(page.getByRole('textbox', { name: 'Betreff' })).toBeVisible();
      await page.getByRole('textbox', { name: 'Betreff' }).fill('Mitteilung');

      await expect(page.getByText('151 Empfänger ausgewählt')).toBeVisible();
      await expect(
        page.getByText(
          'Achtung: Es sind mehr als 150 personen ausgewählt. Bitte entferne die persönliche anrede ({fullname} / {firstname}). Persönliche anrede ist nur bis 150 personen möglich.',
        ),
      ).toBeVisible();

      const editor = page.locator('.ProseMirror');
      await editor.waitFor({ state: 'visible' });
      await editor.click();
      await editor.fill('');
      await page.keyboard.type('Test text');

      await expect(page.getByRole('button', { name: 'An 151 Empfänger senden' })).toBeVisible();
      await page.getByRole('button', { name: 'An 151 Empfänger senden' }).click();

      await expect(page.getByRole('heading', { name: 'Versand abgeschlossen' })).toBeVisible();
      await expect(page.getByText('Fortschritt')).toBeVisible();
      await expect(page.getByText('100%')).toBeVisible();

      await page.goto('http://localhost:8080');
      await expect(page.getByRole('link', { name: 'Inbox (1)' })).toBeVisible();
      await page.getByText('Mitteilung a few seconds ago').click();
      await expect(page.getByText('Mitteilung', { exact: true })).toBeVisible();
      for (let i = 0; i < 151; i++) {
        await expect(
          page.getByText(`${i}_test_1774694157026@test.de`, { exact: true }),
        ).toBeVisible();
      }
      await expect(
        page.locator('#preview-html').contentFrame().getByText('Test text'),
      ).toBeVisible();
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });
      await backend.deleteAllMember();
      await backend.deleteMailSettings();
      await fetch('http://localhost:8080/api/messages', { method: 'DELETE' });

      await backend.deleteAllMember();
      await backend.deleteMailSettings();
    });
  });
});
