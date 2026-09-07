import { useState, useCallback, useEffect, ReactNode } from 'react';
import { api } from '../api';
import { PageConfigContext } from '../hooks/usePageConfig';

const RETRY_INTERVAL_MS = 15_000;

export const PageConfigProvider = ({ children }: { children: ReactNode }) => {
  const [config, setConfig] = useState<{
    pageName: string;
    logo: string;
    selfEnrollmentEnabled: boolean;
  }>({
    pageName: '',
    logo: '',
    selfEnrollmentEnabled: false,
  });
  const [loading, setLoading] = useState(true);
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
