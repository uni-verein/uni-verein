import { test, expect, Page } from '@playwright/test';
import { authenticate, BackendClient, APP_BASE } from './BackendClient';
import { totpCode } from './totp';
import { Role, TestUser } from '../src/types';

const backend = new BackendClient();
const createdUserIds = new Set<string>();

const RECOVERY_CODE_PATTERN = /^[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}$/;

test.beforeAll(async () => {
  await backend.init();
});

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

async function newUser(role: Role = Role.USER): Promise<TestUser> {
  const user = await backend.createUser(role);
  createdUserIds.add(user.id);
  return user;
}

async function newUserWithTwoFactor(role: Role = Role.USER) {
  const user = await newUser(role);
  const passwordToken = await backend.loginUser(user.username, user.password);
  const { secret, recoveryCodes, token } = await backend.enableTwoFactor(
    passwordToken,
    user.password,
  );
  return { user, token, secret, recoveryCodes };
}

async function signIn(page: Page, user: TestUser) {
  await page.goto(APP_BASE);
  await page.getByRole('textbox', { name: 'Benutzername' }).fill(user.username);
  await page.getByRole('textbox', { name: 'Passwort' }).fill(user.password);
  await page.getByRole('button', { name: 'Anmelden' }).click();
}

const signOutButton = (page: Page) => page.getByRole('button', { name: 'Abmelden', exact: true });
const codeField = (page: Page) => page.getByRole('textbox', { name: 'Code' });
const CODE_PROMPT = 'Gib den 6-stelligen Code aus deiner Authenticator-App ein.';
const WRONG_CODE = 'Der Code ist falsch oder abgelaufen.';

async function submitLoginCode(page: Page, code: string) {
  await codeField(page).fill(code);
  await codeField(page).press('Enter');
}

async function openSecurityTab(page: Page, token: string) {
  await authenticate(page, token);
  await page.goto(APP_BASE);
  await expect(page.getByLabel('Profileinstellungen')).toBeVisible({ timeout: 8000 });
  await page.getByLabel('Profileinstellungen').click();
  await page.getByRole('tab', { name: 'Sicherheit' }).click();
  await expect(page.getByText('Zwei-Faktor-Authentifizierung', { exact: true })).toBeVisible();
}

test.describe('TOTP helper', () => {
  test('matches the RFC 6238 test vector', () => {
    // RFC 6238, appendix B: secret "12345678901234567890", time 59 s, SHA-1, 8 digits 94287082.
    expect(totpCode('GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ', 0, 59_000)).toBe('287082');
    expect(totpCode('GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ', 0, 1111111109_000)).toBe('081804');
  });
});

test.describe('Sign in without two-factor authentication', () => {
  test('needs the password only', async ({ page }) => {
    const user = await newUser();

    await signIn(page, user);

    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
    await expect(page.getByText(CODE_PROMPT)).not.toBeVisible();
  });
});

