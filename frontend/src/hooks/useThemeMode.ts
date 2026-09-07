import { createContext, useContext } from 'react';

export type ThemeMode = 'light' | 'dark' | 'system';
export type ResolvedThemeMode = 'light' | 'dark';

type ThemeModeContextType = {
  mode: ThemeMode;
  resolvedMode: ResolvedThemeMode;
  setMode: (mode: ThemeMode) => void;
};

export const ThemeModeContext = createContext<ThemeModeContextType>({
  mode: 'system',
  resolvedMode: 'light',
  setMode: () => {},
});

export const useThemeMode = () => useContext(ThemeModeContext);
