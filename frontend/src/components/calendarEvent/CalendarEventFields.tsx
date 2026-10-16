import { Grid, MenuItem, TextField, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CalendarCategory, CalendarEventVisibility } from '../../types';
import { REMINDER_OPTIONS } from '../../utils/recurrence';
import { CalendarParticipantsEditor } from '../CalendarParticipantsEditor';
import { RecurrenceEditor } from '../RecurrenceEditor';
import { CalendarEventDateFields } from './CalendarEventDateFields';
import { CalendarEventLocationFields } from './CalendarEventLocationFields';
import { CalendarEventVisibilityFields } from './CalendarEventVisibilityFields';
import { CalendarEventFieldProps } from './formValues';

export function CalendarEventFields({
  autoFocus,
  categories,
  isOwner,
  ownerId,
  publicCalendarEnabled,
  ...fieldProps
}: CalendarEventFieldProps & {
  autoFocus: boolean;
  categories: CalendarCategory[];
  isOwner: boolean;
  ownerId: string | undefined;
  publicCalendarEnabled: boolean;
}) {
  const { t } = useTranslation();
  const { values, errors, onChange, onClearError } = fieldProps;

  return (
    <Grid container spacing={2}>
      <Grid size={12}>
        <TextField
          fullWidth
          required
          autoFocus={autoFocus}
          label={t('components.calendarEventForm.fields.title')}
          value={values.title}
          onChange={(e) => {
            onChange({ title: e.target.value });
            onClearError('title');
          }}
          error={!!errors.title}
          helperText={errors.title}
        />
      </Grid>
      <CalendarEventDateFields {...fieldProps} />
      <CalendarEventLocationFields {...fieldProps} />
      <Grid size={12}>
        <TextField
          fullWidth
          multiline
          minRows={3}
          label={t('components.calendarEventForm.fields.description')}
          value={values.description}
          onChange={(e) => onChange({ description: e.target.value })}
          slotProps={{ htmlInput: { maxLength: 10000 } }}
        />
      </Grid>
      <CalendarEventVisibilityFields
        values={values}
        onChange={onChange}
        categories={categories}
        isOwner={isOwner}
        publicCalendarEnabled={publicCalendarEnabled}
      />
      <Grid size={12}>
        <RecurrenceEditor
          value={values.recurrence}
          start={values.start}
          onChange={(recurrence) => onChange({ recurrence })}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6 }}>
        <TextField
          fullWidth
          select
          label={t('components.calendarEventForm.fields.reminder')}
          value={values.reminder}
          onChange={(e) =>
            onChange({ reminder: e.target.value === '' ? '' : Number(e.target.value) })
          }
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="">{t('components.calendarEventForm.reminders.none')}</MenuItem>
          {REMINDER_OPTIONS.map((minutes) => (
            <MenuItem key={minutes} value={minutes}>
              {t(`components.calendarEventForm.reminders.${minutes}`)}
            </MenuItem>
          ))}
        </TextField>
      </Grid>
      {values.visibility !== CalendarEventVisibility.PRIVATE && (
        <Grid size={12}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            {t('components.calendarEventForm.fields.participants')}
          </Typography>
          <CalendarParticipantsEditor
            participants={values.participants}
            ownerId={ownerId}
            onChange={(participants) => onChange({ participants })}
          />
        </Grid>
      )}
    </Grid>
  );
}
