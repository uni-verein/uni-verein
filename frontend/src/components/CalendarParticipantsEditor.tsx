import { useEffect, useState } from 'react';
import {
  Autocomplete,
  Box,
  IconButton,
  List,
  ListItem,
  ListItemText,
  MenuItem,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import PersonRemoveIcon from '@mui/icons-material/PersonRemove';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { CalendarParticipantRole, CalendarUser } from '../types';

export interface ParticipantDraft {
  userId: string;
  username: string;
  role: CalendarParticipantRole;
}

export function CalendarParticipantsEditor({
  participants,
  ownerId,
  disabled,
  onChange,
}: {
  participants: ParticipantDraft[];
  ownerId: string | undefined;
  disabled?: boolean;
  onChange: (participants: ParticipantDraft[]) => void;
}) {
  const { t } = useTranslation();
  const [users, setUsers] = useState<CalendarUser[]>([]);
  const [inputValue, setInputValue] = useState('');

  useEffect(() => {
    const load = async () => {
      try {
        const data = await api('/calendar/users');
        if (data) setUsers(data.items);
      } catch {
        // best-effort: the picker just offers no users if this fails
      }
    };
    load();
  }, []);

  const selectable = users.filter(
    (u) => u.id !== ownerId && !participants.some((p) => p.userId === u.id),
  );

  return (
    <Box>
      <Autocomplete
        options={selectable}
        disabled={disabled}
        getOptionLabel={(u) => u.username}
        value={null}
        inputValue={inputValue}
        onInputChange={(_, value) => setInputValue(value)}
        onChange={(_, user) => {
          if (!user) return;
          onChange([
            ...participants,
            { userId: user.id, username: user.username, role: CalendarParticipantRole.ATTENDEE },
          ]);
          setInputValue('');
        }}
        renderInput={(params) => (
          <TextField {...params} label={t('components.calendarEventForm.fields.addParticipant')} />
        )}
        noOptionsText={t('components.calendarEventForm.noUsers')}
      />
      {participants.length > 0 && (
        <List dense sx={{ mt: 1 }}>
          {participants.map((p) => (
            <ListItem
              key={p.userId}
              disableGutters
              secondaryAction={
                <Tooltip title={t('components.calendarEventForm.removeParticipant')}>
                  <span>
                    <IconButton
                      edge="end"
                      disabled={disabled}
                      onClick={() => onChange(participants.filter((x) => x.userId !== p.userId))}
                    >
                      <PersonRemoveIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              }
            >
              <ListItemText
                primary={<Typography noWrap>{p.username}</Typography>}
                sx={{ mr: 1, minWidth: 0 }}
              />
              <TextField
                select
                size="small"
                disabled={disabled}
                value={p.role}
                onChange={(e) =>
                  onChange(
                    participants.map((x) =>
                      x.userId === p.userId
                        ? { ...x, role: e.target.value as CalendarParticipantRole }
                        : x,
                    ),
                  )
                }
                sx={{ minWidth: 150, mr: 5 }}
              >
                <MenuItem value={CalendarParticipantRole.ATTENDEE}>
                  {t('components.calendarEventForm.roles.ATTENDEE')}
                </MenuItem>
                <MenuItem value={CalendarParticipantRole.EDITOR}>
                  {t('components.calendarEventForm.roles.EDITOR')}
                </MenuItem>
              </TextField>
            </ListItem>
          ))}
        </List>
      )}
    </Box>
  );
}
