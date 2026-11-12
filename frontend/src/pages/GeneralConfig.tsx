import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  ButtonProps,
  CircularProgress,
  Paper,
  Tab,
  Tabs,
  Typography,
} from '@mui/material';
import { UUIDTypes } from 'uuid';
import { api } from '../api';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import ClubSettingsTab from '../components/ClubSettingsTab';
import SelfEnrollmentSettingsTab from '../components/SelfEnrollmentSettingsTab';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';
import { useTranslation } from 'react-i18next';
import { usePageConfig } from '../hooks/usePageConfig';
import { WebPageConfigState } from '../types';

const EMPTY_CONFIG: WebPageConfigState = {
  id: undefined,
  pageName: '',
  logo: '',
};

export default function GeneralConfig() {
  const { reloadConfig } = usePageConfig();
  const { t } = useTranslation();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [fetching, setFetching] = useState(true);
  const setConfigDeleteOrUpdate = useSnackbar();
  const [apiError, setApiError] = useState<string | null>(null);
  const [config, setConfig] = useState<WebPageConfigState>(EMPTY_CONFIG);
  const [mailConfigured, setMailConfigured] = useState(false);
  const [activeTab, setActiveTab] = useState<'club' | 'selfEnrollment'>('club');

  const loadConfig = useCallback(async () => {
    try {
      const data = await api('/web-page-config');
      if (data) {
        setConfig(data);
      }
    } catch {
      setConfig(EMPTY_CONFIG);
    } finally {
      setFetching(false);
    }
  }, []);

  const loadMailConfigured = useCallback(async () => {
    try {
      await api('/mail');
      setMailConfigured(true);
    } catch {
      setMailConfigured(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadConfig();

    loadMailConfigured();
  }, [loadConfig, loadMailConfigured]);

  const handleSaved = async () => {
    await loadConfig();
    await loadMailConfigured();
    await reloadConfig();
  };

  const handleReset = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.generalConfig.confirm.resetMessage'),
      buttonText: t('pages.generalConfig.confirm.resetButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/web-page-config/${id}`, { method: 'DELETE' });
        await loadConfig();
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.generalConfig.snackbar.resetSuccess'),
        });
      } catch {
        setApiError(t('pages.generalConfig.apiError.resetFailed'));
        setConfigDeleteOrUpdate({
          status: 'error',
          message: t('pages.generalConfig.snackbar.resetError'),
        });
      }
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

      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {t('pages.generalConfig.title')}
        </Typography>
      </Box>

      <Paper variant="outlined" sx={{ mb: 3, borderRadius: 2, overflow: 'hidden' }}>
        <Tabs
          value={activeTab}
          onChange={(_, newValue) => setActiveTab(newValue)}
          variant="scrollable"
          scrollButtons="auto"
          allowScrollButtonsMobile
        >
          <Tab value="club" label={t('pages.generalConfig.tabs.club')} />
          <Tab value="selfEnrollment" label={t('pages.generalConfig.tabs.selfEnrollment')} />
        </Tabs>
      </Paper>

      {apiError && (
        <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
          {apiError}
        </Alert>
      )}

      <Box sx={{ display: activeTab === 'club' ? 'block' : 'none' }}>
        <ClubSettingsTab
          config={config}
          onSaved={handleSaved}
          onReset={() => config.id !== undefined && handleReset(config.id)}
          onError={setApiError}
        />
      </Box>

      <Box sx={{ display: activeTab === 'selfEnrollment' ? 'block' : 'none' }}>
        <SelfEnrollmentSettingsTab
          config={config}
          mailConfigured={mailConfigured}
          onSaved={handleSaved}
        />
      </Box>
    </Box>
  );
}
