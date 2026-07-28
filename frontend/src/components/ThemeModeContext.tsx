import { createContext, useContext, useEffect, useMemo, useState } from 'react';

export type ThemeMode = 'light' | 'dark' | 'system';
export type ResolvedThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'themeMode';

type ThemeModeContextType = {
  mode: ThemeMode;
  resolvedMode: ResolvedThemeMode;
  setMode: (mode: ThemeMode) => void;
};

const ThemeModeContext = createContext<ThemeModeContextType>({
  mode: 'system',
  resolvedMode: 'light',
  setMode: () => {},
});

const getSystemPreference = (): ResolvedThemeMode =>
  window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';

const getStoredMode = (): ThemeMode => {
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
};

export const ThemeModeProvider = ({ children }: any) => {
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
    localStorage.setItem(STORAGE_KEY, newMode);
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

export const useThemeMode = () => useContext(ThemeModeContext);
