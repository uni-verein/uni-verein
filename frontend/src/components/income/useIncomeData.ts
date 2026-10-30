import { useCallback, useEffect, useMemo, useState } from 'react';
import debounce from 'lodash.debounce';
import { api } from '../../api';
import { IncomeCategory, IncomeRow, IncomeSummary } from '../../types';
import { EMPTY_SUMMARY } from './incomeUtils';

export function useIncomeData(rowsQuery: string, rangeParams: string) {
  const [rows, setRows] = useState<IncomeRow[]>([]);
  const [summary, setSummary] = useState<IncomeSummary>(EMPTY_SUMMARY);
  const [categories, setCategories] = useState<IncomeCategory[]>([]);
  const [years, setYears] = useState<number[]>([new Date().getFullYear()]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);

  const fetchRows = useCallback(async (query: string) => {
    setLoading(true);
    try {
      const response = await api(`/income?${query}`);
      setRows(response.items);
      setTotalCount(response.total);
    } catch (error) {
      console.error('Loading error:', error);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchSummary = useCallback(async (query: string) => {
    try {
      setSummary(await api(`/income/summary?${query}`));
    } catch (error) {
      console.error('Loading error:', error);
    }
  }, []);

  const fetchYears = useCallback(async () => {
    try {
      const response = await api('/income/years');
      if (response?.years?.length) setYears(response.years);
    } catch (error) {
      console.error('Loading error:', error);
    }
  }, []);

  const fetchCategories = useCallback(async () => {
    try {
      const response = await api('/income-categories');
      setCategories(response?.items ?? []);
    } catch (error) {
      console.error('Loading error:', error);
    }
  }, []);

  const debouncedFetchRows = useMemo(() => debounce(fetchRows, 500), [fetchRows]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchCategories().catch();
    fetchYears().catch();
  }, [fetchCategories, fetchYears]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchSummary(rangeParams).catch();
  }, [fetchSummary, rangeParams]);

  useEffect(() => {
    debouncedFetchRows(rowsQuery);
    return () => debouncedFetchRows.cancel();
  }, [debouncedFetchRows, rowsQuery]);

  const reload = async () => {
    await Promise.all([fetchRows(rowsQuery), fetchSummary(rangeParams), fetchYears()]);
  };

  return { rows, summary, categories, years, totalCount, loading, reload };
}