test.describe('Setting up two-factor authentication', () => {
  test('asks for the password before the QR code is shown', async ({ page }) => {
    const user = await newUser();
    const token = await backend.loginUser(user.username, user.password);
    await openSecurityTab(page, token);
    await expect(page.getByText('Nicht aktiv', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Aktivieren' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Passwort bestätigen')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Weiter' })).toBeDisabled();

    await dialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill('falsches-Passwort-1');
    await dialog.getByRole('button', { name: 'Weiter' }).click();
    await expect(dialog.getByText('Das Passwort ist falsch.')).toBeVisible();
    await expect(page.getByText('Oder gib diesen Schlüssel manuell ein:')).not.toBeVisible();

    await dialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill(user.password);
    await dialog.getByRole('button', { name: 'Weiter' }).click();
    await expect(dialog).not.toBeVisible();
    await expect(page.getByText('Oder gib diesen Schlüssel manuell ein:')).toBeVisible();
    await expect(page.locator('svg[role="img"]')).toBeVisible();
    await expect(page.getByText(/^[A-Z2-7]{32}$/)).toBeVisible();
  });

  test('activates with a valid code and shows ten recovery codes once', async ({ page }) => {
    const user = await newUser();
    const token = await backend.loginUser(user.username, user.password);
    await openSecurityTab(page, token);

    await page.getByRole('button', { name: 'Aktivieren' }).click();
    const passwordDialog = page.getByRole('dialog');
    await passwordDialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill(user.password);
    await passwordDialog.getByRole('button', { name: 'Weiter' }).click();
    await expect(page.getByText(/^[A-Z2-7]{32}$/)).toBeVisible();
    const secret = (await page.getByText(/^[A-Z2-7]{32}$/).textContent())!.trim();

    const confirm = page.getByRole('button', { name: 'Bestätigen und aktivieren' });
    await expect(confirm).toBeDisabled();
    await page.getByRole('textbox', { name: '6-stelliger Code' }).fill('000000');
    await confirm.click();
    await expect(page.getByText('Der Code ist falsch. Bitte erneut versuchen.')).toBeVisible();
    await expect(page.getByText('Aktiv', { exact: true })).not.toBeVisible();

    await page.getByRole('textbox', { name: '6-stelliger Code' }).fill(totpCode(secret));
    await confirm.click();

    const recoveryDialog = page.getByRole('dialog');
    await expect(recoveryDialog.getByText('Wiederherstellungscodes')).toBeVisible();
    const codes = (await recoveryDialog.getByText(RECOVERY_CODE_PATTERN).allTextContents()).map(
      (c) => c.trim(),
    );
    expect(codes).toHaveLength(10);
    expect(new Set(codes).size).toBe(10);

    await recoveryDialog.getByRole('button', { name: 'Ich habe sie gespeichert' }).click();
    await expect(recoveryDialog).not.toBeVisible();
    await expect(page.getByText('Aktiv', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Deaktivieren' })).toBeVisible();
    await expect(page.getByText(codes[0])).not.toBeVisible();
  });

  test('the API refuses a setup without the right password (stolen session)', async () => {
    const user = await newUser();
    const token = await backend.loginUser(user.username, user.password);

    expect(await backend.startTwoFactorSetup(token)).toBe(400);
    expect(await backend.startTwoFactorSetup(token, 'falsches-Passwort-1')).toBe(400);
    expect(await backend.startTwoFactorSetup(token, user.password)).toBe(200);
  });
});

test.describe('Signing in with two-factor authentication', () => {
  test('asks for a code after the password and gives no session before it', async ({ page }) => {
    const { user } = await newUserWithTwoFactor();

    await signIn(page, user);

    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });
    await expect(signOutButton(page)).not.toBeVisible();
    expect((await page.context().cookies()).find((c) => c.name === 'uv_auth')).toBeUndefined();
  });

  test('rejects a wrong code, accepts the right one and keeps the session', async ({ page }) => {
    const { user, secret } = await newUserWithTwoFactor();
    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });

    await submitLoginCode(page, '000000');
    await expect(page.getByText(WRONG_CODE)).toBeVisible();
    await expect(signOutButton(page)).not.toBeVisible();

    await submitLoginCode(page, totpCode(secret, 1));
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
    const cookie = (await page.context().cookies()).find((c) => c.name === 'uv_auth');
    expect(cookie?.httpOnly).toBe(true);

    await page.reload();
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
  });

  test('a code used before is not accepted a second time', async ({ page, browser }) => {
    const { user, secret } = await newUserWithTwoFactor();
    const code = totpCode(secret, 1);

    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });
    await submitLoginCode(page, code);
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });

    const otherContext = await browser.newContext();
    const otherPage = await otherContext.newPage();
    await signIn(otherPage, user);
    await expect(otherPage.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });
    await submitLoginCode(otherPage, code);
    await expect(otherPage.getByText(WRONG_CODE)).toBeVisible();
    await expect(signOutButton(otherPage)).not.toBeVisible();
    await otherContext.close();
  });

  test('accepts the code typed with a space', async ({ page }) => {
    const { user, secret } = await newUserWithTwoFactor();
    const code = totpCode(secret, 1);
    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });

    await submitLoginCode(page, `${code.slice(0, 3)} ${code.slice(3)}`);

    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
  });

  test('"Zurück" returns to the password form', async ({ page }) => {
    const { user } = await newUserWithTwoFactor();
    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });

    await page.getByRole('button', { name: 'Zurück' }).click();

    await expect(page.getByRole('textbox', { name: 'Benutzername' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Passwort' })).toHaveValue('');
    await expect(page.getByText(CODE_PROMPT)).not.toBeVisible();
  });

  test('a recovery code works once, typed in lowercase without dashes', async ({ page }) => {
    const { user, recoveryCodes } = await newUserWithTwoFactor();
    const typed = recoveryCodes[0].replace(/-/g, '').toLowerCase();

    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });
    await submitLoginCode(page, typed);
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });

    await signOutButton(page).click();
    await expect(page.getByRole('textbox', { name: 'Benutzername' })).toBeVisible();
    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });
    await submitLoginCode(page, typed);
    await expect(page.getByText(WRONG_CODE)).toBeVisible();
    await expect(signOutButton(page)).not.toBeVisible();

    await submitLoginCode(page, recoveryCodes[1]);
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
  });

  test('repeated wrong codes lock the entry, and the hint for recovery codes stays hidden', async ({
    page,
  }) => {
    const { user, secret } = await newUserWithTwoFactor();
    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });

    for (const wrong of ['000000', '000001', '000002']) {
      await submitLoginCode(page, wrong);
      await expect(page.getByText(WRONG_CODE)).toBeVisible();
    }

    await submitLoginCode(page, totpCode(secret, 1));
    await expect(page.getByText(/Zu viele fehlerhafte Anmeldeversuche\./)).toBeVisible();
    await expect(page.getByText(/Codes aus der App sind gerade gesperrt/)).not.toBeVisible();
    await expect(signOutButton(page)).not.toBeVisible();
  });

  test('tells that a recovery code still works while the account is blocked', async ({ page }) => {
    const { user, recoveryCodes } = await newUserWithTwoFactor();
    await signIn(page, user);
    await expect(page.getByText(CODE_PROMPT)).toBeVisible({ timeout: 8000 });

    await page.route('**/api/auth/login/2fa', async (route) => {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'To many login attempts.',
          remainingTime: 600,
          recoveryCodeAllowed: true,
        }),
      });
    });

    await submitLoginCode(page, '123456');
    await expect(page.getByText(/Zu viele fehlerhafte Anmeldeversuche\./)).toBeVisible();
    await expect(
      page.getByText(
        'Codes aus der App sind gerade gesperrt. Mit einem Wiederherstellungscode kannst du dich trotzdem anmelden.',
      ),
    ).toBeVisible();

    await page.unroute('**/api/auth/login/2fa');
    await submitLoginCode(page, recoveryCodes[0]);
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
  });
});

