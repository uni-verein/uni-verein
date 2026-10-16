import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import EventRepeatIcon from '@mui/icons-material/EventRepeat';
import ScheduleIcon from '@mui/icons-material/Schedule';
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import listPlugin from '@fullcalendar/list';
import { EventClickArg, EventInput, EventSourceFuncArg } from '@fullcalendar/core';
import { useTranslation } from 'react-i18next';
import { CalendarCategory, PublicCalendarOccurrence } from '../types';
import { CalendarCategoryChips } from '../components/CalendarCategoryChips';
import { CalendarHeader } from '../components/CalendarHeader';
import { LanguageToggle } from '../components/LanguageToggle';
import { PastEventsToggle } from '../components/PastEventsToggle';
import { PoweredBy } from '../components/PoweredBy';
import { DetailRow, EventLocationRows } from '../components/calendarEvent/EventDetailRows';
import { useCalendarSx } from '../hooks/useCalendarSx';
import { usePastEvents } from '../hooks/usePastEvents';
import { formatEventRange, publicCalendarLinks, toggleId } from '../utils/calendar';
import { baseCalendarOptions, calendarLocale, occurrenceToEventInput } from '../utils/fullCalendar';

interface PublicInfo {
  title: string;
  categories: CalendarCategory[];
}

// Read-only club calendar for visitors without a login, reachable at /calendar/public.
export default function PublicCalendar() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t, i18n } = useTranslation();
  const calendarSx = useCalendarSx();
  const { showPast, pastCount, hidePast, toggle: togglePast } = usePastEvents();
  const calendarRef = useRef<FullCalendar>(null);
  const [key] = useState(() => new URLSearchParams(window.location.search).get('key'));
  const [info, setInfo] = useState<PublicInfo | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [selected, setSelected] = useState<PublicCalendarOccurrence | null>(null);

  const keyQuery = key ? `key=${encodeURIComponent(key)}` : '';

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch(`/api/public/calendar/info${keyQuery ? `?${keyQuery}` : ''}`);
        if (!res.ok) {
          setNotFound(true);
          return;
        }
        setInfo(await res.json());
      } catch {
        setNotFound(true);
      }
    };
    load();
  }, [keyQuery]);

  useEffect(() => {
    if (info?.title) document.title = info.title;
  }, [info]);

  const loadEvents = useCallback(
    async (
      range: EventSourceFuncArg,
      success: (events: EventInput[]) => void,
      failure: (error: Error) => void,
    ) => {
      const params = new URLSearchParams({
        from: range.start.toISOString(),
        to: range.end.toISOString(),
      });
      if (key) params.set('key', key);
      selectedCategories.forEach((id) => params.append('categoryIds', id));
      try {
        const res = await fetch(`/api/public/calendar/events?${params.toString()}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        const items = data.items as PublicCalendarOccurrence[];
        const viewType = calendarRef.current?.getApi().view.type;
        success(hidePast(range, viewType, items).map((o) => occurrenceToEventInput(o, theme)));
      } catch (e) {
        failure(e as Error);
      }
    },
    [key, selectedCategories, hidePast, theme],
  );

  const links = useMemo(() => publicCalendarLinks(key), [key]);

  if (notFound) {
    return (
      <Container maxWidth="sm" sx={{ py: 8 }}>
        <Alert severity="info">{t('pages.publicCalendar.notAvailable')}</Alert>
      </Container>
    );
  }

  if (!info) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ bgcolor: 'background.default', minHeight: '100vh', py: { xs: 2, sm: 4 } }}>
      <Container maxWidth="lg">
        <Paper
          elevation={0}
          sx={{
            p: { xs: 1.5, sm: 4 },
            borderRadius: 3,
            border: '1px solid',
            borderColor: 'divider',
          }}
        >
          <CalendarHeader
            title={info.title || t('pages.publicCalendar.defaultTitle')}
            actions={
              <>
                <Button variant="contained" href={links.feedWebcal} startIcon={<EventRepeatIcon />}>
                  {t('pages.publicCalendar.subscribe')}
                </Button>
                <Button
                  variant="outlined"
                  href={links.google}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {t('pages.publicCalendar.google')}
                </Button>
                <LanguageToggle isMobile={isMobile} />
              </>
            }
          />

          {info.categories.length > 1 && (
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
              <CalendarCategoryChips
                categories={info.categories}
                selectedIds={selectedCategories}
                onToggle={(id) => setSelectedCategories((list) => toggleId(list, id))}
              />
            </Box>
          )}

          <Box sx={calendarSx}>
            <PastEventsToggle showPast={showPast} count={pastCount} onToggle={togglePast} />
            <FullCalendar
              {...baseCalendarOptions}
              ref={calendarRef}
              lazyFetching={false}
              plugins={[dayGridPlugin, listPlugin]}
              locale={calendarLocale(i18n.language)}
              initialView={isMobile ? 'listMonth' : 'dayGridMonth'}
              headerToolbar={
                isMobile
                  ? { left: 'prev,next', center: 'title', right: 'today' }
                  : { left: 'prev,next today', center: 'title', right: 'dayGridMonth,listMonth' }
              }
              events={loadEvents}
              eventClick={(arg: EventClickArg) => {
                arg.jsEvent.preventDefault();
                setSelected(arg.event.extendedProps.occurrence as PublicCalendarOccurrence);
              }}
            />
          </Box>
        </Paper>
        <PoweredBy sx={{ mt: 2 }} />
      </Container>

      {selected && (
        <Dialog
          open
          onClose={() => setSelected(null)}
          fullWidth
          maxWidth="sm"
          fullScreen={isMobile}
        >
          <DialogTitle sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
            {selected.title}
          </DialogTitle>
          <DialogContent>
            <Stack spacing={2}>
              {selected.categoryName && (
                <Box>
                  <Chip
                    size="small"
                    label={selected.categoryName}
                    sx={
                      selected.color
                        ? {
                            bgcolor: selected.color,
                            color: theme.palette.getContrastText(selected.color),
                          }
                        : undefined
                    }
                  />
                </Box>
              )}
              <DetailRow icon={<ScheduleIcon color="action" />}>
                <Typography>
                  {formatEventRange(selected.start, selected.end, selected.allDay, i18n.language)}
                </Typography>
              </DetailRow>
              <EventLocationRows
                locationName={selected.locationName}
                locationAddress={selected.locationAddress}
                locationUrl={selected.locationUrl}
              />
              {selected.description && (
                <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                  {selected.description}
                </Typography>
              )}
            </Stack>
          </DialogContent>
          <DialogActions sx={{ p: 2 }}>
            <Button onClick={() => setSelected(null)}>{t('pages.publicCalendar.close')}</Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  );
}
