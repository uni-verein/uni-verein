import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import ImageSearchIcon from '@mui/icons-material/ImageSearch';
import { api } from '../../api';
import { Link } from '../../types';
import { useSnackbar } from '../SnackbarContext';
import { IconPickerDialog } from './IconPickerDialog';
import { DynamicIcon } from '../muiIcons';
import { useTranslation } from 'react-i18next';

export function LinkDialog({
  link,
  onClose,
  onSaved,
  onError,
}: {
  link: Link | null;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [openIconPicker, setOpenIconPicker] = useState(false);
  const [errors, setErrors] = useState<{ link?: string; name?: string; icon?: string }>({});
  const setConfigDeleteOrUpdate = useSnackbar();
  const { t } = useTranslation();

  const [formData, setFormData] = useState(
    link ? { link: link.link, name: link.name, icon: link.icon } : { link: '', name: '', icon: '' },
  );

  const validate = () => {
    const newErrors: { link?: string; name?: string; icon?: string } = {};

    if (!formData.link.trim()) {
      newErrors.link = t('pages.linkConfig.validation.linkEmpty');
    } else if (formData.link.length > 100) {
      newErrors.link = t('pages.linkConfig.validation.linkTooLong');
    }

    if (!formData.name.trim()) {
      newErrors.name = t('pages.linkConfig.validation.nameEmpty');
    } else if (formData.name.length > 20) {
      newErrors.name = t('pages.linkConfig.validation.nameTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      if (link) {
        await api(`/link/${link.id}`, {
          method: 'PATCH',
          body: JSON.stringify(formData),
        });
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.linkConfig.snackbar.saveSuccess'),
        });
      } else {
        await api('/link', {
          method: 'POST',
          body: JSON.stringify(formData),
        });
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.linkConfig.snackbar.createSuccess'),
        });
      }
      onSaved();
    } catch {
      onError(t('pages.linkConfig.errors.saveFailed'));
    }
  };

  const handleIconSelect = (iconName: string) => {
    setFormData({ ...formData, icon: iconName });
    setErrors({ ...errors, icon: undefined });
  };

  return (
    <>
      <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
        <DialogTitle>
          {link ? t('pages.linkConfig.dialog.titleEdit') : t('pages.linkConfig.dialog.titleCreate')}
        </DialogTitle>

        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.linkConfig.fields.link.label')}
            fullWidth
            value={formData.link}
            error={!!errors.link}
            helperText={errors.link ?? `${formData.link.length}/100`}
            placeholder={t('pages.linkConfig.fields.link.placeholder')}
            onChange={(e) => {
              setFormData({ ...formData, link: e.target.value });
              setErrors({ ...errors, link: undefined });
            }}
          />

          <TextField
            sx={{ mt: 1 }}
            label={t('pages.linkConfig.fields.name.label')}
            fullWidth
            value={formData.name}
            error={!!errors.name}
            helperText={errors.name ?? `${formData.name.length}/20`}
            placeholder={t('pages.linkConfig.fields.name.placeholder')}
            onChange={(e) => {
              setFormData({ ...formData, name: e.target.value });
              setErrors({ ...errors, name: undefined });
            }}
          />

          <TextField
            label={t('pages.linkConfig.fields.icon.label')}
            fullWidth
            value={formData.icon}
            error={!!errors.icon}
            helperText={errors.icon}
            inputProps={{ readOnly: true }}
            onClick={() => setOpenIconPicker(true)}
            sx={{ cursor: 'pointer' }}
            InputProps={{
              startAdornment: formData.icon ? (
                <InputAdornment position="start">
                  <DynamicIcon name={formData.icon} fontSize="small" color="action" />
                </InputAdornment>
              ) : null,
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={() => setOpenIconPicker(true)} edge="end" size="small">
                    <ImageSearchIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
        </DialogContent>

        <DialogActions sx={{ p: 3 }}>
          <Button onClick={onClose}>{t('pages.linkConfig.dialog.cancel')}</Button>
          <Button variant="contained" onClick={handleSave}>
            {t('pages.linkConfig.dialog.save')}
          </Button>
        </DialogActions>
      </Dialog>

      <IconPickerDialog
        open={openIconPicker}
        selectedIcon={formData.icon}
        onSelect={handleIconSelect}
        onClose={() => setOpenIconPicker(false)}
      />
    </>
  );
}
