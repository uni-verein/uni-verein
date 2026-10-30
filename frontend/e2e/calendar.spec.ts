import { test, expect, Page } from '@playwright/test';
import { authenticate, BackendClient, APP_BASE } from './BackendClient';
import { CalendarEventVisibility, CalendarParticipantRole, Role, TestUser } from '../src/types';

const backend = new BackendClient();
const createdUserIds = new Set<string>();

// Today 12:00-13:00 local time, so the event is always inside the month shown by default.
function todayNoon() {
  const start = new Date();
  start.setHours(12, 0, 0, 0);
  const end = new Date(start);
  end.setHours(13);
  return { start: start.toISOString(), end: end.toISOString() };
}

async function newUser(role: Role): Promise<{ user: TestUser; token: string }> {
  const user = await backend.createUser(role);
  createdUserIds.add(user.id);
  const token = await backend.loginUser(user.username, user.password);
  return { user, token };
}

async function openCalendar(page: Page, token: string) {
  await authenticate(page, token);
  await page.goto(`${APP_BASE}/?page=calendar`);
  await expect(page.getByRole('heading', { name: 'Kalender', exact: true })).toBeVisible({
    timeout: 8000,
  });
}

function calendarEvent(page: Page, title: string) {
  return page.locator('.fc-event', { hasText: title });
}

async function openListView(page: Page) {
  await page.getByRole('button', { name: 'Terminübersicht' }).click();
  await expect(page.locator('.fc-list')).toBeVisible();
  await page.waitForLoadState('networkidle');
  const showPast = page.getByRole('button', { name: /Vergangene Termine anzeigen/ });
  if (await showPast.isVisible()) await showPast.click();
}

test.beforeAll(async () => {
  await backend.init();
});

test.afterAll(async () => {
  for (const id of createdUserIds) {
    await backend.deleteUser(id);
  }
});

test.describe('Calendar', () => {
  test('A user creates an event with a location and sees it in the calendar', async ({ page }) => {
    const { token } = await newUser(Role.USER);
    const title = `Sommerfest ${Date.now()}`;
    await openCalendar(page, token);

    await page.getByRole('button', { name: 'Neuer Termin' }).click();
    await page.getByLabel('Titel').fill(title);
    await page.getByLabel('Ort', { exact: true }).fill('Stadtpark');
    await page.getByRole('button', { name: 'Speichern' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: title })).toBeVisible();
    await expect(dialog.getByText('Stadtpark')).toBeVisible();
    await dialog.getByRole('button', { name: 'Schließen' }).click();

    await openListView(page);
    await expect(calendarEvent(page, title)).toBeVisible();
  });

  test('An invited co-editor accepts, edits the description and the owner is notified', async ({
    browser,
  }) => {
    const owner = await newUser(Role.USER);
    const editor = await newUser(Role.USER);
    const title = `Vorstandssitzung ${Date.now()}`;
    const created = await backend.createCalendarEvent(owner.token, {
      title,
      ...todayNoon(),
      participants: [{ userId: editor.user.id, role: CalendarParticipantRole.EDITOR }],
    });

    const editorPage = await (await browser.newContext()).newPage();
    await openCalendar(editorPage, editor.token);
    await editorPage.getByRole('button', { name: 'Benachrichtigungen' }).click();
    await editorPage.getByText(`hat dich zu „${title}“ eingeladen`).click();

    const dialog = editorPage.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: title })).toBeVisible();
    await dialog.getByRole('button', { name: 'Zusage' }).click();
    await expect(dialog.getByText('Zusagen: 1')).toBeVisible();

    await dialog.getByRole('button', { name: 'Bearbeiten' }).click();
    await dialog.getByLabel('Beschreibung').fill('Tagesordnung folgt');
    await dialog.getByRole('button', { name: 'Speichern' }).click();
    await expect(dialog.getByText('Tagesordnung folgt')).toBeVisible();

    const ownerPage = await (await browser.newContext()).newPage();
    await openCalendar(ownerPage, owner.token);
    await ownerPage.getByRole('button', { name: 'Benachrichtigungen' }).click();
    await expect(ownerPage.getByText(`hat für „${title}“ zugesagt`)).toBeVisible();
    await expect(ownerPage.getByText(`hat „${title}“ geändert (Beschreibung)`)).toBeVisible();

    await backend.deleteCalendarEvent(owner.token, created.id);
  });

  test('A private event is only visible to its owner', async ({ browser }) => {
    const owner = await newUser(Role.USER);
    const other = await newUser(Role.ADMIN);
    const title = `Arzttermin ${Date.now()}`;
    const created = await backend.createCalendarEvent(owner.token, {
      title,
      ...todayNoon(),
      visibility: CalendarEventVisibility.PRIVATE,
    });

    const ownerPage = await (await browser.newContext()).newPage();
    await openCalendar(ownerPage, owner.token);
    await openListView(ownerPage);
    await expect(calendarEvent(ownerPage, title)).toBeVisible();

    const otherPage = await (await browser.newContext()).newPage();
    await openCalendar(otherPage, other.token);
    await openListView(otherPage);
    await expect(otherPage.locator('.fc-view')).toBeVisible();
    await expect(calendarEvent(otherPage, title)).toHaveCount(0);

    await backend.deleteCalendarEvent(owner.token, created.id);
  });

  test('Public events can be viewed without login on the public calendar page', async ({
    browser,
  }) => {
    await backend.setPublicCalendar(true, 'Öffentlicher Vereinskalender');
    const owner = await newUser(Role.USER);
    const publicTitle = `Tag der offenen Tür ${Date.now()}`;
    const internalTitle = `Internes Treffen ${Date.now()}`;
    const publicEvent = await backend.createCalendarEvent(owner.token, {
      title: publicTitle,
      ...todayNoon(),
      isPublic: true,
    });
    const internalEvent = await backend.createCalendarEvent(owner.token, {
      title: internalTitle,
      ...todayNoon(),
    });

    try {
      const page = await (await browser.newContext()).newPage();
      await page.goto(`${APP_BASE}/calendar/public`);
      await expect(
        page.getByRole('heading', { name: 'Öffentlicher Vereinskalender' }),
      ).toBeVisible();
      await expect(calendarEvent(page, publicTitle)).toBeVisible();
      await expect(calendarEvent(page, internalTitle)).toHaveCount(0);

      await calendarEvent(page, publicTitle).click();
      await expect(page.getByRole('dialog').getByText(publicTitle)).toBeVisible();
    } finally {
      await backend.deleteCalendarEvent(owner.token, publicEvent.id);
      await backend.deleteCalendarEvent(owner.token, internalEvent.id);
      await backend.setPublicCalendar(false);
    }
  });
});
