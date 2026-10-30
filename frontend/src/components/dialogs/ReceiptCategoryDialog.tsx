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
import { useSnackbar } from '../../hooks/useSnackbar';
import { useTranslation } from 'react-i18next';
import { ReceiptCategory, TaxArea } from '../../types';
import { TaxAreaSelect } from '../finance/TaxAreaSelect';

export function ReceiptCategoryDialog({
  category,
  onClose,
  onSaved,
  onError,
}: {
  category?: ReceiptCategory;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [name, setName] = useState(category?.name ?? '');
  const [taxArea, setTaxArea] = useState<TaxArea>(category?.taxArea ?? TaxArea.NON_PROFIT);
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
      const response = await api(
        category ? `/receipt-categories/${category.id}` : '/receipt-categories',
        {
          method: category ? 'PUT' : 'POST',
          body: JSON.stringify({ name, taxArea }),
        },
      );

      if (response === 409) {
        onError(
          category
            ? t('pages.receiptCategoryConfig.apiError.updateExists')
            : t('pages.receiptCategoryConfig.apiError.createExists'),
        );
        setReceiptCategoryChange({
          status: 'error',
          message: t('pages.receiptCategoryConfig.snackbar.createExists'),
        });
      } else {
        setReceiptCategoryChange({
          status: 'success',
          message: category
            ? t('pages.receiptCategoryConfig.snackbar.updateSuccess')
            : t('pages.receiptCategoryConfig.snackbar.createSuccess'),
        });
      }
    } catch {
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
      <DialogTitle>
        {category
          ? t('pages.receiptCategoryConfig.dialog.titleEdit')
          : t('pages.receiptCategoryConfig.dialog.titleCreate')}
      </DialogTitle>
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
        <TaxAreaSelect value={taxArea} onChange={setTaxArea} />
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
