import { IncomeRow, IncomeSource, IncomeSummary } from '../../types';

export const ALL_TAB = 'all';
export const CATEGORY_TAB_PREFIX = 'category:';
export const DATE_FORMAT = 'YYYY-MM-DD';

export const EMPTY_SUMMARY: IncomeSummary = {
  openAmount: 0,
  openCount: 0,
  paidAmount: 0,
  paidCount: 0,
  breakdown: [],
};

export const formatEuro = (amount: number) =>
  amount.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' });

export const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString('de-DE', { timeZone: 'UTC' });

export const incomeBasePath = (row: IncomeRow) =>
  row.source === IncomeSource.CONTRIBUTION ? '/contributions' : '/income';
