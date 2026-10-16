import { IconButton, InputAdornment, TextField, Tooltip } from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { useTranslation } from 'react-i18next';
import { useSnackbar } from '../hooks/useSnackbar';

export function CopyField({ label, value }: { label: string; value: string }) {
  const { t } = useTranslation();
  const setSnackbar = useSnackbar();

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setSnackbar({ status: 'success', message: t('components.copyField.copied') });
    } catch {
      setSnackbar({ status: 'error', message: t('components.copyField.copyFailed') });
    }
  };

  return (
    <TextField
      fullWidth
      size="small"
      label={label}
      value={value}
      onFocus={(e) => e.target.select()}
      slotProps={{
        htmlInput: { readOnly: true },
        input: {
          endAdornment: (
            <InputAdornment position="end">
              <Tooltip title={t('components.copyField.copy')}>
                <IconButton edge="end" onClick={copy} aria-label={t('components.copyField.copy')}>
                  <ContentCopyIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </InputAdornment>
          ),
        },
      }}
    />
  );
}
