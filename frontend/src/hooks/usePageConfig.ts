import { createContext, useContext } from 'react';
import { ConfigContextType } from '../types';

export const PageConfigContext = createContext<ConfigContextType>({
  config: { pageName: '', logo: '', selfEnrollmentEnabled: false },
  loading: true,
  reloadConfig: async () => {},
  serverReachable: true,
});

export const usePageConfig = () => useContext(PageConfigContext);
