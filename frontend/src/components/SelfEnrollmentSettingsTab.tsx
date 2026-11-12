import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControlLabel,
  IconButton,
  InputAdornment,
  Paper,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { useSnackbar } from '../hooks/useSnackbar';
import { WebPageConfigState } from '../types';
import RichTextEditor from './richText/RichTextEditor';

interface EnrollFormSettings {
  selfEnrollmentEnabled: boolean;
  legalText: string;
  embedAllowedOrigins: string;
}

export default function SelfEnrollmentSettingsTab({
  config,
  mailConfigured,
  onSaved,
}: {
  config: WebPageConfigState;
  mailConfigured: boolean;
  onSaved: () => Promise<void>;
}) {
  const { t } = useTranslation();
  const setSnackbar = useSnackbar();
  const [settings, setSettings] = useState<EnrollFormSettings | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const enrollmentUrl = `${window.location.origin}/enroll`;
  const embedSnippet = `<div id="uv-enroll"></div>\n<script src="${window.location.origin}/embed.js" data-target="#uv-enroll" async></script>`;
  const clubNameMissing = config.id === undefined;

  const loadSettings = useCallback(async () => {
    setLoadFailed(false);
    try {
      const data = await api('/enroll-form-settings');
      setSettings({
        selfEnrollmentEnabled: data?.selfEnrollmentEnabled === true,
        legalText: data?.legalText ?? '',
        embedAllowedOrigins: data?.embedAllowedOrigins ?? '',
      });
    } catch {
      setLoadFailed(true);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadSettings();
  }, [loadSettings, config.id]);

  const copy = async (text: string, successKey: string, errorKey: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setSnackbar({ status: 'success', message: t(successKey) });
    } catch {
      setSnackbar({ status: 'error', message: t(errorKey) });
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!settings) return;

    setSaving(true);
    try {
      const saved = await api('/enroll-form-settings', {
        method: 'PUT',
        body: JSON.stringify(settings),
      });
      if (saved && typeof saved === 'object') {
        setSettings({
          selfEnrollmentEnabled: saved.selfEnrollmentEnabled === true,
          legalText: saved.legalText ?? '',
          embedAllowedOrigins: saved.embedAllowedOrigins ?? '',
        });
      }
      setSnackbar({
        status: 'success',
        message: t('pages.generalConfig.snackbar.saveSuccess'),
      });
    } catch (error) {
      const invalidInput = error instanceof Error && error.message === 'Bad Request';
      setSnackbar({
        status: 'error',
        message: invalidInput
          ? t('pages.generalConfig.enrollForm.snackbar.invalidOrigins')
          : t('pages.generalConfig.snackbar.saveError'),
      });
    } finally {
      setSaving(false);
      await onSaved();
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: { xs: 2, md: 4 }, borderRadius: 3 }}>
      <form onSubmit={handleSave}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          {t('pages.generalConfig.selfEnrollment.description')}
        </Typography>
        {!mailConfigured && (
          <Alert severity="warning" sx={{ mb: 1, maxWidth: 480 }}>
            {t('pages.generalConfig.selfEnrollment.mailRequired')}
          </Alert>
        )}
        {clubNameMissing && (
          <Alert severity="info" sx={{ mb: 1, maxWidth: 480 }}>
            {t('pages.generalConfig.selfEnrollment.clubNameRequired')}
          </Alert>
        )}
        <Box>
          <FormControlLabel
            control={
              <Switch
                checked={settings?.selfEnrollmentEnabled ?? false}
                disabled={
                  !settings ||
                  clubNameMissing ||
                  (!mailConfigured && !settings.selfEnrollmentEnabled)
                }
                onChange={(e) =>
                  setSettings((prev) =>
                    prev ? { ...prev, selfEnrollmentEnabled: e.target.checked } : prev,
                  )
                }
                slotProps={{
                  input: {
                    role: 'switch',
                    'aria-label': t('pages.generalConfig.selfEnrollment.toggleLabel'),
                  },
                }}
              />
            }
            label={t('pages.generalConfig.selfEnrollment.toggleLabel')}
          />
        </Box>

        {loadFailed && (
          <Alert
            severity="error"
            sx={{ mt: 1, maxWidth: 480 }}
            action={
              <Button color="inherit" size="small" onClick={loadSettings}>
                {t('pages.generalConfig.enrollForm.retry')}
              </Button>
            }
          >
            {t('pages.generalConfig.enrollForm.loadError')}
          </Alert>
        )}

        {settings?.selfEnrollmentEnabled && (
          <Box sx={{ mt: 2 }}>
            <Box sx={{ maxWidth: 480 }}>
              <TextField
                label={t('pages.generalConfig.selfEnrollment.linkLabel')}
                fullWidth
                value={enrollmentUrl}
                slotProps={{
                  htmlInput: {
                    readOnly: true,
                    onFocus: (e: React.FocusEvent<HTMLInputElement>) => e.target.select(),
                  },
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <IconButton
                          onClick={() =>
                            copy(
                              enrollmentUrl,
                              'pages.generalConfig.snackbar.copySuccess',
                              'pages.generalConfig.snackbar.copyError',
                            )
                          }
                          aria-label={t('pages.generalConfig.selfEnrollment.copyButton')}
                          edge="end"
                        >
                          <ContentCopyIcon fontSize="small" />
                        </IconButton>
                      </InputAdornment>
                    ),
                  },
                }}
              />
            </Box>

            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mt: 4 }}>
                {t('pages.generalConfig.enrollForm.legalTitle')}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                {t('pages.generalConfig.enrollForm.legalDescription')}
              </Typography>
              <RichTextEditor
                value={settings.legalText}
                onChange={(html) =>
                  setSettings((prev) => (prev ? { ...prev, legalText: html } : prev))
                }
              />

              <Typography variant="subtitle2" sx={{ fontWeight: 600, mt: 4 }}>
                {t('pages.generalConfig.enrollForm.embedTitle')}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                {t('pages.generalConfig.enrollForm.embedDescription')}
              </Typography>
              <Box
                sx={{
                  display: 'flex',
                  flexDirection: { xs: 'column', md: 'row' },
                  alignItems: 'flex-start',
                  gap: 2,
                  '& > *': { flex: 1, minWidth: 0 },
                }}
              >
                <TextField
                  label={t('pages.generalConfig.enrollForm.originsLabel')}
                  helperText={t('pages.generalConfig.enrollForm.originsHelper')}
                  multiline
                  minRows={3}
                  fullWidth
                  value={settings.embedAllowedOrigins}
                  onChange={(e) =>
                    setSettings({ ...settings, embedAllowedOrigins: e.target.value })
                  }
                  placeholder="https://uni-verein.de"
                />
                <TextField
                  label={t('pages.generalConfig.enrollForm.snippetLabel')}
                  fullWidth
                  value={embedSnippet}
                  multiline
                  minRows={2}
                  slotProps={{
                    htmlInput: {
                      readOnly: true,
                      onFocus: (e: React.FocusEvent<HTMLInputElement>) => e.target.select(),
                    },
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            onClick={() =>
                              copy(
                                embedSnippet,
                                'pages.generalConfig.enrollForm.snackbar.copySuccess',
                                'pages.generalConfig.enrollForm.snackbar.copyError',
                              )
                            }
                            aria-label={t('pages.generalConfig.enrollForm.copySnippet')}
                            edge="end"
                          >
                            <ContentCopyIcon fontSize="small" />
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                />
              </Box>
            </>
          </Box>
        )}

        <Button
          type="submit"
          variant="contained"
          size="large"
          disabled={saving || clubNameMissing || !settings}
          startIcon={saving ? <CircularProgress size={20} color="inherit" /> : <SaveIcon />}
          sx={{ mt: 3, borderRadius: 2, px: 4, textTransform: 'none' }}
        >
          {saving
            ? t('pages.generalConfig.selfEnrollment.saving')
            : t('pages.generalConfig.selfEnrollment.save')}
        </Button>
      </form>
    </Paper>
  );
}
