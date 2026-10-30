import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import { QRCodeSVG } from 'qrcode.react';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { useSnackbar } from '../hooks/useSnackbar';
import { PasswordField } from './PasswordField';

type Setup = { secret: string; otpAuthUri: string };

export function SecuritySettingsTab() {
  const { t } = useTranslation();
  const setSnackbar = useSnackbar();
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [setup, setSetup] = useState<Setup | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [disableOpen, setDisableOpen] = useState(false);
  const [password, setPassword] = useState('');
  const [setupPasswordOpen, setSetupPasswordOpen] = useState(false);
  const [setupPassword, setSetupPassword] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const data = await api('/users/account');
      setEnabled(!!data.twoFactorEnabled);
    } catch {
      setError(t('pages.userManagement.security.loadFailed'));
    }
  }, [t]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load().catch();
  }, [load]);

  const startSetup = async () => {
    setPasswordError('');
    setBusy(true);
    try {
      setSetup(
        await api('/users/account/2fa/setup', {
          method: 'POST',
          body: JSON.stringify({ password: setupPassword }),
        }),
      );
      setCode('');
      setSetupPasswordOpen(false);
      setSetupPassword('');
    } catch (err) {
      setPasswordError(
        err instanceof Error && err.message === 'Bad Request'
          ? t('pages.userManagement.security.passwordWrong')
          : t('pages.userManagement.security.setupFailed'),
      );
    } finally {
      setBusy(false);
    }
  };

  const confirmSetup = async () => {
    setError('');
    setBusy(true);
    try {
      const result = await api('/users/account/2fa/enable', {
        method: 'POST',
        body: JSON.stringify({ code: code.trim() }),
      });
      setRecoveryCodes(result.recoveryCodes);
      setSetup(null);
      setEnabled(true);
    } catch {
      setError(t('pages.userManagement.security.codeWrong'));
    } finally {
      setBusy(false);
    }
  };

  const disable = async () => {
    setError('');
    setBusy(true);
    try {
      await api('/users/account/2fa/disable', {
        method: 'POST',
        body: JSON.stringify({ password, code: code.trim() }),
      });
      setDisableOpen(false);
      setPassword('');
      setCode('');
      setEnabled(false);
      setSnackbar({ status: 'success', message: t('pages.userManagement.security.disabled') });
    } catch {
      setError(t('pages.userManagement.security.disableFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          px: 2,
          py: 1.5,
          bgcolor: 'action.hover',
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Typography fontWeight={700}>{t('pages.userManagement.security.title')}</Typography>
        {enabled !== null && (
          <Chip
            size="small"
            color={enabled ? 'success' : 'default'}
            label={
              enabled
                ? t('pages.userManagement.security.statusOn')
                : t('pages.userManagement.security.statusOff')
            }
          />
        )}
      </Box>

      <Box sx={{ p: { xs: 2, sm: 3 } }}>
        <Box
          sx={{
            display: 'flex',
            flexDirection: { xs: 'column', sm: 'row' },
            alignItems: { xs: 'stretch', sm: 'center' },
            justifyContent: 'space-between',
            gap: 2,
          }}
        >
          <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 720 }}>
            {t('pages.userManagement.security.description')}
          </Typography>

          {enabled === false && !setup && (
            <Button
              variant="contained"
              sx={{ flexShrink: 0 }}
              onClick={() => {
                setError('');
                setPasswordError('');
                setSetupPassword('');
                setSetupPasswordOpen(true);
              }}
              disabled={busy}
            >
              {t('pages.userManagement.security.enable')}
            </Button>
          )}

          {enabled && !setup && (
            <Button
              color="error"
              variant="outlined"
              sx={{ flexShrink: 0 }}
              onClick={() => {
                setError('');
                setCode('');
                setPassword('');
                setDisableOpen(true);
              }}
            >
              {t('pages.userManagement.security.disable')}
            </Button>
          )}
        </Box>

        {error && (
          <Alert severity="error" onClose={() => setError('')} sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}

        {setup && (
          <Box
            sx={{
              mt: 3,
              pt: 3,
              borderTop: 1,
              borderColor: 'divider',
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: 'auto 1fr' },
              gap: { xs: 3, md: 4 },
              alignItems: 'center',
            }}
          >
            <Box
              sx={{
                justifySelf: 'center',
                p: 2,
                bgcolor: '#fff',
                borderRadius: 2,
                border: 1,
                borderColor: 'divider',
                lineHeight: 0,
              }}
            >
              <QRCodeSVG value={setup.otpAuthUri} size={200} />
            </Box>

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 520 }}>
              <Typography variant="body2">{t('pages.userManagement.security.scanHint')}</Typography>
              <Box>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
                  {t('pages.userManagement.security.manualKey')}
                </Typography>
                <Box
                  sx={{
                    px: 1.5,
                    py: 1,
                    bgcolor: 'action.hover',
                    borderRadius: 1,
                  }}
                >
                  <Typography
                    sx={{ fontFamily: 'monospace', wordBreak: 'break-all', letterSpacing: 1 }}
                  >
                    {setup.secret}
                  </Typography>
                </Box>
              </Box>
              <TextField
                label={t('pages.userManagement.security.code')}
                value={code}
                onChange={(e) => setCode(e.target.value)}
                autoComplete="one-time-code"
                sx={{ maxWidth: { sm: 280 } }}
                slotProps={{ htmlInput: { inputMode: 'numeric', maxLength: 6 } }}
              />
              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                <Button
                  variant="contained"
                  onClick={confirmSetup}
                  disabled={busy || code.trim().length !== 6}
                >
                  {t('pages.userManagement.security.confirm')}
                </Button>
                <Button
                  onClick={() => {
                    setSetup(null);
                    setError('');
                  }}
                >
                  {t('pages.userManagement.buttons.cancel')}
                </Button>
              </Box>
            </Box>
          </Box>
        )}
      </Box>

      <Dialog open={recoveryCodes !== null} onClose={() => {}} disableEscapeKeyDown>
        <DialogTitle>{t('pages.userManagement.security.recoveryTitle')}</DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ mb: 2 }}>
            {t('pages.userManagement.security.recoveryHint')}
          </DialogContentText>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '1fr 1fr',
              gap: 1,
              fontFamily: 'monospace',
            }}
          >
            {recoveryCodes?.map((c) => (
              <Typography key={c} sx={{ fontFamily: 'monospace' }}>
                {c}
              </Typography>
            ))}
          </Box>
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button onClick={() => navigator.clipboard?.writeText(recoveryCodes?.join('\n') ?? '')}>
            {t('pages.userManagement.security.copy')}
          </Button>
          <Button variant="contained" onClick={() => setRecoveryCodes(null)}>
            {t('pages.userManagement.security.recoverySaved')}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={setupPasswordOpen}
        onClose={() => setSetupPasswordOpen(false)}
        fullWidth
        maxWidth="xs"
      >
        <Box
          component="form"
          onSubmit={(e: React.FormEvent) => {
            e.preventDefault();
            if (setupPassword && !busy) startSetup();
          }}
        >
          <DialogTitle>{t('pages.userManagement.security.passwordTitle')}</DialogTitle>
          <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            <DialogContentText>{t('pages.userManagement.security.passwordHint')}</DialogContentText>
            {passwordError && <Alert severity="error">{passwordError}</Alert>}
            <PasswordField
              autoFocus
              label={t('pages.userManagement.dialog.fields.currentPassword')}
              fullWidth
              autoComplete="current-password"
              value={setupPassword}
              onChange={(e) => setSetupPassword(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 50 } }}
            />
          </DialogContent>
          <DialogActions sx={{ p: 3 }}>
            <Button onClick={() => setSetupPasswordOpen(false)}>
              {t('pages.userManagement.buttons.cancel')}
            </Button>
            <Button type="submit" variant="contained" disabled={busy || !setupPassword}>
              {t('pages.userManagement.security.passwordContinue')}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      <Dialog open={disableOpen} onClose={() => setDisableOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>{t('pages.userManagement.security.disableTitle')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
          <DialogContentText>{t('pages.userManagement.security.disableHint')}</DialogContentText>
          {error && <Alert severity="error">{error}</Alert>}
          <PasswordField
            label={t('pages.userManagement.dialog.fields.currentPassword')}
            fullWidth
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 50 } }}
          />
          <TextField
            label={t('pages.userManagement.security.codeOrRecovery')}
            fullWidth
            value={code}
            onChange={(e) => setCode(e.target.value)}
            autoComplete="one-time-code"
            slotProps={{ htmlInput: { maxLength: 20 } }}
          />
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button onClick={() => setDisableOpen(false)}>
            {t('pages.userManagement.buttons.cancel')}
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={disable}
            disabled={busy || !password || !code.trim()}
          >
            {t('pages.userManagement.security.disable')}
          </Button>
        </DialogActions>
      </Dialog>
    </Paper>
  );
}
