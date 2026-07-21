import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { api } from '../../api';
import { Role, User } from '../../types';
import { UUIDTypes } from 'uuid';
import { useSnackbar } from '../SnackbarContext';
import { useTranslation } from 'react-i18next';

export function UserDialog({
  user,
  currentUserId,
  accountView,
  onClose,
  onSaved,
  onError,
}: {
  user: User | null;
  currentUserId: UUIDTypes | undefined;
  accountView: boolean | undefined;
  onClose: () => void;
  onSaved: () => void;
  onError: (message: string) => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [formData, setFormData] = useState(
    user
      ? { username: user.username, email: user.email ?? '', password: '', role: user.role }
      : { username: '', email: '', password: '', role: Role.USER },
  );
  const [errors, setErrors] = useState<{ username?: string; email?: string; password?: string }>(
    {},
  );
  const setUserCreateOrUpdate = useSnackbar();
  const { t } = useTranslation();

  const validate = () => {
    const newErrors: { username?: string; email?: string; password?: string } = {};

    if (!formData.username.trim()) {
      newErrors.username = t('pages.userManagement.validation.usernameEmpty');
    } else if (formData.username.length > 50) {
      newErrors.username = t('pages.userManagement.validation.usernameTooLong');
    }

    if (!user && !formData.password) {
      newErrors.password = t('pages.userManagement.validation.passwordEmpty');
    } else if (
      (!user && formData.password.length < 11) ||
      (user && formData.password.length > 0 && formData.password.length < 11)
    ) {
      newErrors.password = t('pages.userManagement.validation.passwordTooShort');
    } else if (formData.password.length > 50) {
      newErrors.password = t('pages.userManagement.validation.passwordTooLong');
    }

    if (formData.email.trim() && formData.email.length > 50) {
      newErrors.email = t('pages.userManagement.validation.emailTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      if (user) {
        let response = await api(accountView ? `/users/account` : `/users/${user.id}`, {
          method: 'PATCH',
          body: JSON.stringify(formData),
        });
        if (response === 409) {
          onError(t('pages.userManagement.apiError.updateDuplicate'));
          setUserCreateOrUpdate({
            status: 'error',
            message: t('pages.userManagement.snackbar.usernameDuplicate'),
          });
        } else {
          setUserCreateOrUpdate({
            status: 'success',
            message: t('pages.userManagement.snackbar.updateSuccess'),
          });
        }
      } else {
        let response = await api('/users', { method: 'POST', body: JSON.stringify(formData) });
        if (response === 409) {
          onError(t('pages.userManagement.apiError.createDuplicate'));
          setUserCreateOrUpdate({
            status: 'error',
            message: t('pages.userManagement.snackbar.usernameDuplicate'),
          });
        } else {
          setUserCreateOrUpdate({
            status: 'success',
            message: t('pages.userManagement.snackbar.createSuccess'),
          });
        }
      }
      onSaved();
    } catch (e) {
      onError(t('pages.userManagement.apiError.saveFailed'));
      setUserCreateOrUpdate({
        status: 'error',
        message: t('pages.userManagement.snackbar.saveFailed'),
      });
    }
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="xs">
      <DialogTitle>
        {user
          ? t('pages.userManagement.dialog.titleEdit')
          : t('pages.userManagement.dialog.titleCreate')}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.userManagement.dialog.fields.username')}
          fullWidth
          value={formData.username}
          onChange={(e) => {
            setFormData({ ...formData, username: e.target.value });
            if (errors.username !== undefined) setErrors({ ...errors, username: undefined });
          }}
          error={!!errors.username}
          helperText={errors.username ?? `${formData.username.length}/50`}
          slotProps={{ htmlInput: { maxLength: 50 } }}
        />
        <TextField
          label={
            user
              ? t('pages.userManagement.dialog.fields.passwordEdit')
              : t('pages.userManagement.dialog.fields.passwordCreate')
          }
          type="password"
          fullWidth
          value={formData.password}
          autoComplete={'new-password'}
          onChange={(e) => {
            setFormData({ ...formData, password: e.target.value });
            if (errors.password !== undefined) setErrors({ ...errors, password: undefined });
          }}
          error={!!errors.password}
          helperText={errors.password ?? `${formData.password.length}/50`}
          slotProps={{ htmlInput: { maxLength: 50 } }}
        />
        <TextField
          sx={{ mt: 1 }}
          label={t('pages.userManagement.dialog.fields.email')}
          fullWidth
          value={formData.email}
          onChange={(e) => {
            setFormData({ ...formData, email: e.target.value });
            if (errors.email !== undefined) setErrors({ ...errors, email: undefined });
          }}
          error={!!errors.email}
          helperText={errors.email ?? `${formData.email !== null ? formData.email.length : 0}/50`}
          slotProps={{ htmlInput: { maxLength: 50 } }}
        />
        {!accountView && (
          <FormControl fullWidth>
            <InputLabel>{t('pages.userManagement.dialog.fields.role')}</InputLabel>
            <Select
              value={formData.role}
              label={t('pages.userManagement.dialog.fields.role')}
              onChange={(e) => setFormData({ ...formData, role: e.target.value as Role })}
              disabled={currentUserId === user?.id}
            >
              <MenuItem value={Role.USER}>{t('pages.userManagement.dialog.roles.user')}</MenuItem>
              <MenuItem value={Role.ADMIN}>{t('pages.userManagement.dialog.roles.admin')}</MenuItem>
              <MenuItem value={Role.FINANCIAL_MANAGER}>
                {t('pages.userManagement.dialog.roles.financialManager')}
              </MenuItem>
            </Select>
          </FormControl>
        )}
      </DialogContent>
      <DialogActions sx={{ p: 3 }}>
        <Button onClick={onClose}>{t('pages.userManagement.buttons.cancel')}</Button>
        <Button variant="contained" onClick={handleSave}>
          {t('pages.userManagement.buttons.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
