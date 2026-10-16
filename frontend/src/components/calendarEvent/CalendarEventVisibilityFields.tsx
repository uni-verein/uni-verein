import {
  Box,
  FormControlLabel,
  Grid,
  MenuItem,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CalendarCategory, CalendarEventVisibility } from '../../types';
import { CalendarEventFormValues } from './formValues';

export function CalendarEventVisibilityFields({
  values,
  onChange,
  categories,
  isOwner,
  publicCalendarEnabled,
}: {
  values: CalendarEventFormValues;
  onChange: (patch: Partial<CalendarEventFormValues>) => void;
  categories: CalendarCategory[];
  isOwner: boolean;
  publicCalendarEnabled: boolean;
}) {
  const { t } = useTranslation();
  const { categoryId, visibility, isPublic } = values;

  return (
    <>
      <Grid size={{ xs: 12, sm: 6 }}>
        <TextField
          fullWidth
          select
          label={t('components.calendarEventForm.fields.category')}
          value={categoryId}
          onChange={(e) => onChange({ categoryId: e.target.value })}
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="">{t('components.calendarEventForm.noCategory')}</MenuItem>
          {categories.map((c) => (
            <MenuItem key={c.id} value={c.id}>
              <Box
                component="span"
                sx={{
                  display: 'inline-block',
                  width: 12,
                  height: 12,
                  borderRadius: '50%',
                  bgcolor: c.color,
                  mr: 1,
                }}
              />
              {c.name}
            </MenuItem>
          ))}
        </TextField>
      </Grid>
      <Grid size={{ xs: 12, sm: 6 }}>
        <TextField
          fullWidth
          select
          label={t('components.calendarEventForm.fields.visibility')}
          value={visibility}
          onChange={(e) => onChange({ visibility: e.target.value as CalendarEventVisibility })}
          helperText={t(`components.calendarEventForm.visibilityHelp.${visibility}`)}
        >
          {Object.values(CalendarEventVisibility)
            .filter((v) => v !== CalendarEventVisibility.PRIVATE || isOwner)
            .map((v) => (
              <MenuItem key={v} value={v}>
                {t(`components.calendarEventForm.visibility.${v}`)}
              </MenuItem>
            ))}
        </TextField>
      </Grid>
      {publicCalendarEnabled && visibility === CalendarEventVisibility.CLUB && (
        <Grid size={12}>
          <FormControlLabel
            control={
              <Switch
                checked={isPublic}
                onChange={(e) => onChange({ isPublic: e.target.checked })}
              />
            }
            label={t('components.calendarEventForm.fields.isPublic')}
          />
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
            {categories.find((c) => c.id === categoryId)?.isPublic
              ? t('components.calendarEventForm.publicByCategory')
              : t('components.calendarEventForm.publicHelp')}
          </Typography>
        </Grid>
      )}
    </>
  );
}
