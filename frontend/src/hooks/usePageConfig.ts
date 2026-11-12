import { createContext, useContext } from 'react';
import { ConfigContextType } from '../types';

export const PageConfigContext = createContext<ConfigContextType>({
  config: { pageName: '', logo: '', demo: false },
  loading: true,
  reloadConfig: async () => {},
  serverReachable: true,
});

export const usePageConfig = () => useContext(PageConfigContext);
