import { useEffect, useState } from 'react';
import { api } from '../../api';
import { FinanceAnalytics, FinanceAnalyticsRow } from '../../types';

export function useFinanceAnalytics() {
  const [rows, setRows] = useState<FinanceAnalyticsRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api('/finance/analytics')
      .then((result: FinanceAnalytics) => {
        if (!cancelled) setRows(result?.rows ?? []);
      })
      .catch((e) => {
        console.error(e);
        if (!cancelled) setFailed(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return { rows, loading, failed };
}
