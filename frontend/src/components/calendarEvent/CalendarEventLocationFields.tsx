import { Grid, Link, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { mapSearchUrl } from '../../utils/calendar';
import { CalendarEventFieldProps } from './formValues';

export function CalendarEventLocationFields({
  values,
  errors,
  onChange,
  onClearError,
}: CalendarEventFieldProps) {
  const { t } = useTranslation();
  const { locationName, locationAddress, locationUrl } = values;

  return (
    <>
      <Grid size={{ xs: 12, sm: 6 }}>
        <TextField
          fullWidth
          label={t('components.calendarEventForm.fields.locationName')}
          value={locationName}
          onChange={(e) => onChange({ locationName: e.target.value })}
          slotProps={{ htmlInput: { maxLength: 200 } }}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6 }}>
        <TextField
          fullWidth
          label={t('components.calendarEventForm.fields.locationAddress')}
          value={locationAddress}
          onChange={(e) => onChange({ locationAddress: e.target.value })}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          helperText={
            locationAddress.trim() ? (
              <Link href={mapSearchUrl(locationAddress)} target="_blank" rel="noopener noreferrer">
                {t('components.calendarEventForm.openMap')}
              </Link>
            ) : undefined
          }
        />
      </Grid>
      <Grid size={12}>
        <TextField
          fullWidth
          label={t('components.calendarEventForm.fields.locationUrl')}
          placeholder="https://"
          value={locationUrl}
          onChange={(e) => {
            onChange({ locationUrl: e.target.value });
            onClearError('locationUrl');
          }}
          error={!!errors.locationUrl}
          helperText={errors.locationUrl}
          slotProps={{ htmlInput: { maxLength: 1000 } }}
        />
      </Grid>
    </>
  );
}
