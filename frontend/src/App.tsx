import { useState, useMemo, useEffect, lazy, Suspense } from 'react';
import { createTheme, ThemeProvider, CssBaseline, Typography, useMediaQuery } from '@mui/material';
import type {} from '@mui/x-date-pickers/themeAugmentation';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import PublicEnrollmentPage from './pages/PublicEnrollmentPage';
import ConfirmEnrollmentPage from './pages/ConfirmEnrollmentPage';

const PublicCalendar = lazy(() => import('./pages/PublicCalendar'));
import { fetchSession, logout, Session } from './api';
import { usePageConfig } from './hooks/usePageConfig';
import { useThemeMode } from './hooks/useThemeMode';

export default function App() {
  const [session, setSession] = useState<Session | null | undefined>(undefined);
  const { config, reloadConfig } = usePageConfig();
  const { resolvedMode } = useThemeMode();

  // @ts-expect-error - injected at build time via Vite define, not part of ImportMetaEnv types
  const version = import.meta.env.VITE_APP_VERSION;
  const demo = config.demo;

  const theme = useMemo(
    () =>
      createTheme({
        palette: {
          mode: resolvedMode,
          primary: {
            main: resolvedMode === 'dark' ? '#60a5fa' : '#2563eb',
          },
          background:
            resolvedMode === 'dark'
              ? {
                  default: '#121212',
                  paper: '#1e1e1e',
                }
              : {
                  default: '#f8fafc',
                },
        },
        shape: {
          borderRadius: 8,
        },
        typography: {
          fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
        },
        components: {
          MuiOutlinedInput: {
            styleOverrides: {
              notchedOutline: {
                '@supports (-webkit-appearance: none)': {
                  '& legend': {
                    visibility: 'visible !important',
                  },
                },
              },
            },
          },
          MuiPickersOutlinedInput: {
            styleOverrides: {
              notchedOutline: {
                '@supports (-webkit-appearance: none)': {
                  '& legend': {
                    visibility: 'visible !important',
                  },
                },
              },
            },
          },
        },
      }),
    [resolvedMode],
  );

  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });

  useEffect(() => {
    let cancelled = false;

    try {
      localStorage.removeItem('token');
    } catch {
      // storage not available
    }

    fetchSession()
      .then((current) => {
        if (!cancelled) setSession(current);
      })
      .catch(() => {
        if (!cancelled) setSession(null);
      });
    reloadConfig().catch(() => {});

    const interval = setInterval(() => {
      fetchSession()
        .then((current) => {
          if (!cancelled && !current) setSession(null);
        })
        .catch(() => {});
    }, 60_000);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [reloadConfig]);

  const handleLogout = () => {
    logout()
      .catch(() => {})
      .finally(() => setSession(null));
  };

  const handleLoginSuccess = () => {
    fetchSession()
      .then(setSession)
      .catch(() => setSession(null));
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />

      {window.location.pathname === '/calendar/public' ? (
        <Suspense fallback={null}>
          <PublicCalendar />
        </Suspense>
      ) : window.location.pathname === '/enroll/confirm' ? (
        <ConfirmEnrollmentPage />
      ) : window.location.pathname === '/enroll' ? (
        <PublicEnrollmentPage />
      ) : session === undefined ? null : !session ? (
        <Login onLogin={handleLoginSuccess} demo={demo} />
      ) : (
        <Dashboard
          user={session}
          onLogout={handleLogout}
          pageName={config.pageName}
          logo={config.logo}
        />
      )}
      {!isMobile && (
        <Typography
          variant="caption"
          sx={{
            position: 'fixed',
            bottom: 8,
            right: 12,
            color: 'text.disabled',
            pointerEvents: 'none',
            zIndex: 9999,
            userSelect: 'none',
          }}
        >
          v{version}
        </Typography>
      )}
    </ThemeProvider>
  );
}
