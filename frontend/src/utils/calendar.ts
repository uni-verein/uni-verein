import dayjs, { Dayjs } from 'dayjs';

// All-day events are stored as UTC midnight of their (local) calendar date with an exclusive end, so the date
// part of the ISO string is the calendar date regardless of the viewer's time zone.
export function allDayToIso(date: Dayjs): string {
  return `${date.format('YYYY-MM-DD')}T00:00:00Z`;
}

export function isoToAllDayDate(iso: string): Dayjs {
  return dayjs(iso.substring(0, 10));
}

export function isValidHttpUrl(value: string): boolean {
  if (!value.trim()) return true;
  try {
    const url = new URL(value.trim());
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

export function toggleId(ids: string[], id: string): string[] {
  return ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id];
}

export function mapSearchUrl(address: string): string {
  return `https://www.openstreetmap.org/search?query=${encodeURIComponent(address)}`;
}

export function formatEventRange(
  start: string,
  end: string,
  allDay: boolean,
  language: string,
): string {
  const locale = language.startsWith('de') ? 'de-DE' : 'en-GB';
  if (allDay) {
    const first = isoToAllDayDate(start);
    const last = isoToAllDayDate(end).subtract(1, 'day');
    const dateFormat: Intl.DateTimeFormatOptions = {
      weekday: 'short',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    };
    const from = first.toDate().toLocaleDateString(locale, dateFormat);
    if (!last.isAfter(first, 'day')) return from;
    return `${from} – ${last.toDate().toLocaleDateString(locale, dateFormat)}`;
  }

  const startDate = new Date(start);
  const endDate = new Date(end);
  const dateTime: Intl.DateTimeFormatOptions = {
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  };
  const from = startDate.toLocaleString(locale, dateTime);
  const sameDay = dayjs(startDate).isSame(dayjs(endDate), 'day');
  const to = sameDay
    ? endDate.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })
    : endDate.toLocaleString(locale, dateTime);
  return `${from} – ${to}`;
}

// Links of the public calendar page and its iCal feed, optionally restricted to one category.
export function publicCalendarLinks(key: string | null, categoryId?: string) {
  const origin = window.location.origin;
  const params = new URLSearchParams();
  if (key) params.set('key', key);
  const pageQuery = params.toString();
  if (categoryId) params.set('categoryId', categoryId);
  const feedQuery = params.toString();

  const feedHttps = `${origin}/api/public/calendar/feed.ics${feedQuery ? `?${feedQuery}` : ''}`;
  return {
    page: `${origin}/calendar/public${pageQuery ? `?${pageQuery}` : ''}`,
    feedHttps,
    feedWebcal: feedHttps.replace(/^https?:/, 'webcal:'),
    google: `https://calendar.google.com/calendar/r?cid=${encodeURIComponent(feedHttps.replace(/^https?:/, 'webcal:'))}`,
  };
}
