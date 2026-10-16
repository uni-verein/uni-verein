import { useState } from 'react';
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Switch,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { api } from '../../api';
import { useSnackbar } from '../../hooks/useSnackbar';
import { useTranslation } from 'react-i18next';
import { CalendarCategory } from '../../types';

export function CalendarCategoryDialog({
  category,
  onClose,
  onSaved,
  onError,
}: {
  category?: CalendarCategory;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [name, setName] = useState(category?.name ?? '');
  const [color, setColor] = useState(category?.color ?? '#2563eb');
  const [isPublic, setIsPublic] = useState(category?.isPublic ?? false);
  const [error, setError] = useState<string | undefined>();
  const setSnackbar = useSnackbar();
  const { t } = useTranslation();

  const validate = () => {
    if (!name.trim()) {
      setError(t('pages.calendarCategoryConfig.validation.nameEmpty'));
      return false;
    }
    if (name.length > 50) {
      setError(t('pages.calendarCategoryConfig.validation.nameTooLong'));
      return false;
    }
    return true;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      const response = await api(
        category ? `/calendar/categories/${category.id}` : '/calendar/categories',
        {
          method: category ? 'PUT' : 'POST',
          body: JSON.stringify({ name: name.trim(), color, isPublic }),
        },
      );

      if (response === 409) {
        onError(t('pages.calendarCategoryConfig.apiError.exists'));
        setSnackbar({
          status: 'error',
          message: t('pages.calendarCategoryConfig.apiError.exists'),
        });
      } else {
        setSnackbar({
          status: 'success',
          message: t('pages.calendarCategoryConfig.snackbar.saveSuccess'),
        });
      }
    } catch {
      onError(t('pages.calendarCategoryConfig.apiError.saveFailed'));
      setSnackbar({
        status: 'error',
        message: t('pages.calendarCategoryConfig.apiError.saveFailed'),
      });
    }
    onSaved();
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
      <DialogTitle>
        {category
          ? t('pages.calendarCategoryConfig.dialog.titleEdit')
          : t('pages.calendarCategoryConfig.dialog.titleCreate')}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.calendarCategoryConfig.dialog.nameLabel')}
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
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <TextField
            type="color"
            label={t('pages.calendarCategoryConfig.dialog.colorLabel')}
            value={color}
            onChange={(e) => setColor(e.target.value)}
            sx={{ width: 120 }}
          />
          <Box
            sx={{
              px: 1.5,
              py: 0.5,
              borderRadius: 1,
              bgcolor: color,
              color: theme.palette.getContrastText(color),
              fontWeight: 600,
            }}
          >
            {name || t('pages.calendarCategoryConfig.dialog.preview')}
          </Box>
        </Box>
        <Box>
          <FormControlLabel
            control={<Switch checked={isPublic} onChange={(e) => setIsPublic(e.target.checked)} />}
            label={t('pages.calendarCategoryConfig.dialog.isPublic')}
          />
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
            {t('pages.calendarCategoryConfig.dialog.isPublicHelp')}
          </Typography>
        </Box>
      </DialogContent>
      <DialogActions sx={{ p: 3 }}>
        <Button onClick={onClose}>{t('pages.calendarCategoryConfig.dialog.cancel')}</Button>
        <Button variant="contained" onClick={handleSave}>
          {t('pages.calendarCategoryConfig.dialog.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
