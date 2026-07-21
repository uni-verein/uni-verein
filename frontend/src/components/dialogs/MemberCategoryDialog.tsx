import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { api } from '../../api';
import { MemberCategory } from '../../types';
import { useSnackbar } from '../SnackbarContext';
import { useTranslation } from 'react-i18next';

export function MemberCategoryDialog({
  memberCategory,
  onClose,
  onSaved,
  onError,
}: {
  memberCategory: MemberCategory | null;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [errors, setErrors] = useState<{ name?: string; category?: string }>({});
  const setMemberCategoryChange = useSnackbar();
  const { t } = useTranslation();

  const [formData, setFormData] = useState(
    memberCategory
      ? {
          name: memberCategory.name,
          category: memberCategory.category.toString().replace('.', ','),
        }
      : { name: '', category: '' },
  );

  const validate = () => {
    const newErrors: { name?: string } = {};

    if (!formData.name.trim()) {
      newErrors.name = t('pages.memberCategoryConfig.validation.nameEmpty');
    } else if (formData.name.length > 50) {
      newErrors.name = t('pages.memberCategoryConfig.validation.nameTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    let data = {
      name: formData.name,
      category: formData.category,
    };

    try {
      if (memberCategory) {
        let response = await api(`/member-categories/${memberCategory.id}`, {
          method: 'PUT',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          onError(t('pages.memberCategoryConfig.apiError.updateExists'));
          setMemberCategoryChange({
            status: 'error',
            message: t('pages.memberCategoryConfig.snackbar.updateExists'),
          });
        } else {
          setMemberCategoryChange({
            status: 'success',
            message: t('pages.memberCategoryConfig.snackbar.updateSuccess'),
          });
        }
      } else {
        let response = await api('/member-categories', {
          method: 'POST',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          onError(t('pages.memberCategoryConfig.apiError.createExists'));
          setMemberCategoryChange({
            status: 'error',
            message: t('pages.memberCategoryConfig.snackbar.createExists'),
          });
        } else {
          setMemberCategoryChange({
            status: 'success',
            message: t('pages.memberCategoryConfig.snackbar.createSuccess'),
          });
        }
      }
    } catch (error) {
      onError(t('pages.memberCategoryConfig.apiError.saveFailed'));
      setMemberCategoryChange({
        status: 'error',
        message: t('pages.memberCategoryConfig.snackbar.saveFailed'),
      });
    }
    onSaved();
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
      <DialogTitle>
        {memberCategory
          ? t('pages.memberCategoryConfig.dialog.titleEdit')
          : t('pages.memberCategoryConfig.dialog.titleCreate')}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.memberCategoryConfig.dialog.nameLabel')}
          fullWidth
          required
          value={formData.name}
          error={errors.name !== undefined}
          helperText={errors.name ?? `${formData.name.length}/100`}
          onChange={(e) => {
            setFormData({
              ...formData,
              name: e.target.value,
              category: e.target.value.replace(' ', '_').trim().toUpperCase(),
            });
            setErrors({ ...errors, name: undefined });
          }}
        />
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.memberCategoryConfig.dialog.categoryLabel')}
          fullWidth
          value={formData.category}
          error={errors.category !== undefined}
          helperText={errors.category ?? `${formData.category.length}/100`}
          disabled={true}
        />
      </DialogContent>
      <DialogActions sx={{ p: 3 }}>
        <Button onClick={onClose}>{t('pages.memberCategoryConfig.dialog.cancel')}</Button>
        <Button variant="contained" onClick={handleSave}>
          {t('pages.memberCategoryConfig.dialog.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