test.describe('Turning two-factor authentication off', () => {
  async function openDisableDialog(page: Page, token: string) {
    await openSecurityTab(page, token);
    await expect(page.getByText('Aktiv', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Deaktivieren' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Zwei-Faktor-Authentifizierung deaktivieren')).toBeVisible();
    return dialog;
  }

  test('needs the password and a code', async ({ page }) => {
    const { user, token, secret } = await newUserWithTwoFactor();
    const dialog = await openDisableDialog(page, token);
    const submit = dialog.getByRole('button', { name: 'Deaktivieren' });
    await expect(submit).toBeDisabled();

    await dialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill('falsches-Passwort-1');
    await dialog
      .getByRole('textbox', { name: 'Code oder Wiederherstellungscode' })
      .fill(totpCode(secret, 1));
    await submit.click();
    await expect(
      dialog.getByText('Deaktivieren fehlgeschlagen. Passwort oder Code sind falsch.'),
    ).toBeVisible();

    await dialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill(user.password);
    await dialog.getByRole('textbox', { name: 'Code oder Wiederherstellungscode' }).fill('000000');
    await submit.click();
    await expect(
      dialog.getByText('Deaktivieren fehlgeschlagen. Passwort oder Code sind falsch.'),
    ).toBeVisible();
    await expect(dialog).toBeVisible();
  });

  test('turns it off with the password and a code, and the next sign in needs no code', async ({
    page,
  }) => {
    const { user, token, secret } = await newUserWithTwoFactor();
    const dialog = await openDisableDialog(page, token);

    await dialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill(user.password);
    await dialog
      .getByRole('textbox', { name: 'Code oder Wiederherstellungscode' })
      .fill(totpCode(secret, 1));
    await dialog.getByRole('button', { name: 'Deaktivieren' }).click();

    await expect(page.getByText('Zwei-Faktor-Authentifizierung deaktiviert.')).toBeVisible();
    await expect(page.getByText('Nicht aktiv', { exact: true })).toBeVisible();

    await page.context().clearCookies();
    await signIn(page, user);
    await expect(signOutButton(page)).toBeVisible({ timeout: 8000 });
    await expect(page.getByText(CODE_PROMPT)).not.toBeVisible();
  });

  test('accepts a recovery code instead of the authenticator code', async ({ page }) => {
    const { user, token, recoveryCodes } = await newUserWithTwoFactor();
    const dialog = await openDisableDialog(page, token);

    await dialog.getByRole('textbox', { name: 'Aktuelles Passwort' }).fill(user.password);
    await dialog
      .getByRole('textbox', { name: 'Code oder Wiederherstellungscode' })
      .fill(recoveryCodes[0]);
    await dialog.getByRole('button', { name: 'Deaktivieren' }).click();

    await expect(page.getByText('Zwei-Faktor-Authentifizierung deaktiviert.')).toBeVisible();
  });
});

