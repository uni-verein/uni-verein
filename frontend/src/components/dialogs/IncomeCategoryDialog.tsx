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
import { IncomeCategory, TaxArea } from '../../types';
import { TaxAreaSelect } from '../finance/TaxAreaSelect';

export function IncomeCategoryDialog({
  category,
  onClose,
  onSaved,
  onError,
}: {
  category?: IncomeCategory;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [name, setName] = useState(category?.name ?? '');
  const [taxArea, setTaxArea] = useState<TaxArea>(category?.taxArea ?? TaxArea.NON_PROFIT);
  const [error, setError] = useState<string | undefined>();
  const setIncomeCategoryChange = useSnackbar();
  const { t } = useTranslation();

  const validate = () => {
    if (!name.trim()) {
      setError(t('pages.incomeCategoryConfig.validation.nameEmpty'));
      return false;
    }

    if (name.trim().length > 50) {
      setError(t('pages.incomeCategoryConfig.validation.nameTooLong'));
      return false;
    }

    return true;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      const response = await api(
        category ? `/income-categories/${category.id}` : '/income-categories',
        {
          method: category ? 'PUT' : 'POST',
          body: JSON.stringify({ name, taxArea }),
        },
      );

      if (response === 409) {
        onError(
          category
            ? t('pages.incomeCategoryConfig.apiError.updateExists')
            : t('pages.incomeCategoryConfig.apiError.createExists'),
        );
        setIncomeCategoryChange({
          status: 'error',
          message: t('pages.incomeCategoryConfig.snackbar.createExists'),
        });
      } else {
        setIncomeCategoryChange({
          status: 'success',
          message: category
            ? t('pages.incomeCategoryConfig.snackbar.updateSuccess')
            : t('pages.incomeCategoryConfig.snackbar.createSuccess'),
        });
      }
    } catch {
      onError(t('pages.incomeCategoryConfig.apiError.saveFailed'));
      setIncomeCategoryChange({
        status: 'error',
        message: t('pages.incomeCategoryConfig.snackbar.saveFailed'),
      });
    }
    onSaved();
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
      <DialogTitle>
        {category
          ? t('pages.incomeCategoryConfig.dialog.titleEdit')
          : t('pages.incomeCategoryConfig.dialog.titleCreate')}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.incomeCategoryConfig.dialog.nameLabel')}
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
        <Button onClick={onClose}>{t('pages.incomeCategoryConfig.dialog.cancel')}</Button>
        <Button variant="contained" onClick={handleSave}>
          {t('pages.incomeCategoryConfig.dialog.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
