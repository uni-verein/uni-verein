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
  useTheme,
} from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import { login, loginTwoFactor } from '../api';
import { useTranslation } from 'react-i18next';
import { usePageConfig } from '../hooks/usePageConfig';
import { DemoDialog } from '../components/dialogs/DemoDialog';
import { ForcePasswordChangeDialog } from '../components/dialogs/ForcePasswordChangeDialog';

const DEFAULT_PASSWORD = 'admin123';

export default function Login({
  onLogin,
  demo,
}: {
  onLogin: () => void;
  demo: boolean | undefined;
}) {
  const { config, serverReachable } = usePageConfig();
  const theme = useTheme();
  const [user, setUser] = useState('');
  const [pass, setPass] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [countdown, setCountdown] = useState<number | null>(null);
  const [browserOnline, setBrowserOnline] = useState(navigator.onLine);
  const [forcePasswordChange, setForcePasswordChange] = useState(false);
  const [challenge, setChallenge] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [recoveryAllowed, setRecoveryAllowed] = useState(false);
  const { t } = useTranslation();

  const isOffline = !browserOnline || !serverReachable;

  useEffect(() => {
    const handleOnline = () => setBrowserOnline(true);
    const handleOffline = () => setBrowserOnline(false);
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  useEffect(() => {
    if (countdown === null || countdown <= 0) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
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
  }, [countdown, t]);

  const finishLogin = () => {
    if (pass === DEFAULT_PASSWORD) {
      setForcePasswordChange(true);
    } else {
      onLogin();
    }
  };

  const showLockout = (response: Response) => {
    response
      .json()
      .then((json: { error: string; remainingTime: number; recoveryCodeAllowed?: boolean }) => {
        const seconds = Math.ceil(json.remainingTime);
        setRecoveryAllowed(!!json.recoveryCodeAllowed);
        setCountdown(seconds);
        setError(t('pages.login.toManyLoginError', { seconds: seconds }));
      });
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await login(user, pass);

      if (!response.ok && response.status !== 403) throw new Error('Login failed');

      if (response.ok) {
        const json: { requiresTwoFactor?: boolean; challenge?: string } = await response.json();
        if (json.requiresTwoFactor && json.challenge) {
          setChallenge(json.challenge);
          setCode('');
        } else {
          finishLogin();
        }
      }

      if (response.status === 403) showLockout(response);
    } catch (err) {
      if (!(err instanceof TypeError)) {
        setError(t('pages.login.loginError'));
      }
    } finally {
      setLoading(false);
    }
  };

  const submitCode = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!challenge) return;
    setError('');
    setLoading(true);

    try {
      const response = await loginTwoFactor(challenge, code.trim());

      if (response.ok) {
        finishLogin();
      } else if (response.status === 403) {
        showLockout(response);
      } else {
        setError(t('pages.login.twoFactor.error'));
      }
    } catch (err) {
      if (!(err instanceof TypeError)) {
        setError(t('pages.login.loginError'));
      }
    } finally {
      setLoading(false);
    }
  };

  const cancelTwoFactor = () => {
    setChallenge(null);
    setCode('');
    setPass('');
    setError('');
  };

  return (
    <Box
      sx={{
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
              color: 'text.secondary',
              width: '100px',
              height: '100px',
              maxHeight: '100px',
              maxWidth: '100px',
              border: '1px solid',
              borderColor: 'divider',
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

          {isOffline && (
            <Alert
              severity="warning"
              icon={<WifiOffIcon fontSize="inherit" />}
              sx={{ width: '100%', mb: 2 }}
            >
              {t('pages.login.offline')}
            </Alert>
          )}

          {error && (
            <Alert severity="error" sx={{ width: '100%', mb: 2 }}>
              {error}
            </Alert>
          )}

          {challenge && recoveryAllowed && countdown !== null && countdown > 0 && (
            <Alert severity="info" sx={{ width: '100%', mb: 2 }}>
              {t('pages.login.twoFactor.blockedRecoveryHint')}
            </Alert>
          )}

          {challenge ? (
            <Box component="form" onSubmit={submitCode} sx={{ width: '100%' }}>
              <Typography variant="body2" color="text.secondary">
                {t('pages.login.twoFactor.hint')}
              </Typography>
              <TextField
                margin="normal"
                required
                fullWidth
                autoFocus
                label={t('pages.login.twoFactor.code')}
                value={code}
                onChange={(e) => setCode(e.target.value)}
                autoComplete="one-time-code"
                slotProps={{ htmlInput: { inputMode: 'text', maxLength: 20 } }}
                helperText={t('pages.login.twoFactor.recoveryHint')}
                variant="outlined"
              />
              <Button
                type="submit"
                fullWidth
                variant="contained"
                size="large"
                disabled={loading || !code.trim()}
                sx={{
                  mt: 2,
                  mb: 1,
                  py: 1.5,
                  borderRadius: 2,
                  textTransform: 'none',
                  fontSize: '1rem',
                  fontWeight: 600,
                }}
              >
                {loading ? t('pages.login.loading') : t('pages.login.twoFactor.verify')}
              </Button>
              <Button fullWidth onClick={cancelTwoFactor} sx={{ textTransform: 'none' }}>
                {t('pages.login.twoFactor.back')}
              </Button>
            </Box>
          ) : (
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
          )}
        </Paper>
        <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 4 }}>
          &copy; {new Date().getFullYear()} {config.pageName} {t('pages.login.clubManagement')}
        </Typography>
      </Container>
      {forcePasswordChange && (
        <ForcePasswordChangeDialog
          currentPassword={pass}
          onChanged={() => {
            setForcePasswordChange(false);
            onLogin();
          }}
        />
      )}
    </Box>
  );
}
