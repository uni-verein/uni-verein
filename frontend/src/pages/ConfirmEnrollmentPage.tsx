import { useEffect, useState } from 'react';
import {
  Avatar,
  Box,
  CircularProgress,
  Container,
  Paper,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import MarkEmailReadIcon from '@mui/icons-material/MarkEmailRead';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { usePageConfig } from '../hooks/usePageConfig';
import { LanguageToggle } from '../components/LanguageToggle';

type ConfirmState = 'loading' | 'success' | 'error';

export default function ConfirmEnrollmentPage() {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { config } = usePageConfig();
  const [state, setState] = useState<ConfirmState>('loading');

  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get('token');
    if (!token) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setState('error');
      return;
    }

    const confirm = async () => {
      try {
        await api('/self-enrollment/confirm', {
          method: 'POST',
          body: JSON.stringify({ token }),
        });
        setState('success');
      } catch {
        setState('error');
      }
    };
    confirm();
  }, []);

  const background =
    theme.palette.mode === 'dark'
      ? 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)'
      : 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)';

  return (
    <Box
      sx={{
        position: 'relative',
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background,
      }}
    >
      <Box
        sx={{
          position: 'fixed',
          top: 16,
          right: 16,
          zIndex: (theme) => theme.zIndex.modal + 1,
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
          {state === 'loading' && (
            <>
              <CircularProgress sx={{ mb: 2 }} />
              <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
                {t('pages.confirmEnrollment.loading')}
              </Typography>
            </>
          )}

          {state === 'success' && (
            <>
              <Avatar
                src={config.logo}
                sx={{ m: 1, bgcolor: 'transparent', color: 'success.main', width: 72, height: 72 }}
              >
                <MarkEmailReadIcon fontSize="large" />
              </Avatar>
              <Typography
                component="h1"
                variant="h5"
                sx={{ fontWeight: 700, mt: 1, textAlign: 'center' }}
              >
                {t('pages.confirmEnrollment.successTitle')}
              </Typography>
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ mt: 1, textAlign: 'center' }}
              >
                {t('pages.confirmEnrollment.successMessage')}
              </Typography>
            </>
          )}

          {state === 'error' && (
            <>
              <Avatar
                sx={{ m: 1, bgcolor: 'transparent', color: 'error.main', width: 72, height: 72 }}
              >
                <ErrorOutlineIcon fontSize="large" />
              </Avatar>
              <Typography
                component="h1"
                variant="h5"
                sx={{ fontWeight: 700, mt: 1, textAlign: 'center' }}
              >
                {t('pages.confirmEnrollment.errorTitle')}
              </Typography>
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ mt: 1, textAlign: 'center' }}
              >
                {t('pages.confirmEnrollment.errorMessage')}
              </Typography>
            </>
          )}
        </Paper>
      </Container>
    </Box>
  );
}
