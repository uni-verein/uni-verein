import { Theme } from '@mui/material';
import deLocale from '@fullcalendar/core/locales/de';
import { CalendarOptions, EventInput } from '@fullcalendar/core';

export const baseCalendarOptions = {
  locales: [deLocale],
  height: 'auto',
  firstDay: 1,
  dayMaxEventRows: 3,
  eventDisplay: 'block',
} satisfies CalendarOptions;

export function calendarLocale(language: string): string {
  return language.startsWith('de') ? 'de' : 'en-gb';
}

interface Occurrence {
  eventId: string;
  title: string;
  start: string;
  end: string;
  allDay: boolean;
  color: string | null;
}

export function occurrenceToEventInput(o: Occurrence, theme: Theme): EventInput {
  const color = o.color ?? theme.palette.primary.main;
  return {
    id: `${o.eventId}|${o.start}`,
    title: o.title,
    start: o.allDay ? o.start.substring(0, 10) : o.start,
    end: o.allDay ? o.end.substring(0, 10) : o.end,
    allDay: o.allDay,
    backgroundColor: color,
    borderColor: color,
    textColor: theme.palette.getContrastText(color),
    extendedProps: { occurrence: o },
  };
}
