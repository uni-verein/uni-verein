import React, { useCallback, useEffect, useState } from 'react';
import { Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import { api } from '../../api';
import { CalendarCategory, CalendarEvent, CalendarRsvpStatus } from '../../types';
import { formatEventRange } from '../../utils/calendar';
import { useConfirm } from '../../hooks/useConfirm';
import { useSnackbar } from '../../hooks/useSnackbar';
import {
  buildPayload,
  CalendarEventFormErrors,
  CalendarEventFormMode,
  CalendarEventFormValues,
  eventToFormValues,
  initialFormValues,
  validateFormValues,
} from './formValues';

interface Options {
  eventId?: string;
  initialStart?: Dayjs;
  initialEnd?: Dayjs;
  initialAllDay?: boolean;
  occurrence?: { start: string; end: string };
  onClose: () => void;
  onChanged: () => void;
}

export function useCalendarEventForm({
  eventId,
  initialStart,
  initialEnd,
  initialAllDay,
  occurrence,
  onClose,
  onChanged,
}: Options) {
  const { t, i18n } = useTranslation();
  const setSnackbar = useSnackbar();
  const { open: confirmOpen, confirm, handleClose: handleConfirmClose } = useConfirm();
  const [confirmMessage, setConfirmMessage] = useState('');

  const [mode, setMode] = useState<CalendarEventFormMode>(eventId ? 'view' : 'create');
  const [calendarEvent, setCalendarEvent] = useState<CalendarEvent | null>(null);
  const [loading, setLoading] = useState(!!eventId);
  const [saving, setSaving] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [errors, setErrors] = useState<CalendarEventFormErrors>({});
  const [categories, setCategories] = useState<CalendarCategory[]>([]);
  const [values, setValues] = useState<CalendarEventFormValues>(() =>
    initialFormValues(initialStart, initialEnd, initialAllDay),
  );

  const applyEvent = useCallback((e: CalendarEvent) => {
    setCalendarEvent(e);
    setValues(eventToFormValues(e));
  }, []);

  const applyResult = (result: unknown) => {
    if (result && typeof result === 'object') applyEvent(result as CalendarEvent);
  };

  const loadEvent = useCallback(async () => {
    if (!eventId) return;
    setLoading(true);
    try {
      const data: CalendarEvent | null = await api(`/calendar/events/${eventId}`);
      if (data) applyEvent(data);
    } catch {
      setApiError(t('components.calendarEventForm.errors.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [eventId, t, applyEvent]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadEvent();
  }, [loadEvent]);

  useEffect(() => {
    const loadCategories = async () => {
      try {
        const data = await api('/calendar/categories');
        if (data) setCategories(data.items);
      } catch {
        // best-effort: event can still be saved without category
      }
    };
    loadCategories();
  }, []);

  const changeValues = (patch: Partial<CalendarEventFormValues>) =>
    setValues((prev) => ({ ...prev, ...patch }));

  const clearError = (field: keyof CalendarEventFormErrors) =>
    setErrors((prev) => ({ ...prev, [field]: undefined }));

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    const newErrors = validateFormValues(values, t);
    setErrors(newErrors);
    if (Object.keys(newErrors).length > 0) return;

    setSaving(true);
    setApiError(null);
    try {
      const body = JSON.stringify(buildPayload(values, calendarEvent?.version));
      const result =
        mode === 'create'
          ? await api('/calendar/events', { method: 'POST', body })
          : await api(`/calendar/events/${calendarEvent!.id}`, { method: 'PUT', body });

      if (result === 409) {
        setConflict(true);
        return;
      }

      setSnackbar({
        status: 'success',
        message: t(
          mode === 'create'
            ? 'components.calendarEventForm.snackbar.created'
            : 'components.calendarEventForm.snackbar.updated',
        ),
      });
      onChanged();
      if (result && typeof result === 'object') {
        applyEvent(result as CalendarEvent);
        setMode('view');
      } else {
        onClose();
      }
    } catch {
      setApiError(t('components.calendarEventForm.errors.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const deleteEvent = async () => {
    if (!calendarEvent) return;
    setConfirmMessage(
      t('components.calendarEventForm.confirm.deleteMessage', { title: calendarEvent.title }),
    );
    if (!(await confirm())) return;

    try {
      await api(`/calendar/events/${calendarEvent.id}`, { method: 'DELETE' });
      setSnackbar({
        status: 'success',
        message: t('components.calendarEventForm.snackbar.deleted'),
      });
      onChanged();
      onClose();
    } catch {
      setApiError(t('components.calendarEventForm.errors.deleteFailed'));
    }
  };

  const deleteOccurrence = async () => {
    if (!calendarEvent || !occurrence) return;
    setConfirmMessage(
      t('components.calendarEventForm.confirm.deleteOccurrenceMessage', {
        title: calendarEvent.title,
        date: formatEventRange(
          occurrence.start,
          occurrence.end,
          calendarEvent.allDay,
          i18n.language,
        ),
      }),
    );
    if (!(await confirm())) return;

    try {
      const result = await api(`/calendar/events/${calendarEvent.id}/exceptions`, {
        method: 'POST',
        body: JSON.stringify({ occurrenceStart: occurrence.start }),
      });
      if (result === 409) {
        setConflict(true);
        return;
      }
      setSnackbar({
        status: 'success',
        message: t('components.calendarEventForm.snackbar.occurrenceDeleted'),
      });
      onChanged();
      onClose();
    } catch {
      setApiError(t('components.calendarEventForm.errors.deleteFailed'));
    }
  };

  const reply = async (status: CalendarRsvpStatus) => {
    if (!calendarEvent) return;
    try {
      const result = await api(`/calendar/events/${calendarEvent.id}/rsvp`, {
        method: 'PUT',
        body: JSON.stringify({ status }),
      });
      applyResult(result);
      onChanged();
    } catch {
      setApiError(t('components.calendarEventForm.errors.rsvpFailed'));
    }
  };

  const toggleSubscription = async () => {
    if (!calendarEvent) return;
    try {
      const result = await api(`/calendar/events/${calendarEvent.id}/subscription`, {
        method: calendarEvent.isSubscribed ? 'DELETE' : 'POST',
      });
      applyResult(result);
      setSnackbar({
        status: 'success',
        message: t(
          calendarEvent.isSubscribed
            ? 'components.calendarEventForm.snackbar.unsubscribed'
            : 'components.calendarEventForm.snackbar.subscribed',
        ),
      });
    } catch {
      setApiError(t('components.calendarEventForm.errors.subscriptionFailed'));
    }
  };

  const reloadAfterConflict = async () => {
    setConflict(false);
    await loadEvent();
  };

  const startEdit = () => setMode('edit');

  const cancel = () => {
    if (mode === 'edit' && calendarEvent) {
      setValues(eventToFormValues(calendarEvent));
      setErrors({});
      setMode('view');
    } else {
      onClose();
    }
  };

  return {
    mode,
    calendarEvent,
    categories,
    values,
    errors,
    loading,
    saving,
    apiError,
    conflict,
    confirmDialog: {
      open: confirmOpen,
      message: confirmMessage,
      buttonText: t('components.calendarEventForm.confirm.deleteButton'),
      onClose: handleConfirmClose,
    },
    changeValues,
    clearError,
    dismissApiError: () => setApiError(null),
    save,
    deleteEvent,
    deleteOccurrence,
    reply,
    toggleSubscription,
    reloadAfterConflict,
    startEdit,
    cancel,
  };
}
