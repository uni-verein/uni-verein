import { useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Container,
  Paper,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';
import { useTranslation } from 'react-i18next';
import { login, submitSetup } from '../api';
import { PasswordField } from '../components/PasswordField';
import { LanguageToggle } from '../components/LanguageToggle';
import { PoweredBy } from '../components/PoweredBy';

const MIN_PASSWORD_LENGTH = 10;
const MAX_PASSWORD_LENGTH = 50;
const MAX_USERNAME_LENGTH = 50;
const MAX_EMAIL_LENGTH = 50;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function SetupPage({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [token, setToken] = useState('');
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const validate = () => {
    if (!token.trim()) return t('pages.setup.validation.tokenEmpty');
    if (!username.trim()) return t('pages.setup.validation.usernameEmpty');
    if (username.length > MAX_USERNAME_LENGTH) return t('pages.setup.validation.usernameTooLong');
    if (!email.trim()) return t('pages.setup.validation.emailEmpty');
    if (email.trim().length > MAX_EMAIL_LENGTH) return t('pages.setup.validation.emailTooLong');
    if (!EMAIL_PATTERN.test(email.trim())) return t('pages.setup.validation.emailInvalid');
    if (password.length < MIN_PASSWORD_LENGTH) return t('pages.setup.validation.passwordTooShort');
    if (password.length > MAX_PASSWORD_LENGTH) return t('pages.setup.validation.passwordTooLong');
    if (password !== confirmPassword) return t('pages.setup.validation.mismatch');
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
      const response = await submitSetup(token.trim(), username.trim(), email.trim(), password);
      if (response.status === 403) {
        setError(t('pages.setup.errors.invalidToken'));
        return;
      }
      if (response.status === 409) {
        setError(t('pages.setup.errors.alreadySetUp'));
        return;
      }
      if (response.status === 429) {
        setError(t('pages.setup.errors.tooManyRequests'));
        return;
      }
      if (!response.ok) {
        setError(t('pages.setup.errors.generic'));
        return;
      }

      await login(username.trim(), password);
      onDone();
    } catch {
      setError(t('pages.setup.errors.generic'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box
      sx={{
        position: 'relative',
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background:
          theme.palette.mode === 'dark'
            ? 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)'
            : 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)',
      }}
    >
      <Box
        sx={{
          position: 'fixed',
          top: 16,
          right: 16,
          zIndex: (th) => th.zIndex.modal + 1,
        }}
      >
        <LanguageToggle isMobile={isMobile} floating />
      </Box>
      <Container maxWidth="xs">
        <Paper
          elevation={6}
          sx={{
            p: 4,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            borderRadius: 3,
          }}
        >
          <Avatar
            sx={{ m: 1, bgcolor: 'transparent', color: 'text.secondary', width: 72, height: 72 }}
          >
            <ManageAccountsIcon fontSize="large" />
          </Avatar>

          <Typography
            component="h1"
            variant="h5"
            sx={{ fontWeight: 700, mt: 1, textAlign: 'center' }}
          >
            {t('pages.setup.title')}
          </Typography>
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ mt: 1, mb: 2, textAlign: 'center' }}
          >
            {t('pages.setup.hint')}
          </Typography>

          {error && (
            <Alert severity="error" sx={{ width: '100%', mb: 1 }}>
              {error}
            </Alert>
          )}

          <Box component="form" onSubmit={submit} sx={{ width: '100%' }}>
            <PasswordField
              margin="normal"
              required
              fullWidth
              autoFocus
              label={t('pages.setup.token')}
              autoComplete="off"
              value={token}
              onChange={(e) => setToken(e.target.value)}
            />
            <TextField
              margin="normal"
              required
              fullWidth
              label={t('pages.setup.username')}
              autoComplete="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              slotProps={{ htmlInput: { maxLength: MAX_USERNAME_LENGTH } }}
            />
            <TextField
              margin="normal"
              required
              fullWidth
              type="email"
              label={t('pages.setup.email')}
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              slotProps={{ htmlInput: { maxLength: MAX_EMAIL_LENGTH } }}
            />
            <PasswordField
              margin="normal"
              required
              fullWidth
              label={t('pages.setup.password')}
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              slotProps={{ htmlInput: { maxLength: MAX_PASSWORD_LENGTH } }}
            />
            <PasswordField
              margin="normal"
              required
              fullWidth
              label={t('pages.setup.confirmPassword')}
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              slotProps={{ htmlInput: { maxLength: MAX_PASSWORD_LENGTH } }}
            />
            <Button
              type="submit"
              fullWidth
              variant="contained"
              size="large"
              disabled={saving}
              sx={{
                mt: 3,
                mb: 1,
                py: 1.5,
                borderRadius: 2,
                textTransform: 'none',
                fontSize: '1rem',
                fontWeight: 600,
              }}
            >
              {saving ? t('pages.setup.submitting') : t('pages.setup.submit')}
            </Button>
          </Box>
        </Paper>
        <PoweredBy sx={{ mt: 2 }} />
      </Container>
    </Box>
  );
}
