import { test, expect, Page } from '@playwright/test';
import { BackendClient, APP_BASE } from './BackendClient';
import path from 'path';
import fs from 'fs';
import os from 'os';
import { Role, TestUser } from '../src/types';

const createdUserIds = new Set<string>();
const backend = new BackendClient();

// A minimal, valid single-page PDF ("Gesamtbetrag 12,34 EUR" as its only
// text) so pdfjs-dist can actually parse and rasterize it for the preview.
const MINIMAL_TEST_PDF_BASE64 =
  'JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCAyMDAgMjAwXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA0IDAgUiA+PiA+PiAvQ29udGVudHMgNSAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL1R5cGUgL0ZvbnQgL1N1YnR5cGUgL1R5cGUxIC9CYXNlRm9udCAvSGVsdmV0aWNhID4+CmVuZG9iago1IDAgb2JqCjw8IC9MZW5ndGggNTMgPj4Kc3RyZWFtCkJUIC9GMSAyNCBUZiAyMCAxMDAgVGQgKEdlc2FtdGJldHJhZyAxMiwzNCBFVVIpIFRqIEVUCmVuZHN0cmVhbQplbmRvYmoKeHJlZgowIDYKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDA5IDAwMDAwIG4gCjAwMDAwMDAwNTggMDAwMDAgbiAKMDAwMDAwMDExNSAwMDAwMCBuIAowMDAwMDAwMjQxIDAwMDAwIG4gCjAwMDAwMDAzMTEgMDAwMDAgbiAKdHJhaWxlcgo8PCAvU2l6ZSA2IC9Sb290IDEgMCBSID4+CnN0YXJ0eHJlZgo0MTQKJSVFT0Y=';

async function openDashboard(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('token', t);
  }, token);

  await page.goto(APP_BASE);
  await expect(page.getByText('Vereinsverwaltung')).toBeVisible({ timeout: 8000 });
}

