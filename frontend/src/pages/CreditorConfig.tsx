import React, { useEffect, useState } from 'react';
import {
  Box,
  Typography,
  TextField,
  Button,
  Grid,
  Paper,
  CircularProgress,
  Alert,
  MenuItem,
  ButtonProps,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import { api } from '../api';
import { formatBIC, formatIBAN, validateBIC, validateIBAN } from '../utils';
import * as countries from 'i18n-iso-countries';
import deLocale from 'i18n-iso-countries/langs/de.json';
import DeleteIcon from '@mui/icons-material/Delete';
import { UUIDTypes } from 'uuid';
import { useConfirm } from '../hooks/useConfirm';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
countries.registerLocale(deLocale);

const countryOptions = Object.entries(countries.getNames('de', { select: 'official' }))
  .map(([code, name]) => ({
    value: code,
    label: name,
  }))
  .sort((a, b) => a.label.localeCompare(b.label));

export default function CreditorConfig() {
  const { t } = useTranslation();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [loading, setLoading] = useState(false);
  const [fetching, setFetching] = useState(true);
  const setCreditorConfigChange = useSnackbar();
  const [apiError, setApiError] = useState<string | null>(null);
  const [errors, setErrors] = useState<{
    name?: string;
    iban?: string;
    bic?: string;
    creditorId?: string;
    streetNameAndNumber?: string;
    postCode?: string;
    cityName?: string;
    countryCode?: string;
  }>({});

  const [config, setConfig] = useState({
    id: undefined,
    name: '',
    iban: '',
    bic: '',
    creditorId: '',
    streetNameAndNumber: '',
    postCode: '',
    cityName: '',
    countryCode: '',
  });

  const loadConfig = async () => {
    setFetching(true);
    try {
      const data = await api('/creditor-config');
      if (data) {
        setConfig(data);
      }
    } catch (error) {
      setConfig({
        id: undefined,
        name: '',
        iban: '',
        bic: '',
        creditorId: '',
        streetNameAndNumber: '',
        postCode: '',
        cityName: '',
        countryCode: '',
      });
    } finally {
      setFetching(false);
    }
  };

  useEffect(() => {
    loadConfig();
  }, []);

  const validate = () => {
    const newErrors: {
      name?: string;
      iban?: string;
      bic?: string;
      creditorId?: string;
      streetNameAndNumber?: string;
      postCode?: string;
      cityName?: string;
      countryCode?: string;
    } = {};

    if (!config.name.trim()) {
      newErrors.name = t('pages.creditorConfig.validation.nameRequired');
    } else if (config.name.length > 100) {
      newErrors.name = t('pages.creditorConfig.validation.nameMaxLength');
    }

    if (!config.iban.trim()) {
      newErrors.iban = t('pages.creditorConfig.validation.ibanRequired');
    } else if (!validateIBAN(config.iban)) {
      newErrors.iban = t('pages.creditorConfig.validation.ibanInvalid');
    }

    if (!config.bic.trim()) {
      newErrors.bic = t('pages.creditorConfig.validation.bicRequired');
    } else if (!validateBIC(config.bic)) {
      newErrors.bic = t('pages.creditorConfig.validation.bicInvalid');
    }

    if (!config.creditorId.trim()) {
      newErrors.creditorId = t('pages.creditorConfig.validation.creditorIdRequired');
    } else if (config.creditorId.length > 50) {
      newErrors.creditorId = t('pages.creditorConfig.validation.creditorIdMaxLength');
    }

    if (config.streetNameAndNumber.length > 100) {
      newErrors.streetNameAndNumber = t('pages.creditorConfig.validation.streetMaxLength');
    }

    if (!config.postCode.trim()) {
      newErrors.postCode = t('pages.creditorConfig.validation.postCodeRequired');
    } else if (config.postCode.length > 10) {
      newErrors.postCode = t('pages.creditorConfig.validation.postCodeMaxLength');
    }

    if (!config.cityName.trim()) {
      newErrors.cityName = t('pages.creditorConfig.validation.cityRequired');
    } else if (config.cityName.length > 50) {
      newErrors.cityName = t('pages.creditorConfig.validation.cityMaxLength');
    }

    if (!config.countryCode.trim()) {
      newErrors.countryCode = t('pages.creditorConfig.validation.countryCodeRequired');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    setLoading(true);
    try {
      await api('/creditor-config', {
        method: 'PUT',
        body: JSON.stringify(config),
      });
      setCreditorConfigChange({
        status: 'success',
        message: t('pages.creditorConfig.snackbar.saveSuccess'),
      });
    } catch (error) {
      setApiError(t('pages.creditorConfig.apiError.saveFailed'));
      setCreditorConfigChange({
        status: 'error',
        message: t('pages.creditorConfig.snackbar.saveError'),
      });
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.creditorConfig.confirm.deleteMessage'),
      buttonText: t('pages.creditorConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/creditor-config/${id}`, { method: 'DELETE' });
        await loadConfig();
        setCreditorConfigChange({
          status: 'success',
          message: t('pages.creditorConfig.snackbar.deleteSuccess'),
        });
      } catch (e) {
        setApiError(t('pages.creditorConfig.apiError.deleteFailed'));
        setCreditorConfigChange({
          status: 'error',
          message: t('pages.creditorConfig.snackbar.deleteError'),
        });
      }
    }
  };

  const handleIbanChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value.replace(/\s+/g, '').toUpperCase();

    setConfig({
      ...config,
      iban: raw,
    });

    if (validateIBAN(raw)) {
      setErrors({ ...errors, iban: undefined });
    } else {
      setErrors({ ...errors, iban: t('pages.creditorConfig.validation.ibanInvalid') });
    }
  };

  const handleBicChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value.replace(/\s+/g, '').toUpperCase();

    setConfig({
      ...config,
      bic: raw,
    });

    if (validateBIC(raw)) {
      setErrors({ ...errors, bic: undefined });
    } else {
      setErrors({ ...errors, bic: t('pages.creditorConfig.validation.bicInvalid') });
    }
  };

  const formattedIBAN = formatIBAN(config.iban ?? '');
  const formattedBIC = formatBIC(config.bic ?? '');

  if (fetching) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
      />

      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {t('pages.creditorConfig.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('pages.creditorConfig.description')}
        </Typography>
      </Box>

      {apiError && (
        <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
          {apiError}
        </Alert>
      )}

      <Paper variant="outlined" sx={{ p: { xs: 2, md: 4 }, borderRadius: 3 }}>
        <form onSubmit={handleSave}>
          <Typography
            variant="subtitle1"
            sx={{ mb: 3, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 1 }}
          >
            {t('pages.creditorConfig.sectionTitle')}
          </Typography>

          <Grid container spacing={2}>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.name.label')}
                fullWidth
                required
                value={config.name}
                onChange={(e) => setConfig({ ...config, name: e.target.value })}
                placeholder={t('pages.creditorConfig.fields.name.placeholder')}
                error={errors.name !== undefined}
                helperText={errors.name ?? `${config.name.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.iban.label')}
                fullWidth
                required
                placeholder={t('pages.creditorConfig.fields.iban.placeholder')}
                value={formattedIBAN}
                onChange={handleIbanChange}
                error={errors.iban !== undefined}
                helperText={errors.iban}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.bic.label')}
                fullWidth
                required
                placeholder={t('pages.creditorConfig.fields.bic.placeholder')}
                value={formattedBIC}
                onChange={handleBicChange}
                error={errors.bic !== undefined}
                helperText={errors.bic}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.creditorId.label')}
                fullWidth
                required
                placeholder={t('pages.creditorConfig.fields.creditorId.placeholder')}
                value={config.creditorId}
                onChange={(e) => setConfig({ ...config, creditorId: e.target.value })}
                error={errors.creditorId !== undefined}
                helperText={errors.creditorId ?? `${config.creditorId.length}/50`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.streetNameAndNumber.label')}
                fullWidth
                required
                value={config.streetNameAndNumber}
                onChange={(e) => setConfig({ ...config, streetNameAndNumber: e.target.value })}
                error={errors.streetNameAndNumber !== undefined}
                helperText={
                  errors.streetNameAndNumber ?? `${config.streetNameAndNumber.length}/100`
                }
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.postCode.label')}
                fullWidth
                required
                value={config.postCode}
                onChange={(e) => setConfig({ ...config, postCode: e.target.value })}
                error={errors.postCode !== undefined}
                helperText={errors.postCode ?? `${config.postCode.length}/10`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.cityName.label')}
                fullWidth
                required
                value={config.cityName}
                onChange={(e) => setConfig({ ...config, cityName: e.target.value })}
                error={errors.cityName !== undefined}
                helperText={errors.cityName ?? `${config.cityName.length}/50`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.creditorConfig.fields.countryCode.label')}
                fullWidth
                required
                value={config.countryCode}
                onChange={(e) => setConfig({ ...config, countryCode: e.target.value })}
                select
                error={errors.countryCode !== undefined}
                helperText={errors.countryCode}
              >
                {countryOptions.map(({ value, label }) => (
                  <MenuItem key={value} value={value}>
                    {label} ({value})
                  </MenuItem>
                ))}
              </TextField>
            </Grid>

            <Grid size={12} sx={{ mt: 2 }}>
              <Box sx={{ display: 'flex', gap: 2, alignItems: 'flex-start' }}>
                <Button
                  type="submit"
                  variant="contained"
                  size="large"
                  disabled={loading}
                  startIcon={
                    loading ? <CircularProgress size={20} color="inherit" /> : <SaveIcon />
                  }
                  sx={{ borderRadius: 2, px: 4, textTransform: 'none' }}
                >
                  {loading
                    ? t('pages.creditorConfig.buttons.saving')
                    : t('pages.creditorConfig.buttons.save')}
                </Button>

                {config.id !== undefined && (
                  <Button
                    onClick={() => handleDelete(config.id!)}
                    variant="outlined"
                    color="error"
                    size="large"
                    startIcon={<DeleteIcon />}
                    sx={{ borderRadius: 2, px: 3, textTransform: 'none' }}
                  >
                    {t('pages.creditorConfig.buttons.delete')}
                  </Button>
                )}
              </Box>
            </Grid>
          </Grid>
        </form>
      </Paper>
    </Box>
  );
}
