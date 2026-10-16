import dayjs, { Dayjs } from 'dayjs';
import { TFunction } from 'i18next';

export type RecurrenceFrequency = 'NONE' | 'DAILY' | 'WEEKLY' | 'MONTHLY' | 'YEARLY';
export type RecurrenceEnd = 'NEVER' | 'UNTIL' | 'COUNT';

export const WEEKDAYS = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU'] as const;

export interface RecurrenceState {
  freq: RecurrenceFrequency;
  interval: number;
  byDay: string[];
  end: RecurrenceEnd;
  until: Dayjs | null;
  count: number;
}

export function defaultRecurrence(start: Dayjs): RecurrenceState {
  return {
    freq: 'NONE',
    interval: 1,
    byDay: [WEEKDAYS[(start.day() + 6) % 7]],
    end: 'NEVER',
    until: null,
    count: 10,
  };
}

function parseUntil(value: string): Dayjs | null {
  const match = /^(\d{4})(\d{2})(\d{2})(T(\d{2})(\d{2})(\d{2})Z?)?$/.exec(value);
  if (!match) return null;
  const [, y, m, d, time, hh, mm, ss] = match;
  if (!time) return dayjs(`${y}-${m}-${d}`);
  return dayjs(`${y}-${m}-${d}T${hh}:${mm}:${ss}Z`);
}

// Supports the subset of RRULE the editor can produce.
export function parseRule(rule: string | null | undefined, start: Dayjs): RecurrenceState {
  const state = defaultRecurrence(start);
  if (!rule) return state;

  const parts = Object.fromEntries(
    rule
      .replace(/^RRULE:/i, '')
      .split(';')
      .map((p) => p.split('=') as [string, string])
      .filter(([k, v]) => k && v)
      .map(([k, v]) => [k.toUpperCase(), v.toUpperCase()]),
  );

  if (['DAILY', 'WEEKLY', 'MONTHLY', 'YEARLY'].includes(parts.FREQ)) {
    state.freq = parts.FREQ as RecurrenceFrequency;
  }
  if (parts.INTERVAL) state.interval = Math.max(1, parseInt(parts.INTERVAL, 10) || 1);
  if (parts.BYDAY) state.byDay = parts.BYDAY.split(',').filter((d: string) => d.length === 2);
  if (parts.COUNT) {
    state.end = 'COUNT';
    state.count = Math.max(1, parseInt(parts.COUNT, 10) || 1);
  } else if (parts.UNTIL) {
    const until = parseUntil(parts.UNTIL);
    if (until) {
      state.end = 'UNTIL';
      state.until = until;
    }
  }
  return state;
}

export function buildRule(state: RecurrenceState, allDay: boolean): string | null {
  if (state.freq === 'NONE') return null;

  const parts = [`FREQ=${state.freq}`];
  if (state.interval > 1) parts.push(`INTERVAL=${state.interval}`);
  if (state.freq === 'WEEKLY' && state.byDay.length > 0) {
    const ordered = WEEKDAYS.filter((d) => state.byDay.includes(d));
    parts.push(`BYDAY=${ordered.join(',')}`);
  }
  if (state.end === 'COUNT') parts.push(`COUNT=${state.count}`);
  if (state.end === 'UNTIL' && state.until) {
    // The until date is inclusive: all-day series end on that date, timed series at the end of that local day.
    parts.push(
      allDay
        ? `UNTIL=${state.until.format('YYYYMMDD')}`
        : `UNTIL=${state.until
            .endOf('day')
            .toDate()
            .toISOString()
            .replace(/[-:]/g, '')
            .replace(/\.\d{3}/, '')}`,
    );
  }
  return parts.join(';');
}

export function describeRule(rule: string | null, start: Dayjs, t: TFunction, language: string) {
  if (!rule) return null;
  const state = parseRule(rule, start);
  if (state.freq === 'NONE') return null;

  const unit = t(`components.recurrenceEditor.units.${state.freq}`, { count: state.interval });
  let text =
    state.interval === 1
      ? t(`components.recurrenceEditor.describe.every.${state.freq}`)
      : t('components.recurrenceEditor.describe.everyN', { count: state.interval, unit });

  if (state.freq === 'WEEKLY' && state.byDay.length > 0) {
    const days = WEEKDAYS.filter((d) => state.byDay.includes(d))
      .map((d) => t(`components.recurrenceEditor.weekdaysShort.${d}`))
      .join(', ');
    text += ' ' + t('components.recurrenceEditor.describe.onDays', { days });
  }

  if (state.end === 'COUNT') {
    text += ' · ' + t('components.recurrenceEditor.describe.count', { count: state.count });
  } else if (state.end === 'UNTIL' && state.until) {
    const locale = language.startsWith('de') ? 'de-DE' : 'en-GB';
    text +=
      ' · ' +
      t('components.recurrenceEditor.describe.until', {
        date: state.until.toDate().toLocaleDateString(locale),
      });
  }
  return text;
}

// Days to move the start forward so a weekly series starts on one of its selected weekdays.
export function daysUntilFirstOccurrence(start: Dayjs, state: RecurrenceState): number {
  if (state.freq !== 'WEEKLY' || state.byDay.length === 0) return 0;
  for (let offset = 0; offset < 7; offset++) {
    const weekday = WEEKDAYS[(start.add(offset, 'day').day() + 6) % 7];
    if (state.byDay.includes(weekday)) return offset;
  }
  return 0;
}

export const REMINDER_OPTIONS = [0, 15, 60, 180, 1440, 2880, 10080] as const;
