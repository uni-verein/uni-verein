import { FormControlLabel, Grid, Switch } from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { DateTimePicker } from '@mui/x-date-pickers/DateTimePicker';
import { Dayjs } from 'dayjs';
import 'dayjs/locale/de';
import { useTranslation } from 'react-i18next';
import { CalendarEventFieldProps } from './formValues';

export function CalendarEventDateFields({
  values,
  errors,
  onChange,
  onClearError,
}: CalendarEventFieldProps) {
  const { t } = useTranslation();
  const { allDay, start, end } = values;

  const changeEnd = (v: Dayjs | null) => {
    if (!v) return;
    onChange({ end: v });
    onClearError('end');
  };

  const endTextField = {
    fullWidth: true,
    required: true,
    error: !!errors.end,
    helperText: errors.end,
  };

  return (
    <>
      <Grid size={12}>
        <FormControlLabel
          control={
            <Switch
              checked={allDay}
              onChange={(e) => {
                const checked = e.target.checked;
                onChange(
                  !checked && !end.isAfter(start)
                    ? { allDay: checked, end: start.add(1, 'hour') }
                    : { allDay: checked },
                );
              }}
            />
          }
          label={t('components.calendarEventForm.fields.allDay')}
        />
      </Grid>
      <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
        <Grid size={{ xs: 12, sm: 6 }}>
          {allDay ? (
            <DatePicker
              label={t('components.calendarEventForm.fields.start')}
              format="DD.MM.YYYY"
              value={start}
              onChange={(v) => {
                if (!v) return;
                onChange(end.isBefore(v, 'day') ? { start: v, end: v } : { start: v });
              }}
              slotProps={{ textField: { fullWidth: true, required: true } }}
            />
          ) : (
            <DateTimePicker
              label={t('components.calendarEventForm.fields.start')}
              format="DD.MM.YYYY HH:mm"
              ampm={false}
              value={start}
              onChange={(v) => {
                if (!v) return;
                const duration = end.diff(start);
                onChange({
                  start: v,
                  end: v.add(duration > 0 ? duration : 3600000, 'millisecond'),
                });
              }}
              slotProps={{ textField: { fullWidth: true, required: true } }}
            />
          )}
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          {allDay ? (
            <DatePicker
              label={t('components.calendarEventForm.fields.end')}
              format="DD.MM.YYYY"
              value={end}
              minDate={start}
              onChange={changeEnd}
              slotProps={{ textField: endTextField }}
            />
          ) : (
            <DateTimePicker
              label={t('components.calendarEventForm.fields.end')}
              format="DD.MM.YYYY HH:mm"
              ampm={false}
              value={end}
              minDateTime={start}
              onChange={changeEnd}
              slotProps={{ textField: endTextField }}
            />
          )}
        </Grid>
      </LocalizationProvider>
    </>
  );
}
