import {
  Box,
  MenuItem,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { Dayjs } from 'dayjs';
import 'dayjs/locale/de';
import { useTranslation } from 'react-i18next';
import { RecurrenceEnd, RecurrenceFrequency, RecurrenceState, WEEKDAYS } from '../utils/recurrence';

export function RecurrenceEditor({
  value,
  start,
  onChange,
}: {
  value: RecurrenceState;
  start: Dayjs;
  onChange: (value: RecurrenceState) => void;
}) {
  const { t } = useTranslation();
  const set = (patch: Partial<RecurrenceState>) => onChange({ ...value, ...patch });

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <TextField
          select
          label={t('components.recurrenceEditor.frequency')}
          value={value.freq}
          onChange={(e) => set({ freq: e.target.value as RecurrenceFrequency })}
          sx={{ minWidth: 200, flex: 1 }}
        >
          {(['NONE', 'DAILY', 'WEEKLY', 'MONTHLY', 'YEARLY'] as RecurrenceFrequency[]).map((f) => (
            <MenuItem key={f} value={f}>
              {t(`components.recurrenceEditor.frequencies.${f}`)}
            </MenuItem>
          ))}
        </TextField>
        {value.freq !== 'NONE' && (
          <TextField
            type="number"
            label={t('components.recurrenceEditor.interval')}
            value={value.interval}
            onChange={(e) =>
              set({ interval: Math.min(366, Math.max(1, parseInt(e.target.value, 10) || 1)) })
            }
            slotProps={{
              htmlInput: { min: 1, max: 366 },
              input: {
                endAdornment: (
                  <Typography variant="body2" color="text.secondary" sx={{ ml: 1 }}>
                    {t(`components.recurrenceEditor.units.${value.freq}`, {
                      count: value.interval,
                    })}
                  </Typography>
                ),
              },
            }}
            sx={{ width: 180 }}
          />
        )}
      </Box>

      {value.freq === 'WEEKLY' && (
        <ToggleButtonGroup
          size="small"
          value={value.byDay}
          onChange={(_, days: string[]) => days.length > 0 && set({ byDay: days })}
          aria-label={t('components.recurrenceEditor.weekdays')}
          sx={{ flexWrap: 'wrap' }}
        >
          {WEEKDAYS.map((d) => (
            <ToggleButton key={d} value={d} sx={{ minWidth: 44 }}>
              {t(`components.recurrenceEditor.weekdaysShort.${d}`)}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      )}

      {value.freq !== 'NONE' && (
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <TextField
            select
            label={t('components.recurrenceEditor.ends')}
            value={value.end}
            onChange={(e) => {
              const end = e.target.value as RecurrenceEnd;
              set({
                end,
                until: end === 'UNTIL' && !value.until ? start.add(3, 'month') : value.until,
              });
            }}
            sx={{ minWidth: 200, flex: 1 }}
          >
            {(['NEVER', 'UNTIL', 'COUNT'] as RecurrenceEnd[]).map((e) => (
              <MenuItem key={e} value={e}>
                {t(`components.recurrenceEditor.endOptions.${e}`)}
              </MenuItem>
            ))}
          </TextField>
          {value.end === 'UNTIL' && (
            <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
              <DatePicker
                label={t('components.recurrenceEditor.untilDate')}
                format="DD.MM.YYYY"
                value={value.until}
                minDate={start}
                onChange={(v) => v && set({ until: v })}
                slotProps={{ textField: { sx: { width: 200 } } }}
              />
            </LocalizationProvider>
          )}
          {value.end === 'COUNT' && (
            <TextField
              type="number"
              label={t('components.recurrenceEditor.count')}
              value={value.count}
              onChange={(e) =>
                set({ count: Math.min(1000, Math.max(1, parseInt(e.target.value, 10) || 1)) })
              }
              slotProps={{ htmlInput: { min: 1, max: 1000 } }}
              sx={{ width: 200 }}
            />
          )}
        </Box>
      )}
    </Box>
  );
}