async function goToReceipts(page: Page) {
  await page.getByRole('button', { name: 'Belege', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Belegsverwaltung' })).toBeVisible();
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

test.describe('Receipts page', () => {
  const tc = makeTestContextOnlyUser();

  test.beforeEach(async () => {
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test('Open receipts page as ADMIN', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('button', { name: 'Beleg hinzufügen' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Datum' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Betrag' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Eingereicht von' })).toBeVisible();
    await expect(page.getByText('Keine Belege gefunden.')).toBeVisible();
  });

  test('Create receipt without image', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);
    await page.getByRole('button', { name: 'Beleg hinzufügen' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Neuer Beleg', level: 5 })).toBeVisible();
    await dialog.getByRole('textbox', { name: 'Betrag' }).fill('42,50');
    await dialog.getByRole('textbox', { name: 'Händler' }).fill('Testhändler GmbH');
    await dialog.getByRole('button', { name: 'Speichern' }).click();

    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole('cell', { name: '42,50 €' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Testhändler GmbH' })).toBeVisible();
  });

  test('Create receipt with image and view it', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);
    await page.getByRole('button', { name: 'Beleg hinzufügen' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('textbox', { name: 'Betrag' }).fill('10,00');

    const imagePath = path.join(os.tmpdir(), 'playwright-receipt.png');
    fs.writeFileSync(
      imagePath,
      Buffer.from(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
        'base64',
      ),
    );
    await dialog.locator('input[type="file"]').first().setInputFiles(imagePath);
    await expect(dialog.getByAltText('playwright-receipt.png')).toBeVisible({ timeout: 15000 });

    await dialog.getByRole('button', { name: 'Speichern' }).click();
    await expect(dialog).not.toBeVisible({ timeout: 10000 });

    await page.getByLabel('Ansehen').first().click();
    const viewDialog = page.getByRole('dialog');
    await expect(
      viewDialog.getByRole('heading', { name: 'Beleg ansehen', level: 5 }),
    ).toBeVisible();
    await expect(viewDialog.locator('img')).toBeVisible({ timeout: 10000 });
    await viewDialog.getByRole('button', { name: 'Schließen' }).click();

    fs.unlinkSync(imagePath);
  });

  test('Create receipt with PDF and view it', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);
    await page.getByRole('button', { name: 'Beleg hinzufügen' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('textbox', { name: 'Betrag' }).fill('10,00');

    const pdfPath = path.join(os.tmpdir(), 'playwright-receipt.pdf');
    fs.writeFileSync(pdfPath, Buffer.from(MINIMAL_TEST_PDF_BASE64, 'base64'));
    await dialog.locator('input[type="file"]').first().setInputFiles(pdfPath);
    await expect(dialog.getByAltText('playwright-receipt.pdf')).toBeVisible({ timeout: 15000 });

    await dialog.getByRole('button', { name: 'Speichern' }).click();
    await expect(dialog).not.toBeVisible({ timeout: 10000 });

    await page.getByLabel('Ansehen').first().click();
    const viewDialog = page.getByRole('dialog');
    await expect(
      viewDialog.getByRole('heading', { name: 'Beleg ansehen', level: 5 }),
    ).toBeVisible();
    await expect(viewDialog.locator('img')).toBeVisible({ timeout: 15000 });
    await viewDialog.locator('img').click();

    const lightbox = page.getByRole('dialog').last();
    await expect(lightbox.getByRole('link', { name: 'PDF öffnen' })).toBeVisible({
      timeout: 15000,
    });

    fs.unlinkSync(pdfPath);
  });

  test('Soft delete then hard delete receipt as ADMIN', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, { amount: '5.00' });
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: '5,00 €' })).toBeVisible();
    await page.getByLabel('Löschen').first().click();
    await expect(page.getByRole('heading', { name: 'Bestätigung' })).toBeVisible();
    await expect(page.getByText('Beleg wirklich löschen?')).toBeVisible();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Beleg erfolgreich gelöscht.' }),
    ).toBeVisible();

    await page.getByLabel('Endgültig löschen').first().click();
    await expect(page.getByText(/ACHTUNG/)).toBeVisible();
    await page.getByRole('button', { name: 'Endgültig löschen', exact: true }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Beleg endgültig gelöscht.' }),
    ).toBeVisible();
    await expect(page.getByText('Keine Belege gefunden.')).toBeVisible();
  });

  test('USER only sees own receipts and cannot hard delete', async ({ page }) => {
    const otherUser = await backend.createUser(Role.ADMIN);
    createdUserIds.add(otherUser.id);
    const otherToken = await backend.loginUser(otherUser.username, otherUser.password);
    await backend.createTestReceipt(otherToken, { amount: '9.00', vendor: 'Other user receipt' });

    await tc.setup(Role.USER);
    await backend.createTestReceipt(tc.get().token, { amount: '7.00', vendor: 'Own receipt' });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Own receipt' })).toBeVisible();
    await expect(page.getByText('Other user receipt')).not.toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Eingereicht von' })).not.toBeVisible();
    await expect(page.getByLabel('Endgültig löschen')).toHaveCount(0);

    await backend.deleteUser(otherUser.id);
    createdUserIds.delete(otherUser.id);
  });

  test('FINANCIAL_MANAGER sees all receipts including other users', async ({ page }) => {
    const otherUser = await backend.createUser(Role.USER);
    createdUserIds.add(otherUser.id);
    const otherToken = await backend.loginUser(otherUser.username, otherUser.password);
    await backend.createTestReceipt(otherToken, { amount: '3.00', vendor: 'Other user receipt' });

    await tc.setup(Role.FINANCIAL_MANAGER);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Other user receipt' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Eingereicht von' })).toBeVisible();
    await expect(page.getByLabel('Endgültig löschen')).toHaveCount(0);

    await backend.deleteUser(otherUser.id);
    createdUserIds.delete(otherUser.id);
  });

  test('Export receipts as ZIP', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    const pdfBytes = Buffer.from(
      '%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF',
      'utf-8',
    );
    await backend.createTestReceiptWithPdf(tc.get().token, pdfBytes);

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByLabel('Als ZIP exportieren').click(),
    ]);

    expect(download.suggestedFilename()).toMatch(/receipts_export_\d{8}_\d{6}\.zip/);
    const downloadPath = await download.path();
    expect(fs.statSync(downloadPath!).size).toBeGreaterThan(0);

    await expect(page.getByText('Export erfolgreich.')).toBeVisible();
  });

  test('Filter by category', async ({ page }) => {
    const category = await backend.createReceiptCategory(`Playwright Kategorie ${Date.now()}`);
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '11.00',
      vendor: 'Categorized receipt',
      categoryId: category.id,
    });
    await backend.createTestReceipt(tc.get().token, {
      amount: '22.00',
      vendor: 'Uncategorized receipt',
    });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(
      page.getByRole('cell', { name: 'Categorized receipt', exact: true }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Uncategorized receipt' })).toBeVisible();

    await page.getByRole('combobox', { name: 'Kategorie' }).click();
    await page.getByRole('option', { name: category.name }).click();

    await expect(
      page.getByRole('cell', { name: 'Categorized receipt', exact: true }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Uncategorized receipt' })).not.toBeVisible();
  });

  test('Filter by date range', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '11.00',
      vendor: 'March receipt',
      receiptDate: '2026-03-15T00:00:00.000Z',
    });
    await backend.createTestReceipt(tc.get().token, {
      amount: '22.00',
      vendor: 'June receipt',
      receiptDate: '2026-06-15T00:00:00.000Z',
    });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'March receipt' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'June receipt' })).toBeVisible();

    const fromGroup = page.getByRole('group', { name: 'Von' });
    await fromGroup.getByRole('spinbutton', { name: 'Day' }).click();
    await page.keyboard.type('01032026');

    const toGroup = page.getByRole('group', { name: 'Bis' });
    await toGroup.getByRole('spinbutton', { name: 'Day' }).click();
    await page.keyboard.type('31032026');

    await expect(page.getByRole('cell', { name: 'March receipt' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'June receipt' })).not.toBeVisible();
  });

  test('Reset filter clears active filters', async ({ page }) => {
    const category = await backend.createReceiptCategory(`Playwright Reset ${Date.now()}`);
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '11.00',
      vendor: 'Filtered receipt',
      categoryId: category.id,
    });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await page.getByRole('combobox', { name: 'Kategorie' }).click();
    await page.getByRole('option', { name: category.name }).click();
    await expect(page.getByRole('button', { name: 'Filter zurücksetzen' })).toBeVisible();

    await page.getByRole('button', { name: 'Filter zurücksetzen' }).click();

    await expect(page.getByRole('button', { name: 'Filter zurücksetzen' })).not.toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Kategorie' })).toHaveText('Alle Kategorien');
  });

  test('Restore a soft-deleted receipt as ADMIN', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '8.00',
      vendor: 'Restorable receipt',
    });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await page.getByLabel('Löschen').first().click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Beleg erfolgreich gelöscht.' }),
    ).toBeVisible();

    await page.getByLabel('Gelöschte anzeigen').check();
    await expect(page.getByRole('cell', { name: 'Restorable receipt' })).toBeVisible();

    await page.getByLabel('Wiederherstellen').click();
    await expect(page.getByText('Diesen Beleg wirklich wiederherstellen?')).toBeVisible();
    await page.getByRole('button', { name: 'Wiederherstellen', exact: true }).click();

    await expect(
      page.getByRole('alert').filter({ hasText: 'Beleg erfolgreich wiederhergestellt.' }),
    ).toBeVisible();

    await page.getByLabel('Gelöschte anzeigen').uncheck();
    await expect(page.getByRole('cell', { name: 'Restorable receipt' })).toBeVisible();
  });

  test('Edit a receipt within the edit window', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, { amount: '5.00', vendor: 'Original Vendor' });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Original Vendor' })).toBeVisible();
    await page.getByLabel('Bearbeiten').first().click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Beleg bearbeiten', level: 5 })).toBeVisible();
    await dialog.getByRole('textbox', { name: 'Betrag' }).fill('42,00');
    await dialog.getByRole('textbox', { name: 'Händler' }).fill('Corrected Vendor');
    await dialog.getByRole('button', { name: 'Speichern' }).click();

    await expect(dialog).not.toBeVisible();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Beleg erfolgreich bearbeitet.' }),
    ).toBeVisible();
    await expect(page.getByRole('cell', { name: '42,00 €' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Corrected Vendor' })).toBeVisible();
  });

  test('Edit action is not available for a deleted receipt', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, { amount: '5.00', vendor: 'Deleted receipt' });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByLabel('Bearbeiten')).toHaveCount(1);
    await page.getByLabel('Löschen').first().click();
    await page.getByRole('button', { name: 'Löschen', exact: true }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Beleg erfolgreich gelöscht.' }),
    ).toBeVisible();

    await page.getByLabel('Gelöschte anzeigen').check();
    await expect(page.getByRole('cell', { name: 'Deleted receipt' })).toBeVisible();
    await expect(page.getByLabel('Bearbeiten')).toHaveCount(0);
    await expect(page.getByLabel('Wiederherstellen')).toHaveCount(1);
  });
});

