import { createContext, useContext, useState, useCallback, Dispatch, SetStateAction } from 'react';
import { ConfigContextType } from '../types';
import { api } from '../api';

const PageConfigContext = createContext<ConfigContextType>({
  config: { pageName: '', logo: '' },
  loading: false,
  reloadConfig: async () => {},
});

export const PageConfigProvider = ({ children }: any) => {
  const [config, setConfig] = useState<{ pageName: string; logo: string }>({
    pageName: '',
    logo: '',
  });
  const [loading, setLoading] = useState(false);

  const reloadConfig = useCallback(async () => {
    setLoading(true);
    try {
      const response = await api('/web-page-config');
      if (response) {
        setConfig(response);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  return (
    <PageConfigContext.Provider value={{ config, loading, reloadConfig }}>
      {children}
    </PageConfigContext.Provider>
  );
};

export const usePageConfig = () => useContext(PageConfigContext);
