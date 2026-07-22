import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { api } from '../../api';
import { ContributionPlans, Interval } from '../../types';
import { useSnackbar } from '../SnackbarContext';
import { useTranslation } from 'react-i18next';

export function ContributionPlanDialog({
  contributionPlan,
  onClose,
  onSaved,
  onError,
}: {
  contributionPlan: ContributionPlans | null;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [errors, setErrors] = useState<{ name?: string; amount?: string; interval?: string }>({});
  const setContributionChange = useSnackbar();
  const { t } = useTranslation();

  const euroRegex = /^\d*(?:[.,]\d{0,2})?$/;

  const [formData, setFormData] = useState(
    contributionPlan
      ? {
          name: contributionPlan.name,
          amount: contributionPlan.amount.toString().replace('.', ','),
          interval: contributionPlan.interval,
        }
      : { name: '', amount: '0', interval: '' },
  );

  const validate = () => {
    const newErrors: { name?: string; amount?: string; interval?: string } = {};

    if (!formData.name.trim()) {
      newErrors.name = t('pages.contributionPlanConfig.validation.nameEmpty');
    } else if (formData.name.length > 50) {
      newErrors.name = t('pages.contributionPlanConfig.validation.nameTooLong');
    }

    const amount = parseFloat(String(formData.amount).replace(',', '.'));
    if (!formData.amount.toString().trim()) {
      newErrors.amount = t('pages.contributionPlanConfig.validation.amountEmpty');
    } else if (isNaN(amount) || amount <= 0) {
      newErrors.amount = t('pages.contributionPlanConfig.validation.amountInvalid');
    }

    if (!formData.interval.trim()) {
      newErrors.interval = t('pages.contributionPlanConfig.validation.intervalEmpty');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    let data = {
      name: formData.name,
      amount: Number(formData.amount.replace(',', '.')),
      interval: formData.interval,
    };

    try {
      if (contributionPlan) {
        let response = await api(`/contribution-plans/${contributionPlan.id}`, {
          method: 'PATCH',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          onError(t('pages.contributionPlanConfig.apiError.updateExists'));
          setContributionChange({
            status: 'error',
            message: t('pages.contributionPlanConfig.snackbar.updateExists'),
          });
        } else {
          setContributionChange({
            status: 'success',
            message: t('pages.contributionPlanConfig.snackbar.updateSuccess'),
          });
        }
      } else {
        let response = await api('/contribution-plans', {
          method: 'POST',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          onError(t('pages.contributionPlanConfig.apiError.createExists'));
          setContributionChange({
            status: 'error',
            message: t('pages.contributionPlanConfig.snackbar.createExists'),
          });
        } else {
          setContributionChange({
            status: 'success',
            message: t('pages.contributionPlanConfig.snackbar.createSuccess'),
          });
        }
      }
    } catch (error) {
      onError(t('pages.contributionPlanConfig.apiError.saveFailed'));
      setContributionChange({
        status: 'error',
        message: t('pages.contributionPlanConfig.snackbar.saveFailed'),
      });
    }
    onSaved();
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
      <DialogTitle>
        {contributionPlan
          ? t('pages.contributionPlanConfig.dialog.titleEdit')
          : t('pages.contributionPlanConfig.dialog.titleCreate')}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.contributionPlanConfig.dialog.nameLabel')}
          fullWidth
          value={formData.name}
          error={errors.name !== undefined}
          helperText={errors.name ?? `${formData.name.length}/100`}
          onChange={(e) => {
            setFormData({ ...formData, name: e.target.value });
            setErrors({ ...errors, name: undefined });
          }}
        />
        <TextField
          type="text"
          label={t('pages.contributionPlanConfig.dialog.amountLabel')}
          fullWidth
          value={formData.amount.replace('.', ',')}
          error={errors.amount !== undefined}
          helperText={errors.amount}
          onChange={(e) => {
            const value = e.target.value;

            if (value === '' || euroRegex.test(value)) {
              setErrors({ ...errors, amount: undefined });
              setFormData({
                ...formData,
                amount: value.replace('.', ','),
              });
            } else {
              setErrors({
                ...errors,
                amount: t('pages.contributionPlanConfig.validation.amountFormat'),
              });
            }
          }}
        />
        <FormControl fullWidth>
          <InputLabel>{t('pages.contributionPlanConfig.dialog.intervalLabel')}</InputLabel>
          <Select
            error={errors.interval !== undefined}
            value={formData.interval || ''}
            label={t('pages.contributionPlanConfig.dialog.intervalLabel')}
            onChange={(e) => {
              setFormData({ ...formData, interval: e.target.value });
              setErrors({ ...errors, interval: undefined });
            }}
          >
            <MenuItem value={Interval.YEARLY}>
              {t('pages.contributionPlanConfig.dialog.intervalYearly')}
            </MenuItem>
            <MenuItem value={Interval.MONTHLY}>
              {t('pages.contributionPlanConfig.dialog.intervalMonthly')}
            </MenuItem>
          </Select>
          {errors.interval && <FormHelperText error={true}>{errors.interval}</FormHelperText>}
        </FormControl>
      </DialogContent>
      <DialogActions sx={{ p: 3 }}>
        <Button onClick={onClose}>{t('pages.contributionPlanConfig.dialog.cancel')}</Button>
        <Button variant="contained" onClick={handleSave}>
          {t('pages.contributionPlanConfig.dialog.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
