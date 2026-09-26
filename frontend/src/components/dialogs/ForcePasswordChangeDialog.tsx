import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  TextField,
} from '@mui/material';
import { api } from '../../api';
import { useTranslation } from 'react-i18next';

const DEFAULT_PASSWORD = 'admin123';

export function ForcePasswordChangeDialog({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const validate = () => {
    if (!password) return t('pages.login.forcePasswordChange.validation.empty');
    if (password.length < 11) return t('pages.login.forcePasswordChange.validation.tooShort');
    if (password.length > 50) return t('pages.login.forcePasswordChange.validation.tooLong');
    if (password === DEFAULT_PASSWORD)
      return t('pages.login.forcePasswordChange.validation.isDefault');
    if (password !== confirmPassword)
      return t('pages.login.forcePasswordChange.validation.mismatch');
    return '';
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();

    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }

    setSaving(true);
    setError('');
    try {
      await api('/users/account', { method: 'PATCH', body: JSON.stringify({ password }) });
      onChanged();
    } catch {
      setError(t('pages.login.forcePasswordChange.error'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onClose={() => {}} disableEscapeKeyDown fullWidth maxWidth="xs">
      <DialogTitle>{t('pages.login.forcePasswordChange.title')}</DialogTitle>
      <Box component="form" onSubmit={submit}>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <DialogContentText>{t('pages.login.forcePasswordChange.description')}</DialogContentText>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            autoFocus
            label={t('pages.login.forcePasswordChange.newPassword')}
            type="password"
            fullWidth
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 50 } }}
          />
          <TextField
            label={t('pages.login.forcePasswordChange.confirmPassword')}
            type="password"
            fullWidth
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 50 } }}
          />
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button type="submit" variant="contained" disabled={saving}>
            {t('pages.login.forcePasswordChange.save')}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  );
}