test.describe('Admin resets the two-factor authentication of another user', () => {
  test('removes it, the user signs in with the password only afterwards', async ({
    page,
    browser,
  }) => {
    const admin = await newUser(Role.ADMIN);
    const adminToken = await backend.loginUser(admin.username, admin.password);
    const { user: target } = await newUserWithTwoFactor();

    await authenticate(page, adminToken);
    await page.goto(APP_BASE);
    await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
    await page.getByRole('button', { name: 'Einstellungen', exact: true }).click();
    await page.getByRole('button', { name: 'Nutzerverwaltung', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Nutzerverwaltung' })).toBeVisible();

    const row = page.getByRole('row').filter({ hasText: target.username });
    await expect(row).toBeVisible();
    const adminRow = page.getByRole('row').filter({ hasText: admin.username });
    await expect(adminRow.getByLabel('Zwei-Faktor-Authentifizierung zurücksetzen')).toHaveCount(0);

    await row.getByLabel('Zwei-Faktor-Authentifizierung zurücksetzen').click();
    await page.getByRole('dialog').getByRole('button', { name: 'Zurücksetzen' }).click();

    await expect(page.getByText('Zwei-Faktor-Authentifizierung zurückgesetzt.')).toBeVisible();
    await expect(row.getByLabel('Zwei-Faktor-Authentifizierung zurücksetzen')).toHaveCount(0);

    const userContext = await browser.newContext();
    const userPage = await userContext.newPage();
    await signIn(userPage, target);
    await expect(signOutButton(userPage)).toBeVisible({ timeout: 8000 });
    await expect(userPage.getByText(CODE_PROMPT)).not.toBeVisible();
    await userContext.close();
  });
});
