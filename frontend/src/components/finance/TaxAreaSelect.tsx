import { MenuItem, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { TaxArea } from '../../types';

export function TaxAreaSelect({
  value,
  onChange,
}: {
  value: TaxArea;
  onChange: (value: TaxArea) => void;
}) {
  const { t } = useTranslation();

  return (
    <TextField
      select
      fullWidth
      label={t('components.taxArea.label')}
      helperText={t('components.taxArea.hint')}
      value={value}
      onChange={(e) => onChange(e.target.value as TaxArea)}
    >
      {Object.values(TaxArea).map((area) => (
        <MenuItem key={area} value={area}>
          {t(`components.taxArea.${area}`)}
        </MenuItem>
      ))}
    </TextField>
  );
}
