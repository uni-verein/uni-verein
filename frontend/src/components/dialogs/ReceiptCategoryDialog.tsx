import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { api } from '../../api';
import { useSnackbar } from '../SnackbarContext';
import { useTranslation } from 'react-i18next';

export function ReceiptCategoryDialog({
  onClose,
  onSaved,
  onError,
}: {
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [name, setName] = useState('');
  const [error, setError] = useState<string | undefined>();
  const setReceiptCategoryChange = useSnackbar();
  const { t } = useTranslation();

  const validate = () => {
    if (!name.trim()) {
      setError(t('pages.receiptCategoryConfig.validation.nameEmpty'));
      return false;
    }

    if (name.length > 50) {
      setError(t('pages.receiptCategoryConfig.validation.nameTooLong'));
      return false;
    }

    return true;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      const response = await api('/receipt-categories', {
        method: 'POST',
        body: JSON.stringify({ name }),
      });

      if (response === 409) {
        onError(t('pages.receiptCategoryConfig.apiError.createExists'));
        setReceiptCategoryChange({
          status: 'error',
          message: t('pages.receiptCategoryConfig.snackbar.createExists'),
        });
      } else {
        setReceiptCategoryChange({
          status: 'success',
          message: t('pages.receiptCategoryConfig.snackbar.createSuccess'),
        });
      }
    } catch (error) {
      onError(t('pages.receiptCategoryConfig.apiError.saveFailed'));
      setReceiptCategoryChange({
        status: 'error',
        message: t('pages.receiptCategoryConfig.snackbar.saveFailed'),
      });
    }
    onSaved();
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
      <DialogTitle>{t('pages.receiptCategoryConfig.dialog.titleCreate')}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.receiptCategoryConfig.dialog.nameLabel')}
          fullWidth
          required
          value={name}
          error={error !== undefined}
          helperText={error ?? `${name.length}/50`}
          onChange={(e) => {
            setName(e.target.value);
            setError(undefined);
          }}
        />
      </DialogContent>
      <DialogActions sx={{ p: 3 }}>
        <Button onClick={onClose}>{t('pages.receiptCategoryConfig.dialog.cancel')}</Button>
        <Button variant="contained" onClick={handleSave}>
          {t('pages.receiptCategoryConfig.dialog.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
