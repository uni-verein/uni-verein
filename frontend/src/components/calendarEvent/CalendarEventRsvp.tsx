import React from 'react';
import { Box, Chip, ToggleButton, ToggleButtonGroup, Tooltip, Typography } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HelpIcon from '@mui/icons-material/Help';
import CancelIcon from '@mui/icons-material/Cancel';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import { useTranslation } from 'react-i18next';
import { CalendarEventParticipant, CalendarRsvpStatus } from '../../types';

const rsvpIcons: Record<CalendarRsvpStatus, React.ReactElement> = {
  [CalendarRsvpStatus.ACCEPTED]: <CheckCircleIcon color="success" />,
  [CalendarRsvpStatus.TENTATIVE]: <HelpIcon color="warning" />,
  [CalendarRsvpStatus.DECLINED]: <CancelIcon color="error" />,
  [CalendarRsvpStatus.PENDING]: <HourglassEmptyIcon color="disabled" />,
};

const replyOptions = [
  CalendarRsvpStatus.ACCEPTED,
  CalendarRsvpStatus.TENTATIVE,
  CalendarRsvpStatus.DECLINED,
];

export function RsvpReply({
  value,
  onReply,
}: {
  value: CalendarRsvpStatus;
  onReply: (status: CalendarRsvpStatus) => void;
}) {
  const { t } = useTranslation();

  return (
    <Box>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        {t('components.calendarEventForm.rsvp.question')}
      </Typography>
      <ToggleButtonGroup
        exclusive
        size="small"
        value={value}
        onChange={(_, status: CalendarRsvpStatus | null) => status && onReply(status)}
        sx={{ flexWrap: 'wrap' }}
      >
        {replyOptions.map((status) => (
          <ToggleButton key={status} value={status} sx={{ gap: 0.75, px: 1.5 }}>
            {rsvpIcons[status]}
            {t(`components.calendarEventForm.rsvp.${status}`)}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
    </Box>
  );
}

export function ParticipantRsvpList({
  participants,
}: {
  participants: CalendarEventParticipant[];
}) {
  const { t } = useTranslation();
  const count = (status: CalendarRsvpStatus) =>
    participants.filter((p) => p.rsvpStatus === status).length;

  return (
    <Box>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
        {t('components.calendarEventForm.fields.participants')}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
        {t('components.calendarEventForm.rsvp.summary', {
          accepted: count(CalendarRsvpStatus.ACCEPTED),
          tentative: count(CalendarRsvpStatus.TENTATIVE),
          declined: count(CalendarRsvpStatus.DECLINED),
          pending: count(CalendarRsvpStatus.PENDING),
        })}
      </Typography>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        {participants.map((p) => (
          <Tooltip
            key={p.userId}
            title={t(`components.calendarEventForm.rsvp.${p.rsvpStatus}`)}
            arrow
          >
            <Chip
              icon={rsvpIcons[p.rsvpStatus]}
              label={`${p.username} · ${t(`components.calendarEventForm.roles.${p.role}`)}`}
              variant="outlined"
            />
          </Tooltip>
        ))}
      </Box>
    </Box>
  );
}
