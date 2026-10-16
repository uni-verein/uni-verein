import dayjs, { Dayjs } from 'dayjs';
import { TFunction } from 'i18next';
import { CalendarEvent, CalendarEventPayload, CalendarEventVisibility } from '../../types';
import { allDayToIso, isoToAllDayDate, isValidHttpUrl } from '../../utils/calendar';
import {
  buildRule,
  daysUntilFirstOccurrence,
  defaultRecurrence,
  parseRule,
  RecurrenceState,
} from '../../utils/recurrence';
import { ParticipantDraft } from '../CalendarParticipantsEditor';

export type CalendarEventFormMode = 'create' | 'view' | 'edit';

export interface CalendarEventFormValues {
  title: string;
  description: string;
  allDay: boolean;
  start: Dayjs;
  end: Dayjs;
  locationName: string;
  locationAddress: string;
  locationUrl: string;
  visibility: CalendarEventVisibility;
  categoryId: string;
  participants: ParticipantDraft[];
  recurrence: RecurrenceState;
  reminder: number | '';
  isPublic: boolean;
}

export interface CalendarEventFormErrors {
  title?: string;
  end?: string;
  locationUrl?: string;
}

export interface CalendarEventFieldProps {
  values: CalendarEventFormValues;
  errors: CalendarEventFormErrors;
  onChange: (patch: Partial<CalendarEventFormValues>) => void;
  onClearError: (field: keyof CalendarEventFormErrors) => void;
}

export function initialFormValues(
  initialStart?: Dayjs,
  initialEnd?: Dayjs,
  initialAllDay?: boolean,
): CalendarEventFormValues {
  const start = initialStart ?? dayjs().add(1, 'hour').startOf('hour');
  return {
    title: '',
    description: '',
    allDay: initialAllDay ?? false,
    start,
    end: initialEnd ?? (initialAllDay ? start : start.add(1, 'hour')),
    locationName: '',
    locationAddress: '',
    locationUrl: '',
    visibility: CalendarEventVisibility.CLUB,
    categoryId: '',
    participants: [],
    recurrence: defaultRecurrence(start),
    reminder: '',
    isPublic: false,
  };
}

export function eventToFormValues(e: CalendarEvent): CalendarEventFormValues {
  return {
    title: e.title,
    description: e.description,
    allDay: e.allDay,
    start: e.allDay ? isoToAllDayDate(e.start) : dayjs(e.start),
    end: e.allDay ? isoToAllDayDate(e.end).subtract(1, 'day') : dayjs(e.end),
    locationName: e.locationName,
    locationAddress: e.locationAddress,
    locationUrl: e.locationUrl,
    visibility: e.visibility,
    categoryId: e.category?.id ?? '',
    participants: e.participants.map((p) => ({
      userId: p.userId,
      username: p.username,
      role: p.role,
    })),
    recurrence: parseRule(e.recurrenceRule, dayjs(e.start)),
    reminder: e.reminderMinutesBefore ?? '',
    isPublic: e.isPublic,
  };
}

export function validateFormValues(
  values: CalendarEventFormValues,
  t: TFunction,
): CalendarEventFormErrors {
  const { title, allDay, start, end, locationUrl } = values;
  const errors: CalendarEventFormErrors = {};
  if (!title.trim()) errors.title = t('components.calendarEventForm.validation.titleRequired');
  else if (title.length > 200)
    errors.title = t('components.calendarEventForm.validation.titleTooLong');

  if (allDay ? end.isBefore(start, 'day') : !end.isAfter(start))
    errors.end = t('components.calendarEventForm.validation.endBeforeStart');

  if (!isValidHttpUrl(locationUrl))
    errors.locationUrl = t('components.calendarEventForm.validation.invalidUrl');

  return errors;
}

export function buildPayload(
  values: CalendarEventFormValues,
  version: number | undefined,
): CalendarEventPayload {
  const { allDay, start, end, recurrence, visibility } = values;
  // A weekly series always starts on one of its weekdays, otherwise the shown start would not be an occurrence.
  const shift = daysUntilFirstOccurrence(start, recurrence);
  const seriesStart = start.add(shift, 'day');
  const seriesEnd = end.add(shift, 'day');
  return {
    title: values.title.trim(),
    description: values.description,
    start: allDay ? allDayToIso(seriesStart) : seriesStart.toISOString(),
    end: allDay ? allDayToIso(seriesEnd.add(1, 'day')) : seriesEnd.toISOString(),
    allDay,
    locationName: values.locationName,
    locationAddress: values.locationAddress,
    locationUrl: values.locationUrl.trim(),
    visibility,
    categoryId: values.categoryId || null,
    isPublic: visibility === CalendarEventVisibility.CLUB && values.isPublic,
    participants:
      visibility === CalendarEventVisibility.PRIVATE
        ? []
        : values.participants.map((p) => ({ userId: p.userId, role: p.role })),
    recurrenceRule: buildRule(recurrence, allDay),
    reminderMinutesBefore: values.reminder === '' ? null : values.reminder,
    version,
  };
}
