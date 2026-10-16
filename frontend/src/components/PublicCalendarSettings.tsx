import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  ButtonProps,
  FormControlLabel,
  Link,
  Paper,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import RefreshIcon from '@mui/icons-material/Refresh';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { CalendarCategory, CalendarSettings } from '../types';
import { publicCalendarLinks } from '../utils/calendar';
import { CopyField } from './CopyField';
import { ConfirmDialog } from './dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';

export function PublicCalendarSettings({ categories }: { categories: CalendarCategory[] }) {
  const { t } = useTranslation();
  const setSnackbar = useSnackbar();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'warning' });
  const [settings, setSettings] = useState<CalendarSettings | null>(null);
  const [enabled, setEnabled] = useState(false);
  const [title, setTitle] = useState('');
  const [requireKey, setRequireKey] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const apply = (s: CalendarSettings) => {
    setSettings(s);
    setEnabled(s.publicCalendarEnabled);
    setTitle(s.publicCalendarTitle);
    setRequireKey(s.requireKey);
  };

  useEffect(() => {
    const load = async () => {
      try {
        const data: CalendarSettings | null = await api('/calendar/settings');
        if (data) apply(data);
      } catch {
        setError(t('pages.calendarCategoryConfig.publicCalendar.loadFailed'));
      }
    };
    load();
  }, [t]);

  const save = async (regenerateKey = false) => {
    if (regenerateKey) {
      setConfirmDialog({
        message: t('pages.calendarCategoryConfig.publicCalendar.confirmRegenerate'),
        buttonText: t('pages.calendarCategoryConfig.publicCalendar.regenerate'),
        confirmColor: 'warning',
      });
      if (!(await confirm())) return;
    }
    try {
      const data: CalendarSettings = await api('/calendar/settings', {
        method: 'PUT',
        body: JSON.stringify({
          publicCalendarEnabled: enabled,
          publicCalendarTitle: title,
          requireKey,
          regenerateKey,
        }),
      });
      apply(data);
      setSnackbar({
        status: 'success',
        message: t('pages.calendarCategoryConfig.publicCalendar.saved'),
      });
    } catch {
      setError(t('pages.calendarCategoryConfig.publicCalendar.saveFailed'));
    }
  };

  const links =
    settings?.publicCalendarEnabled === true
      ? publicCalendarLinks(settings.publicCalendarKey)
      : null;
  const publicCategories = categories.filter((c) => c.isPublic);

  return (
    <Paper variant="outlined" sx={{ p: { xs: 2, sm: 3 }, borderRadius: 2, mt: 4 }}>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
      />
      <Stack spacing={2}>
        <Typography variant="h6" fontWeight={700}>
          {t('pages.calendarCategoryConfig.publicCalendar.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('pages.calendarCategoryConfig.publicCalendar.description')}
        </Typography>

        <FormControlLabel
          control={<Switch checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />}
          label={t('pages.calendarCategoryConfig.publicCalendar.enabled')}
        />
        <TextField
          label={t('pages.calendarCategoryConfig.publicCalendar.calendarTitle')}
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          slotProps={{ htmlInput: { maxLength: 100 } }}
          sx={{ maxWidth: 480 }}
        />
        <Box>
          <FormControlLabel
            control={
              <Switch checked={requireKey} onChange={(e) => setRequireKey(e.target.checked)} />
            }
            label={t('pages.calendarCategoryConfig.publicCalendar.requireKey')}
          />
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
            {t('pages.calendarCategoryConfig.publicCalendar.requireKeyHelp')}
          </Typography>
        </Box>

        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Button variant="contained" startIcon={<SaveIcon />} onClick={() => save()}>
            {t('pages.calendarCategoryConfig.publicCalendar.save')}
          </Button>
          {settings?.requireKey && (
            <Button color="warning" startIcon={<RefreshIcon />} onClick={() => save(true)}>
              {t('pages.calendarCategoryConfig.publicCalendar.regenerate')}
            </Button>
          )}
        </Box>

        {links && (
          <>
            <Typography variant="body2">
              <Link href={links.page} target="_blank" rel="noopener noreferrer">
                {t('pages.calendarCategoryConfig.publicCalendar.openPage')}
              </Link>
            </Typography>
            <CopyField
              label={t('pages.calendarCategoryConfig.publicCalendar.pageUrl')}
              value={links.page}
            />
            <CopyField
              label={t('pages.calendarCategoryConfig.publicCalendar.feedUrl')}
              value={links.feedHttps}
            />
            {publicCategories.map((c) => (
              <CopyField
                key={c.id}
                label={t('pages.calendarCategoryConfig.publicCalendar.categoryFeedUrl', {
                  name: c.name,
                })}
                value={publicCalendarLinks(settings?.publicCalendarKey ?? null, c.id).feedHttps}
              />
            ))}
          </>
        )}

        {error && (
          <Alert severity="error" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}
      </Stack>
    </Paper>
  );
}
