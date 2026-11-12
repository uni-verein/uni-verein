import { useEffect, useMemo, useState, ReactNode } from 'react';
import { ThemeMode, ResolvedThemeMode, ThemeModeContext } from '../hooks/useThemeMode';

const STORAGE_KEY = 'themeMode';

const getSystemPreference = (): ResolvedThemeMode =>
  window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';

const isEmbedded = () => window.location.pathname === '/embed/enroll';

const getEmbeddedMode = (): ThemeMode => {
  const requested = new URLSearchParams(window.location.search).get('theme');
  if (requested === 'dark') return 'dark';
  if (requested === 'auto') return 'system';
  return 'light';
};

const getStoredMode = (): ThemeMode => {
  if (isEmbedded()) return getEmbeddedMode();
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
  } catch {
    return 'system';
  }
};

export const ThemeModeProvider = ({ children }: { children: ReactNode }) => {
  const [mode, setModeState] = useState<ThemeMode>(getStoredMode);
  const [systemPreference, setSystemPreference] = useState<ResolvedThemeMode>(getSystemPreference);

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const listener = (e: MediaQueryListEvent) => setSystemPreference(e.matches ? 'dark' : 'light');
    mediaQuery.addEventListener('change', listener);
    return () => mediaQuery.removeEventListener('change', listener);
  }, []);

  const setMode = (newMode: ThemeMode) => {
    setModeState(newMode);
    if (isEmbedded()) return;
    try {
      localStorage.setItem(STORAGE_KEY, newMode);
    } catch {
      // storage not available
    }
  };

  const resolvedMode = useMemo<ResolvedThemeMode>(
    () => (mode === 'system' ? systemPreference : mode),
    [mode, systemPreference],
  );

  return (
    <ThemeModeContext.Provider value={{ mode, resolvedMode, setMode }}>
      {children}
    </ThemeModeContext.Provider>
  );
};
