import React, { useEffect, useState, useRef } from 'react';
import {
  Box,
  Typography,
  TextField,
  Button,
  Grid,
  Paper,
  CircularProgress,
  Alert,
  ButtonProps,
  Avatar,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import { api } from '../api';
import DeleteIcon from '@mui/icons-material/Delete';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
import { usePageConfig } from '../components/PageConfigContext';
import useMediaQuery from '@mui/material/useMediaQuery';

export default function GeneralConfig() {
  const { reloadConfig } = usePageConfig();
  const { t } = useTranslation();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [loading, setLoading] = useState(false);
  const [fetching, setFetching] = useState(true);
  const setConfigDeleteOrUpdate = useSnackbar();
  const [errors, setErrors] = useState<{ pageName?: string }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [config, setConfig] = useState({
    id: undefined,
    pageName: '',
    logo: '',
  });
  const fileInputRef = useRef<HTMLInputElement>(null);
  const isSmall = useMediaQuery('(max-width:1000px)');

  const loadConfig = async () => {
    try {
      const data = await api('/web-page-config');
      if (data) {
        setConfig(data);
      }
    } catch (error) {
      setConfig({
        id: undefined,
        pageName: '',
        logo: '',
      });
    } finally {
      setFetching(false);
    }
  };

  useEffect(() => {
    loadConfig();
  }, []);

  const validate = () => {
    const newErrors: { pageName?: string } = {};

    if (!config.pageName.trim()) {
      newErrors.pageName = t('pages.generalConfig.validation.pageNameEmpty');
    } else if (config.pageName.length > 50) {
      newErrors.pageName = t('pages.generalConfig.validation.pageNameTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d')!;
        const MAX_SIZE = 100;

        let width = img.width;
        let height = img.height;

        if (width > height) {
          if (width > MAX_SIZE) {
            height *= MAX_SIZE / width;
            width = MAX_SIZE;
          }
        } else {
          if (height > MAX_SIZE) {
            width *= MAX_SIZE / height;
            height = MAX_SIZE;
          }
        }

        canvas.width = width;
        canvas.height = height;
        ctx.drawImage(img, 0, 0, width, height);

        const base64 = canvas.toDataURL('image/jpeg', 0.8);
        setConfig((prev) => ({ ...prev, logo: base64 }));
      };
      img.src = reader.result as string;
    };
    reader.readAsDataURL(file);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    setLoading(true);
    try {
      await api('/web-page-config', {
        method: 'PUT',
        body: JSON.stringify(config),
      });
      setConfigDeleteOrUpdate({
        status: 'success',
        message: t('pages.generalConfig.snackbar.saveSuccess'),
      });
    } catch (error) {
      setApiError(t('pages.generalConfig.apiError.saveFailed'));
      setConfigDeleteOrUpdate({
        status: 'error',
        message: t('pages.generalConfig.snackbar.saveError'),
      });
    } finally {
      setLoading(false);
      await loadConfig();
      await reloadConfig();
    }
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.generalConfig.confirm.deleteMessage'),
      buttonText: t('pages.generalConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/web-page-config/${id}`, { method: 'DELETE' });
        await loadConfig();
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.generalConfig.snackbar.deleteSuccess'),
        });
      } catch (e) {
        setApiError(t('pages.generalConfig.apiError.deleteFailed'));
        setConfigDeleteOrUpdate({
          status: 'error',
          message: t('pages.generalConfig.snackbar.deleteError'),
        });
      }
    }
  };

  const handleLogoDelete = () => {
    setConfig((prev) => ({ ...prev, logo: '' }));
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

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
          {t('pages.generalConfig.title')}
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
            {t('pages.generalConfig.sectionTitle')}
          </Typography>

          <Grid container spacing={2}>
            <Grid size={isSmall ? 12 : 6}>
              <TextField
                label={t('pages.generalConfig.fields.pageName.label')}
                fullWidth
                value={config.pageName}
                onChange={(e) => {
                  setConfig({ ...config, pageName: e.target.value });
                  setErrors({ ...errors, pageName: undefined });
                }}
                placeholder={t('pages.generalConfig.fields.pageName.placeholder')}
                error={!!errors.pageName}
                helperText={errors.pageName ?? `${config.pageName.length}/50`}
              />
            </Grid>

            <Grid size={isSmall ? 12 : 6}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                {config.logo !== '' ? (
                  <Avatar
                    src={config.logo}
                    sx={{
                      m: 1,
                      bgcolor: 'transparent',
                      width: 56,
                      height: 56,
                      '& img': {
                        objectFit: 'contain',
                      },
                    }}
                    variant="square"
                  />
                ) : (
                  <Avatar src={config.logo} sx={{ width: 56, height: 56 }} variant="square" />
                )}

                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleImageUpload}
                  accept="image/*"
                  style={{ display: 'none' }}
                />
                <Button
                  variant="outlined"
                  onClick={() => fileInputRef.current?.click()}
                  sx={{ textTransform: 'none' }}
                >
                  {config.logo
                    ? t('pages.generalConfig.buttons.changeLogo')
                    : t('pages.generalConfig.buttons.uploadLogo')}
                </Button>
                {config.logo && (
                  <Button
                    variant="outlined"
                    color="error"
                    onClick={handleLogoDelete}
                    sx={{ textTransform: 'none' }}
                  >
                    {t('pages.generalConfig.buttons.deleteLogo')}
                  </Button>
                )}
              </Box>
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
                    ? t('pages.generalConfig.buttons.saving')
                    : t('pages.generalConfig.buttons.save')}
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
                    {t('pages.generalConfig.buttons.delete')}
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
