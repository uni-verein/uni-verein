import { Box, Chip, Stack, Typography, useTheme } from '@mui/material';
import ScheduleIcon from '@mui/icons-material/Schedule';
import PeopleIcon from '@mui/icons-material/People';
import LockIcon from '@mui/icons-material/Lock';
import RepeatIcon from '@mui/icons-material/Repeat';
import AlarmIcon from '@mui/icons-material/Alarm';
import PublicIcon from '@mui/icons-material/Public';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { CalendarEvent, CalendarEventVisibility, CalendarRsvpStatus } from '../../types';
import { formatEventRange } from '../../utils/calendar';
import { describeRule } from '../../utils/recurrence';
import { ParticipantRsvpList, RsvpReply } from './CalendarEventRsvp';
import { DetailRow, EventLocationRows } from './EventDetailRows';

export function CalendarEventDetails({
  event: e,
  occurrence,
  onReply,
}: {
  event: CalendarEvent;
  occurrence?: { start: string; end: string };
  onReply: (status: CalendarRsvpStatus) => void;
}) {
  const theme = useTheme();
  const { t, i18n } = useTranslation();

  return (
    <Stack spacing={2}>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
        {e.category && (
          <Chip
            size="small"
            label={e.category.name}
            sx={{
              bgcolor: e.category.color,
              color: theme.palette.getContrastText(e.category.color),
            }}
          />
        )}
        <Chip
          size="small"
          variant="outlined"
          icon={e.visibility === CalendarEventVisibility.PRIVATE ? <LockIcon /> : <PeopleIcon />}
          label={t(`components.calendarEventForm.visibility.${e.visibility}`)}
        />
        {e.isPubliclyVisible && (
          <Chip
            size="small"
            color="info"
            variant="outlined"
            icon={<PublicIcon />}
            label={t('components.calendarEventForm.publicChip')}
          />
        )}
      </Box>

      <DetailRow icon={<ScheduleIcon color="action" />}>
        <Typography>
          {occurrence
            ? formatEventRange(occurrence.start, occurrence.end, e.allDay, i18n.language)
            : formatEventRange(e.start, e.end, e.allDay, i18n.language)}
        </Typography>
      </DetailRow>

      {e.recurrenceRule && (
        <DetailRow icon={<RepeatIcon color="action" />}>
          <Typography>
            {describeRule(e.recurrenceRule, dayjs(e.start), t, i18n.language)}
            {e.recurrenceExceptions.length > 0 &&
              ' · ' +
                t('components.calendarEventForm.exceptionsCount', {
                  count: e.recurrenceExceptions.length,
                })}
          </Typography>
        </DetailRow>
      )}

      {e.reminderMinutesBefore !== null && (
        <DetailRow icon={<AlarmIcon color="action" />}>
          <Typography>
            {t(`components.calendarEventForm.reminders.${e.reminderMinutesBefore}`, {
              defaultValue: t('components.calendarEventForm.reminders.custom', {
                minutes: e.reminderMinutesBefore,
              }),
            })}
          </Typography>
        </DetailRow>
      )}

      <EventLocationRows
        locationName={e.locationName}
        locationAddress={e.locationAddress}
        locationUrl={e.locationUrl}
      />

      {e.description && (
        <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
          {e.description}
        </Typography>
      )}

      {e.myRsvp && <RsvpReply value={e.myRsvp} onReply={onReply} />}

      {e.participants.length > 0 && <ParticipantRsvpList participants={e.participants} />}

      <Typography variant="caption" color="text.secondary">
        {t('components.calendarEventForm.createdBy', { name: e.owner.username })}
        {e.lastModifiedBy &&
          e.version > 1 &&
          ' · ' +
            t('components.calendarEventForm.lastModifiedBy', {
              name: e.lastModifiedBy.username,
              date: new Date(e.updatedAt).toLocaleString(
                i18n.language.startsWith('de') ? 'de-DE' : 'en-GB',
              ),
            })}
      </Typography>
    </Stack>
  );
}
