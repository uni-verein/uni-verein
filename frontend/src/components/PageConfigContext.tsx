import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { ConfigContextType } from '../types';
import { api } from '../api';

const PageConfigContext = createContext<ConfigContextType>({
  config: { pageName: '', logo: '' },
  loading: false,
  reloadConfig: async () => {},
  serverReachable: true,
});

const RETRY_INTERVAL_MS = 15_000;

export const PageConfigProvider = ({ children }: any) => {
  const [config, setConfig] = useState<{ pageName: string; logo: string }>({
    pageName: '',
    logo: '',
  });
  const [loading, setLoading] = useState(false);
  const [serverReachable, setServerReachable] = useState(true);

  const reloadConfig = useCallback(async () => {
    setLoading(true);
    try {
      const response = await api('/web-page-config');
      if (response) {
        setConfig(response);
      }
      setServerReachable(true);
    } catch (err) {
      setServerReachable(!(err instanceof TypeError));
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (serverReachable) return;

    const interval = setInterval(() => {
      reloadConfig().catch(() => {});
    }, RETRY_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [serverReachable, reloadConfig]);

  return (
    <PageConfigContext.Provider value={{ config, loading, reloadConfig, serverReachable }}>
      {children}
    </PageConfigContext.Provider>
  );
};

export const usePageConfig = () => useContext(PageConfigContext);
