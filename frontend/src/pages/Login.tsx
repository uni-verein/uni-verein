import { useEffect, useState } from 'react';
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Container,
  Avatar,
  InputAdornment,
  IconButton,
  Alert,
  Divider,
} from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import PersonIcon from '@mui/icons-material/Person';
import { login } from '../api';
import { useTranslation } from 'react-i18next';
import { usePageConfig } from '../components/PageConfigContext';
import { DemoDialog } from '../components/DemoDialog';

export default function Login({
  onLogin,
  demo,
}: {
  onLogin: () => void;
  demo: boolean | undefined;
}) {
  const { config } = usePageConfig();
  const [user, setUser] = useState('');
  const [pass, setPass] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [countdown, setCountdown] = useState<number | null>(null);
  const { t } = useTranslation();

  useEffect(() => {
    if (countdown === null || countdown <= 0) {
      if (countdown === 0) setError('');
      return;
    }

    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev === null || prev <= 1) {
          clearInterval(timer);
          setError('');
          return 0;
        }
        setError(t('pages.login.toManyLoginError', { seconds: prev - 1 }));
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [countdown]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await login(user, pass);

      if (!response.ok && response.status !== 403) throw new Error('Login failed');

      if (response.ok) {
        localStorage.setItem('token', (await response.json()).token);
        onLogin();
      }

      if (response.status === 403) {
        response.json().then((json: { error: string; remainingTime: number }) => {
          const seconds = Math.ceil(json.remainingTime);
          setCountdown(seconds);
          setError(t('pages.login.toManyLoginError', { seconds: seconds }));
        });
      }
    } catch (err) {
      setError(t('pages.login.loginError'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)',
      }}
    >
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
            src={config.logo}
            sx={{
              m: 1,
              bgcolor: 'transparent',
              width: '100px',
              height: '100px',
              maxHeight: '100px',
              maxWidth: '100px',
              '& img': {
                objectFit: 'contain',
              },
            }}
          >
            <LockOutlinedIcon fontSize="large" />
          </Avatar>

          <Typography component="h1" variant="h4" sx={{ fontWeight: 700, mt: 2 }}>
            {config.pageName} {t('pages.login.clubManagement')}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            {t('pages.login.loginHint')}
          </Typography>

          {error && (
            <Alert severity="error" sx={{ width: '100%', mb: 2 }}>
              {error}
            </Alert>
          )}

          <Box component="form" onSubmit={submit} sx={{ width: '100%' }}>
            <TextField
              margin="normal"
              required
              fullWidth
              label={t('pages.login.username')}
              autoFocus
              value={user}
              onChange={(e) => setUser(e.target.value)}
              variant="outlined"
            />
            <TextField
              margin="normal"
              required
              fullWidth
              label={t('pages.login.password')}
              type={showPassword ? 'text' : 'password'}
              value={pass}
              onChange={(e) => setPass(e.target.value)}
              InputProps={{
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton onClick={() => setShowPassword(!showPassword)} edge="end">
                      {showPassword ? <VisibilityOff /> : <Visibility />}
                    </IconButton>
                  </InputAdornment>
                ),
              }}
            />
            <Button
              type="submit"
              fullWidth
              variant="contained"
              size="large"
              disabled={loading}
              sx={{
                mt: 4,
                mb: 2,
                py: 1.5,
                borderRadius: 2,
                textTransform: 'none',
                fontSize: '1rem',
                fontWeight: 600,
              }}
            >
              {loading ? t('pages.login.loading') : t('pages.login.login')}
            </Button>
            {demo && <DemoDialog />}
          </Box>
        </Paper>
        <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 4 }}>
          &copy; {new Date().getFullYear()} {config.pageName} {t('pages.login.clubManagement')}
        </Typography>
      </Container>
    </Box>
  );
}
