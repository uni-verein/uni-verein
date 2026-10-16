import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  ButtonProps,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Link,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import EventRepeatIcon from '@mui/icons-material/EventRepeat';
import LinkOffIcon from '@mui/icons-material/LinkOff';
import { useTranslation } from 'react-i18next';
import { api } from '../../api';
import { CalendarSettings } from '../../types';
import { publicCalendarLinks } from '../../utils/calendar';
import { CopyField } from '../CopyField';
import { ConfirmDialog } from './ConfirmDialog';
import { useConfirm } from '../../hooks/useConfirm';

interface FeedStatus {
  exists: boolean;
  createdAt: string | null;
  lastUsedAt: string | null;
}

export function CalendarFeedDialog({
  settings,
  onClose,
}: {
  settings: CalendarSettings | null;
  onClose: () => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t, i18n } = useTranslation();
  const { open: confirmOpen, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [status, setStatus] = useState<FeedStatus | null>(null);
  const [feedPath, setFeedPath] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadStatus = useCallback(async () => {
    try {
      setStatus(await api('/calendar/feed/token'));
    } catch {
      setError(t('components.calendarFeedDialog.errors.loadFailed'));
    }
  }, [t]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadStatus();
  }, [loadStatus]);

  const createToken = async () => {
    if (status?.exists) {
      setConfirmDialog({
        message: t('components.calendarFeedDialog.confirm.regenerate'),
        buttonText: t('components.calendarFeedDialog.buttons.regenerate'),
        confirmColor: 'warning',
      });
      if (!(await confirm())) return;
    }
    setBusy(true);
    try {
      const result = await api('/calendar/feed/token', { method: 'POST' });
      setFeedPath(result.path);
      await loadStatus();
    } catch {
      setError(t('components.calendarFeedDialog.errors.createFailed'));
    } finally {
      setBusy(false);
    }
  };

  const revokeToken = async () => {
    setConfirmDialog({
      message: t('components.calendarFeedDialog.confirm.revoke'),
      buttonText: t('components.calendarFeedDialog.buttons.revoke'),
      confirmColor: 'error',
    });
    if (!(await confirm())) return;
    setBusy(true);
    try {
      await api('/calendar/feed/token', { method: 'DELETE' });
      setFeedPath(null);
      await loadStatus();
    } catch {
      setError(t('components.calendarFeedDialog.errors.revokeFailed'));
    } finally {
      setBusy(false);
    }
  };

  const feedHttps = feedPath ? `${window.location.origin}/api${feedPath}` : null;
  const feedWebcal = feedHttps?.replace(/^https?:/, 'webcal:');
  const locale = i18n.language.startsWith('de') ? 'de-DE' : 'en-GB';
  const publicLinks =
    settings?.publicCalendarEnabled === true
      ? publicCalendarLinks(settings.publicCalendarKey)
      : null;

  return (
    <>
      <ConfirmDialog
        open={confirmOpen}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
      />
      <Dialog open onClose={onClose} fullWidth fullScreen={isMobile} maxWidth="sm">
        <DialogTitle sx={{ fontWeight: 700 }}>
          {t('components.calendarFeedDialog.title')}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {t('components.calendarFeedDialog.personal.title')}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t('components.calendarFeedDialog.personal.description')}
            </Typography>

            {status === null ? (
              <Box sx={{ display: 'flex', justifyContent: 'center' }}>
                <CircularProgress size={24} />
              </Box>
            ) : (
              <>
                {status.exists && !feedPath && (
                  <Alert severity="info">
                    {t('components.calendarFeedDialog.personal.active', {
                      date: status.createdAt
                        ? new Date(status.createdAt).toLocaleString(locale)
                        : '',
                    })}
                    {status.lastUsedAt &&
                      ' ' +
                        t('components.calendarFeedDialog.personal.lastUsed', {
                          date: new Date(status.lastUsedAt).toLocaleString(locale),
                        })}
                  </Alert>
                )}

                {feedHttps && feedWebcal && (
                  <>
                    <Alert severity="warning">
                      {t('components.calendarFeedDialog.personal.secret')}
                    </Alert>
                    <CopyField
                      label={t('components.calendarFeedDialog.personal.url')}
                      value={feedHttps}
                    />
                    <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                      <Button variant="contained" href={feedWebcal} startIcon={<EventRepeatIcon />}>
                        {t('components.calendarFeedDialog.buttons.openInApp')}
                      </Button>
                      <Button
                        variant="outlined"
                        href={`https://calendar.google.com/calendar/r?cid=${encodeURIComponent(feedWebcal)}`}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        {t('components.calendarFeedDialog.buttons.google')}
                      </Button>
                    </Box>
                  </>
                )}

                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <Button
                    variant={feedHttps ? 'text' : 'contained'}
                    onClick={createToken}
                    disabled={busy}
                  >
                    {status.exists
                      ? t('components.calendarFeedDialog.buttons.regenerate')
                      : t('components.calendarFeedDialog.buttons.create')}
                  </Button>
                  {status.exists && (
                    <Button
                      color="error"
                      startIcon={<LinkOffIcon />}
                      onClick={revokeToken}
                      disabled={busy}
                    >
                      {t('components.calendarFeedDialog.buttons.revoke')}
                    </Button>
                  )}
                </Box>
              </>
            )}

            <Box component="ul" sx={{ m: 0, pl: 2.5, color: 'text.secondary' }}>
              {(['apple', 'google', 'outlook'] as const).map((app) => (
                <Typography key={app} component="li" variant="body2">
                  {t(`components.calendarFeedDialog.howTo.${app}`)}
                </Typography>
              ))}
            </Box>

            {publicLinks && (
              <>
                <Divider />
                <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                  {t('components.calendarFeedDialog.public.title')}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t('components.calendarFeedDialog.public.description')}{' '}
                  <Link href={publicLinks.page} target="_blank" rel="noopener noreferrer">
                    {t('components.calendarFeedDialog.public.openPage')}
                  </Link>
                </Typography>
                <CopyField
                  label={t('components.calendarFeedDialog.public.pageUrl')}
                  value={publicLinks.page}
                />
                <CopyField
                  label={t('components.calendarFeedDialog.public.feedUrl')}
                  value={publicLinks.feedHttps}
                />
              </>
            )}

            {error && (
              <Alert severity="error" onClose={() => setError(null)}>
                {error}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button onClick={onClose} color="inherit" startIcon={<CloseIcon />}>
            {t('components.calendarFeedDialog.buttons.close')}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
