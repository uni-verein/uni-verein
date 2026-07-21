import { useState, useMemo, useEffect } from 'react';
import { createTheme, ThemeProvider, CssBaseline, Typography, useMediaQuery } from '@mui/material';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import { isTokenValid } from './utils';
import { usePageConfig } from './components/PageConfigContext';

export default function App() {
  const [logged, setLogged] = useState(!!localStorage.getItem('token'));
  const { config, reloadConfig } = usePageConfig();

  // @ts-ignore
  const version = import.meta.env.VITE_APP_VERSION;
  // @ts-ignore
  const demo = import.meta.env.VITE_APP_DEMO;

  const theme = useMemo(
    () =>
      createTheme({
        palette: {
          primary: {
            main: '#2563eb',
          },
          background: {
            default: '#f8fafc',
          },
        },
        shape: {
          borderRadius: 8,
        },
        typography: {
          fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
        },
      }),
    [],
  );

  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });

  useEffect(() => {
    const token = localStorage.getItem('token');
    console.log(token);
    if (token && isTokenValid(token)) {
      console.log('token is valid');
      setLogged(true);
    } else {
      localStorage.removeItem('token');
      console.log('token is invalid');
      setLogged(false);
    }
    reloadConfig().catch();

    const interval = setInterval(() => {
      const currentToken = localStorage.getItem('token');
      if (!currentToken || !isTokenValid(currentToken)) {
        localStorage.removeItem('token');
        setLogged(false);
      }
    }, 60_000);

    return () => clearInterval(interval);
  }, [reloadConfig]);

  const handleLogout = () => {
    localStorage.removeItem('token');
    setLogged(false);
  };

  const handleLoginSuccess = () => {
    setLogged(true);
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />

      {!logged ? (
        <Login onLogin={handleLoginSuccess} demo={demo} />
      ) : (
        <Dashboard onLogout={handleLogout} pageName={config.pageName} />
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