test.describe('Receipt payment method & paid status', () => {
  const tc = makeTestContextOnlyUser();

  test.beforeEach(async () => {
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test.afterEach(async () => {
    await tc.teardown();
    await backend.deleteAllReceipts();
    await backend.deleteAllReceiptCategories();
  });

  test('Payment method field is hidden for USER on create', async ({ page }) => {
    await tc.setup(Role.USER);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);
    await page.getByRole('button', { name: 'Beleg hinzufügen' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Neuer Beleg', level: 5 })).toBeVisible();
    await expect(dialog.getByRole('combobox', { name: 'Zahlungsart' })).toHaveCount(0);
  });

  test('Payment method field is available for ADMIN and persists after creation', async ({
    page,
  }) => {
    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);
    await page.getByRole('button', { name: 'Beleg hinzufügen' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('textbox', { name: 'Betrag' }).fill('15,00');
    await dialog.getByRole('textbox', { name: 'Händler' }).fill('Bar bezahlt');
    await dialog.getByRole('combobox', { name: 'Zahlungsart' }).click();
    await page.getByRole('option', { name: 'Bar' }).click();
    await dialog.getByRole('button', { name: 'Speichern' }).click();
    await expect(dialog).not.toBeVisible();

    await page.getByLabel('Ansehen').first().click();
    const viewDialog = page.getByRole('dialog');
    await expect(viewDialog.getByRole('combobox', { name: 'Zahlungsart' })).toHaveText('Bar');
  });

  test('New receipt shows Offen status and no mark-as-paid button for USER', async ({ page }) => {
    await tc.setup(Role.USER);
    await backend.createTestReceipt(tc.get().token, { amount: '9.00', vendor: 'Fresh receipt' });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Offen' })).toBeVisible();
    await expect(page.getByLabel('Als bezahlt markieren')).toHaveCount(0);
  });

  test('Mark receipt as paid when payment method is already set', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '12.00',
      vendor: 'Preset method',
      paymentMethod: 'CASH',
    });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Offen' })).toBeVisible();
    await page.getByLabel('Als bezahlt markieren').first().click();

    const dialog = page.getByRole('dialog');
    await expect(
      dialog.getByRole('heading', { name: 'Beleg als bezahlt markieren' }),
    ).toBeVisible();
    await expect(dialog.getByRole('combobox', { name: 'Zahlungsart' })).toHaveText('Bar');
    await expect(dialog.getByRole('combobox', { name: 'Zahlungsart' })).toBeDisabled();
    await dialog.getByRole('button', { name: 'Als bezahlt markieren' }).click();

    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole('cell', { name: 'Bezahlt' })).toBeVisible();
    await expect(page.getByLabel('Bearbeiten')).toHaveCount(0);
  });

  test('Marking as paid requires selecting a payment method when none is set', async ({
    page,
  }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, { amount: '8.00', vendor: 'No method yet' });

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);
    await page.getByLabel('Als bezahlt markieren').first().click();

    const dialog = page.getByRole('dialog');
    const confirmButton = dialog.getByRole('button', { name: 'Als bezahlt markieren' });
    await expect(confirmButton).toBeDisabled();

    await dialog.getByRole('combobox', { name: 'Zahlungsart' }).click();
    await page.getByRole('option', { name: 'Überweisung' }).click();
    await expect(confirmButton).toBeEnabled();
    await confirmButton.click();

    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole('cell', { name: 'Bezahlt' })).toBeVisible();
  });

  test('Status filter narrows list to open or paid receipts', async ({ page }) => {
    await tc.setup(Role.ADMIN);
    await backend.createTestReceipt(tc.get().token, {
      amount: '4.00',
      vendor: 'Open receipt',
    });
    const paid = await backend.createTestReceipt(tc.get().token, {
      amount: '6.00',
      vendor: 'Paid receipt',
    });
    await backend.payReceipt(tc.get().token, paid.id, 'CASH');

    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Open receipt' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Paid receipt' })).toBeVisible();

    await page.getByRole('combobox', { name: 'Status' }).click();
    await page.getByRole('option', { name: 'Bezahlt', exact: true }).click();
    await expect(page.getByRole('cell', { name: 'Paid receipt' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Open receipt' })).not.toBeVisible();

    await page.getByRole('combobox', { name: 'Status' }).click();
    await page.getByRole('option', { name: 'Offen', exact: true }).click();
    await expect(page.getByRole('cell', { name: 'Open receipt' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Paid receipt' })).not.toBeVisible();
  });

  test('ADMIN cannot edit another user\'s receipt but can still mark it as paid', async ({
    page,
  }) => {
    const otherUser = await backend.createUser(Role.USER);
    createdUserIds.add(otherUser.id);
    const otherToken = await backend.loginUser(otherUser.username, otherUser.password);
    await backend.createTestReceipt(otherToken, { amount: '13.00', vendor: 'Foreign receipt' });

    await tc.setup(Role.ADMIN);
    await openDashboard(page, tc.get().token);
    await goToReceipts(page);

    await expect(page.getByRole('cell', { name: 'Foreign receipt' })).toBeVisible();
    await expect(page.getByLabel('Bearbeiten')).toHaveCount(0);

    await page.getByLabel('Als bezahlt markieren').first().click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('combobox', { name: 'Zahlungsart' }).click();
    await page.getByRole('option', { name: 'Karte' }).click();
    await dialog.getByRole('button', { name: 'Als bezahlt markieren' }).click();

    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole('cell', { name: 'Bezahlt' })).toBeVisible();

    await backend.deleteUser(otherUser.id);
    createdUserIds.delete(otherUser.id);
  });
});
