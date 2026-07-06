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
  Divider,
  ButtonProps,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import SendIcon from '@mui/icons-material/Send';
import { api } from '../api';
import { UUIDTypes } from 'uuid';
import DeleteIcon from '@mui/icons-material/Delete';
import { useConfirm } from '../hooks/useConfirm';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';

export default function EmailConfig() {
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
  const [errors, setErrors] = useState<{
    smtpServer?: string;
    port?: string;
    imapServer?: string;
    imapPort?: string;
    username?: string;
    password?: string;
    fromMail?: string;
  }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [config, setConfig] = useState({
    id: undefined,
    smtpServer: '',
    port: 587,
    imapServer: '',
    imapPort: 587,
    username: '',
    password: '',
    fromMail: '',
    enableSsl: true,
  });
  const [testEmail, setTestEmail] = useState('');
  const [testEmailError, setTestEmailError] = useState('');
  const [testLoading, setTestLoading] = useState(false);

  const loadConfig = async () => {
    try {
      const data = await api('/mail');
      if (data) {
        setConfig(data);
      }
    } catch (error) {
      setConfig({
        id: undefined,
        smtpServer: '',
        port: 587,
        imapServer: '',
        imapPort: 587,
        username: '',
        password: '',
        fromMail: '',
        enableSsl: true,
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
      smtpServer?: string;
      port?: string;
      imapServer?: string;
      imapPort?: string;
      username?: string;
      password?: string;
      fromMail?: string;
    } = {};

    if (!config.smtpServer.trim()) {
      newErrors.smtpServer = t('pages.emailConfig.validation.smtpServerEmpty');
    } else if (config.smtpServer.length > 50) {
      newErrors.smtpServer = t('pages.emailConfig.validation.smtpServerTooLong');
    }

    if (config.port < 1) {
      newErrors.port = t('pages.emailConfig.validation.portTooSmall');
    }

    if (!config.imapServer.trim()) {
      newErrors.imapServer = t('pages.emailConfig.validation.imapServerEmpty');
    } else if (config.imapServer.length > 50) {
      newErrors.imapServer = t('pages.emailConfig.validation.imapServerTooLong');
    }

    if (config.imapPort < 1) {
      newErrors.imapPort = t('pages.emailConfig.validation.imapPortTooSmall');
    }

    if (!config.username.trim()) {
      newErrors.username = t('pages.emailConfig.validation.usernameEmpty');
    } else if (config.username.length > 50) {
      newErrors.username = t('pages.emailConfig.validation.usernameTooLong');
    }

    if (!config.password.trim() && config.id === undefined) {
      newErrors.password = t('pages.emailConfig.validation.passwordEmpty');
    } else if (config.password.length > 50) {
      newErrors.password = t('pages.emailConfig.validation.passwordTooLong');
    }

    if (!config.fromMail.trim()) {
      newErrors.fromMail = t('pages.emailConfig.validation.fromMailEmpty');
    } else if (config.fromMail.length > 50) {
      newErrors.fromMail = t('pages.emailConfig.validation.fromMailTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    setLoading(true);
    try {
      await api('/mail', {
        method: 'PUT',
        body: JSON.stringify(config),
      });
      setConfigDeleteOrUpdate({
        status: 'success',
        message: t('pages.emailConfig.snackbar.saveSuccess'),
      });
    } catch (error) {
      setApiError(t('pages.emailConfig.apiError.saveFailed'));
      setConfigDeleteOrUpdate({
        status: 'error',
        message: t('pages.emailConfig.snackbar.saveError'),
      });
    } finally {
      setLoading(false);
      await loadConfig();
    }
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.emailConfig.confirm.deleteMessage'),
      buttonText: t('pages.emailConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/mail/${id}`, { method: 'DELETE' });
        await loadConfig();
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.emailConfig.snackbar.deleteSuccess'),
        });
      } catch (e) {
        setApiError(t('pages.emailConfig.apiError.deleteFailed'));
        setConfigDeleteOrUpdate({
          status: 'error',
          message: t('pages.emailConfig.snackbar.deleteError'),
        });
      }
    }
  };

  const handleTestEmail = async () => {
    if (!testEmail.trim()) {
      setTestEmailError(t('pages.emailConfig.validation.testEmailEmpty'));
      return;
    }

    setTestEmailError('');
    setTestLoading(true);

    try {
      await api('/mail/test', { method: 'POST', body: JSON.stringify({ email: testEmail }) });
      setConfigDeleteOrUpdate({
        status: 'success',
        message: t('pages.emailConfig.snackbar.testSuccess'),
      });
      setApiError(null);
    } catch (e) {
      setApiError(t('pages.emailConfig.apiError.testSendFailed'));
      setConfigDeleteOrUpdate({
        status: 'error',
        message: t('pages.emailConfig.snackbar.testError'),
      });
    } finally {
      setTestLoading(false);
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
          {t('pages.emailConfig.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('pages.emailConfig.description')}
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
            {t('pages.emailConfig.sectionSmtp')}
          </Typography>

          <Grid container spacing={2}>
            <Grid size={6}>
              <TextField
                label={t('pages.emailConfig.fields.smtpServer.label')}
                fullWidth
                required
                value={config.smtpServer}
                onChange={(e) => {
                  setConfig({ ...config, smtpServer: e.target.value });
                }}
                placeholder={t('pages.emailConfig.fields.smtpServer.placeholder')}
                error={!!errors.smtpServer}
                helperText={errors.smtpServer ?? `${config.smtpServer.length}/50`}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.emailConfig.fields.port.label')}
                type="number"
                fullWidth
                required
                value={config.port}
                onChange={(e) => setConfig({ ...config, port: parseInt(e.target.value) })}
                error={!!errors.port}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>
            <Grid size={12}>
              <Divider sx={{ my: 1 }} />
              <Typography variant="subtitle1" sx={{ mt: 2, mb: 1, fontWeight: 600 }}>
                {t('pages.emailConfig.sectionImap')}
              </Typography>
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.emailConfig.fields.imapServer.label')}
                fullWidth
                required
                value={config.imapServer}
                onChange={(e) => {
                  setConfig({ ...config, imapServer: e.target.value });
                }}
                placeholder={t('pages.emailConfig.fields.imapServer.placeholder')}
                error={!!errors.imapServer}
                helperText={errors.imapServer ?? `${config.imapServer.length}/50`}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.emailConfig.fields.imapPort.label')}
                type="number"
                fullWidth
                required
                value={config.imapPort}
                onChange={(e) => setConfig({ ...config, imapPort: parseInt(e.target.value) })}
                error={!!errors.imapPort}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>
            <Grid size={12}>
              <Divider sx={{ my: 1 }} />
              <Typography variant="subtitle1" sx={{ mt: 2, mb: 1, fontWeight: 600 }}>
                {t('pages.emailConfig.sectionAuth')}
              </Typography>
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.emailConfig.fields.username.label')}
                fullWidth
                required
                value={config.username}
                onChange={(e) => setConfig({ ...config, username: e.target.value })}
                error={!!errors.username}
                helperText={errors.username ?? `${config.username.length}/50`}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                label={t('pages.emailConfig.fields.fromMail.label')}
                fullWidth
                required
                helperText={`(${config.fromMail.length}/50) ${t('pages.emailConfig.fields.fromMail.helperText')}`}
                value={config.fromMail}
                onChange={(e) => setConfig({ ...config, fromMail: e.target.value })}
                placeholder={t('pages.emailConfig.fields.fromMail.placeholder')}
                error={!!errors.fromMail}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>
            <Grid size={12}>
              <TextField
                label={
                  config.id !== undefined
                    ? t('pages.emailConfig.fields.password.labelKeep')
                    : t('pages.emailConfig.fields.password.label')
                }
                type="password"
                fullWidth
                required={config.id === undefined}
                value={config.password}
                onChange={(e) => setConfig({ ...config, password: e.target.value })}
                error={!!errors.password}
                helperText={errors.password ?? `${config.password.length}/50`}
                slotProps={{ htmlInput: { maxLength: 50 } }}
              />
            </Grid>

            {config.id !== undefined && (
              <>
                <Grid size={12}>
                  <Divider sx={{ my: 2 }} />
                  <Typography variant="subtitle1" sx={{ mb: 2, fontWeight: 600 }}>
                    {t('pages.emailConfig.testMode.title')}
                  </Typography>
                </Grid>
                <Grid size={6}>
                  <TextField
                    label={t('pages.emailConfig.testMode.recipientEmail.label')}
                    fullWidth
                    value={testEmail}
                    onChange={(e) => setTestEmail(e.target.value)}
                    placeholder={t('pages.emailConfig.testMode.recipientEmail.placeholder')}
                    error={!!testEmailError}
                    helperText={testEmailError}
                    slotProps={{ htmlInput: { maxLength: 50 } }}
                  />
                </Grid>
                <Grid size={6} sx={{ display: 'flex', alignItems: 'center' }}>
                  <Button
                    variant="outlined"
                    size="large"
                    onClick={handleTestEmail}
                    disabled={!testEmail || testLoading}
                    startIcon={
                      testLoading ? <CircularProgress size={20} color="inherit" /> : <SendIcon />
                    }
                    sx={{ borderRadius: 2, textTransform: 'none', height: '56px' }}
                  >
                    {testLoading
                      ? t('pages.emailConfig.testMode.sending')
                      : t('pages.emailConfig.testMode.sendTest')}
                  </Button>
                </Grid>
              </>
            )}

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
                    ? t('pages.emailConfig.buttons.saving')
                    : t('pages.emailConfig.buttons.save')}
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
                    {t('pages.emailConfig.buttons.delete')}
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
