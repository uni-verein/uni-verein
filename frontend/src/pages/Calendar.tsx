import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Box,
  Button,
  FormControlLabel,
  Switch,
  Theme,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EventRepeatIcon from '@mui/icons-material/EventRepeat';
import PublicIcon from '@mui/icons-material/Public';
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import listPlugin from '@fullcalendar/list';
import interactionPlugin, { DateClickArg } from '@fullcalendar/interaction';
import {
  EventClickArg,
  EventContentArg,
  EventDropArg,
  EventInput,
  EventSourceFuncArg,
} from '@fullcalendar/core';
import { EventResizeDoneArg } from '@fullcalendar/interaction';
import dayjs, { Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import { UUIDTypes } from 'uuid';
import { api } from '../api';
import {
  CalendarCategory,
  CalendarEvent,
  CalendarEventOccurrence,
  CalendarRsvpStatus,
  CalendarSettings,
} from '../types';
import { CalendarFeedDialog } from '../components/dialogs/CalendarFeedDialog';
import CalendarEventForm from '../components/dialogs/CalendarEventForm';
import { CalendarCategoryChips } from '../components/CalendarCategoryChips';
import { CalendarHeader } from '../components/CalendarHeader';
import { PastEventsToggle } from '../components/PastEventsToggle';
import { useCalendarSx } from '../hooks/useCalendarSx';
import { usePastEvents } from '../hooks/usePastEvents';
import { useSnackbar } from '../hooks/useSnackbar';
import { allDayToIso, toggleId } from '../utils/calendar';
import { baseCalendarOptions, calendarLocale, occurrenceToEventInput } from '../utils/fullCalendar';

const FILTER_STORAGE_KEY = 'calendar.filters';
const DESKTOP_CALENDAR_MIN_WIDTH = 520;

const interactiveCalendarSx = (theme: Theme) => ({
  '& .fc .fc-daygrid-day, & .fc .fc-timegrid-slot': { cursor: 'pointer' },
  '& .fc .uv-rsvp-declined': { opacity: 0.45, textDecoration: 'line-through' },
  '& .fc .uv-rsvp-pending': {
    borderStyle: 'dashed',
    borderWidth: 2,
    borderColor: `${theme.palette.text.primary} !important`,
  },
  '& .fc .fc-timegrid-axis-cushion': { maxWidth: 'none', whiteSpace: 'nowrap' },
  '& .fc .fc-list-event-title': { minWidth: 0, overflowWrap: 'anywhere' },
  '& .fc .fc-timegrid-event-short .uv-event-content': { flexWrap: 'nowrap', alignItems: 'center' },
  '& .fc .fc-timegrid-event-short .uv-event-title': {
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
});

interface StoredFilters {
  categoryIds: string[];
  mineOnly: boolean;
}

function readStoredFilters(): StoredFilters {
  try {
    const raw = localStorage.getItem(FILTER_STORAGE_KEY);
    if (raw) return { categoryIds: [], mineOnly: false, ...JSON.parse(raw) };
  } catch {
    // ignore broken or blocked storage
  }
  return { categoryIds: [], mineOnly: false };
}

type DialogState =
  | { kind: 'closed' }
  | { kind: 'view'; eventId: string; occurrence?: { start: string; end: string } }
  | { kind: 'create'; start: Dayjs; end?: Dayjs; allDay: boolean };

export default function Calendar({
  userId,
  openEventRequest,
  onOpenEventRequestHandled,
}: {
  userId: UUIDTypes | undefined;
  openEventRequest?: { id: string; nonce: number } | null;
  onOpenEventRequestHandled?: () => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  // The calendar shares the row with the sidebar, so the full toolbar only fits on wide screens.
  const isCompact = useMediaQuery(theme.breakpoints.down('lg'), { noSsr: true });
  const { t, i18n } = useTranslation();
  const setSnackbar = useSnackbar();
  const calendarSx = useCalendarSx();
  const { showPast, pastCount, hidePast, toggle: togglePast } = usePastEvents();
  const calendarRef = useRef<FullCalendar>(null);
  const [categories, setCategories] = useState<CalendarCategory[]>([]);
  const [filters, setFilters] = useState<StoredFilters>(readStoredFilters);
  const [dialog, setDialog] = useState<DialogState>({ kind: 'closed' });
  const [feedDialogOpen, setFeedDialogOpen] = useState(false);
  const [settings, setSettings] = useState<CalendarSettings | null>(null);

  useEffect(() => {
    if (openEventRequest) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDialog({ kind: 'view', eventId: openEventRequest.id });
      onOpenEventRequestHandled?.();
    }
  }, [openEventRequest, onOpenEventRequestHandled]);

  useEffect(() => {
    const calendarApi = calendarRef.current?.getApi();
    if (isMobile && calendarApi?.view.type === 'timeGridWeek')
      calendarApi.changeView('timeGridDay');
  }, [isMobile]);

  useEffect(() => {
    try {
      localStorage.setItem(FILTER_STORAGE_KEY, JSON.stringify(filters));
    } catch {
      // ignore blocked storage
    }
  }, [filters]);

  useEffect(() => {
    const load = async () => {
      try {
        const data = await api('/calendar/categories');
        if (data) setCategories(data.items);
      } catch {
        // best-effort: filters just stay empty
      }
      try {
        setSettings(await api('/calendar/settings'));
      } catch {
        // best-effort: public calendar options stay hidden
      }
    };
    load();
  }, []);

  const toEventInput = useCallback(
    (o: CalendarEventOccurrence): EventInput => {
      const classNames =
        o.myRsvp === CalendarRsvpStatus.DECLINED
          ? ['uv-rsvp-declined']
          : o.myRsvp === CalendarRsvpStatus.PENDING
            ? ['uv-rsvp-pending']
            : [];
      return {
        ...occurrenceToEventInput(o, theme),
        classNames,
        groupId: o.eventId,
        // Single occurrences of a series cannot be moved, only the whole series in the dialog.
        editable: o.canEdit && !o.isRecurring,
      };
    },
    [theme],
  );

  const loadEvents = useCallback(
    async (
      info: EventSourceFuncArg,
      success: (events: EventInput[]) => void,
      failure: (error: Error) => void,
    ) => {
      const params = new URLSearchParams({
        from: info.start.toISOString(),
        to: info.end.toISOString(),
      });
      filters.categoryIds.forEach((id) => params.append('categoryIds', id));
      if (filters.mineOnly) params.set('mineOnly', 'true');

      try {
        const data = await api(`/calendar/events?${params.toString()}`);
        const items = (data?.items ?? []) as CalendarEventOccurrence[];
        const viewType = calendarRef.current?.getApi().view.type;
        success(hidePast(info, viewType, items).map(toEventInput));
      } catch (e) {
        failure(e as Error);
        setSnackbar({ status: 'error', message: t('pages.calendar.loadFailed') });
      }
    },
    [filters, hidePast, toEventInput, setSnackbar, t],
  );

  const refetch = useCallback(() => {
    calendarRef.current?.getApi().refetchEvents();
  }, []);

  const handleMove = async (info: EventDropArg | EventResizeDoneArg) => {
    const occurrence = info.event.extendedProps.occurrence as CalendarEventOccurrence;
    try {
      const current: CalendarEvent | null = await api(`/calendar/events/${occurrence.eventId}`);
      if (!current || !info.event.start) throw new Error('missing event');

      const allDay = info.event.allDay;
      const start = dayjs(info.event.start);
      const end = info.event.end
        ? dayjs(info.event.end)
        : allDay
          ? start.add(1, 'day')
          : start.add(1, 'hour');

      const result = await api(`/calendar/events/${current.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          title: current.title,
          description: current.description,
          start: allDay ? allDayToIso(start) : start.toISOString(),
          end: allDay ? allDayToIso(end) : end.toISOString(),
          allDay,
          locationName: current.locationName,
          locationAddress: current.locationAddress,
          locationUrl: current.locationUrl,
          visibility: current.visibility,
          categoryId: current.category?.id ?? null,
          isPublic: current.isPublic,
          recurrenceRule: current.recurrenceRule,
          reminderMinutesBefore: current.reminderMinutesBefore,
          participants: current.participants.map((p) => ({ userId: p.userId, role: p.role })),
          version: current.version,
        }),
      });

      if (result === 409) {
        info.revert();
        setSnackbar({ status: 'error', message: t('pages.calendar.moveConflict') });
        refetch();
        return;
      }
      setSnackbar({ status: 'success', message: t('pages.calendar.moved') });
      refetch();
    } catch {
      info.revert();
      setSnackbar({ status: 'error', message: t('pages.calendar.moveFailed') });
    }
  };

  const toggleCategory = (id: string) => {
    setFilters((f) => ({ ...f, categoryIds: toggleId(f.categoryIds, id) }));
  };

  return (
    <Box>
      <CalendarHeader
        title={t('pages.calendar.title')}
        actions={
          <>
            <Button
              variant="outlined"
              startIcon={<EventRepeatIcon />}
              onClick={() => setFeedDialogOpen(true)}
            >
              {t('pages.calendar.subscribe')}
            </Button>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() =>
                setDialog({
                  kind: 'create',
                  start: dayjs().add(1, 'hour').startOf('hour'),
                  allDay: false,
                })
              }
            >
              {t('pages.calendar.newEvent')}
            </Button>
          </>
        }
      />

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mb: 2 }}>
        <FormControlLabel
          control={
            <Switch
              checked={filters.mineOnly}
              onChange={(e) => setFilters((f) => ({ ...f, mineOnly: e.target.checked }))}
            />
          }
          label={t('pages.calendar.mineOnly')}
        />
        <CalendarCategoryChips
          categories={categories}
          selectedIds={filters.categoryIds}
          onToggle={toggleCategory}
        />
      </Box>

      <Box
        sx={[
          { minWidth: 0, maxWidth: '100%', overflowX: 'auto' },
          calendarSx,
          interactiveCalendarSx,
        ]}
      >
        <Box sx={{ minWidth: isMobile ? 0 : DESKTOP_CALENDAR_MIN_WIDTH }}>
          <PastEventsToggle showPast={showPast} count={pastCount} onToggle={togglePast} />
          <FullCalendar
            {...baseCalendarOptions}
            ref={calendarRef}
            plugins={[dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin]}
            locale={calendarLocale(i18n.language)}
            initialView={isMobile ? 'listMonth' : 'dayGridMonth'}
            headerToolbar={
              isCompact
                ? { left: 'prev,next', center: 'title', right: 'today' }
                : {
                    left: 'prev,next today',
                    center: 'title',
                    right: 'dayGridMonth,timeGridWeek,listMonth',
                  }
            }
            footerToolbar={
              isCompact
                ? {
                    center: isMobile
                      ? 'dayGridMonth,timeGridDay,listMonth'
                      : 'dayGridMonth,timeGridWeek,listMonth',
                  }
                : undefined
            }
            nowIndicator
            eventMinHeight={24}
            events={loadEvents}
            lazyFetching={false}
            eventClick={(info: EventClickArg) => {
              info.jsEvent.preventDefault();
              const occurrence = info.event.extendedProps.occurrence as CalendarEventOccurrence;
              setDialog({
                kind: 'view',
                eventId: occurrence.eventId,
                occurrence: occurrence.isRecurring
                  ? { start: occurrence.start, end: occurrence.end }
                  : undefined,
              });
            }}
            dateClick={(info: DateClickArg) =>
              setDialog({
                kind: 'create',
                start: dayjs(info.date),
                end: info.allDay ? dayjs(info.date) : dayjs(info.date).add(1, 'hour'),
                allDay: info.allDay,
              })
            }
            eventContent={(arg: EventContentArg) => {
              const occurrence = arg.event.extendedProps.occurrence as CalendarEventOccurrence;
              const wraps = arg.view.type === 'timeGridWeek' || arg.view.type === 'listMonth';
              return (
                <Box
                  component="span"
                  className="uv-event-content"
                  sx={{
                    display: 'flex',
                    width: '100%',
                    minWidth: 0,
                    alignItems: wraps ? 'flex-start' : 'center',
                    flexWrap: wraps ? 'wrap' : 'nowrap',
                    columnGap: 0.5,
                    overflow: 'hidden',
                  }}
                >
                  {arg.timeText && arg.view.type !== 'listMonth' && (
                    <Box
                      component="span"
                      sx={{ fontWeight: 600, whiteSpace: 'nowrap', flexShrink: 0 }}
                    >
                      {arg.timeText}
                    </Box>
                  )}
                  {occurrence.isPublic && (
                    <PublicIcon
                      sx={{ fontSize: '1em', flexShrink: 0 }}
                      titleAccess={t('pages.calendar.publicEvent')}
                    />
                  )}
                  <Box
                    component="span"
                    className="uv-event-title"
                    sx={
                      wraps
                        ? { minWidth: 0, whiteSpace: 'normal', overflowWrap: 'anywhere' }
                        : {
                            minWidth: 0,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }
                    }
                  >
                    {arg.event.title}
                  </Box>
                </Box>
              );
            }}
            eventDrop={handleMove}
            eventResize={handleMove}
          />
        </Box>
      </Box>

      {feedDialogOpen && (
        <CalendarFeedDialog settings={settings} onClose={() => setFeedDialogOpen(false)} />
      )}

      {dialog.kind !== 'closed' && (
        <CalendarEventForm
          key={dialog.kind === 'view' ? `${dialog.eventId}|${dialog.occurrence?.start}` : 'create'}
          eventId={dialog.kind === 'view' ? dialog.eventId : undefined}
          initialStart={dialog.kind === 'create' ? dialog.start : undefined}
          initialEnd={dialog.kind === 'create' ? dialog.end : undefined}
          initialAllDay={dialog.kind === 'create' ? dialog.allDay : undefined}
          occurrence={dialog.kind === 'view' ? dialog.occurrence : undefined}
          currentUserId={userId?.toString()}
          publicCalendarEnabled={settings?.publicCalendarEnabled ?? false}
          onClose={() => setDialog({ kind: 'closed' })}
          onChanged={refetch}
        />
      )}
    </Box>
  );
}
