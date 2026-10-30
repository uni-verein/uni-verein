import type { TFunction } from 'i18next';
import { FinanceAnalyticsRow, FinanceEntryKind } from '../../types';

export type FinanceSide = 'income' | 'expense';

export interface FinanceCategorySummary {
  key: string;
  side: FinanceSide;
  kind: FinanceEntryKind;
  name: string | null;
  total: number;
  share: number;
  color: string;
  monthly: number[];
}

export interface FinanceKpis {
  income: number;
  expenses: number;
  balance: number;
  membershipShare: number | null;
}

const NO_CATEGORY_KEY = 'none';
const MEMBERSHIP_KEY = 'membership';
const GOLDEN_ANGLE = 137.508;

export const formatPercent = (value: number) =>
  value.toLocaleString('de-DE', { style: 'percent', maximumFractionDigits: 1 });

export function categoryColorForIndex(index: number): string {
  const hue = (index * GOLDEN_ANGLE) % 360;
  const saturation = 62 + (index % 3) * 8;
  const lightness = 50 + (index % 2) * 8;
  return `hsl(${hue.toFixed(1)}, ${saturation}%, ${lightness}%)`;
}

export function sideOf(kind: FinanceEntryKind): FinanceSide {
  return kind === FinanceEntryKind.EXPENSE ? 'expense' : 'income';
}

function categoryKey(row: FinanceAnalyticsRow): string {
  if (row.kind === FinanceEntryKind.MEMBERSHIP) return MEMBERSHIP_KEY;
  return row.categoryId ? String(row.categoryId) : NO_CATEGORY_KEY;
}

export function categoryLabel(
  category: Pick<FinanceCategorySummary, 'kind' | 'name'>,
  t: TFunction,
): string {
  if (category.kind === FinanceEntryKind.MEMBERSHIP) return t('pages.financeAnalytics.membership');
  return category.name ?? t('pages.financeAnalytics.noCategory');
}

export function availableYears(rows: FinanceAnalyticsRow[], currentYear: number): number[] {
  const years = new Set(rows.map((r) => r.year));
  years.add(currentYear);
  return [...years].sort((a, b) => b - a);
}

export function computeKpis(rows: FinanceAnalyticsRow[], year: number): FinanceKpis {
  let income = 0;
  let expenses = 0;
  let membership = 0;
  for (const row of rows) {
    if (row.year !== year) continue;
    if (row.kind === FinanceEntryKind.EXPENSE) expenses += row.amount;
    else income += row.amount;
    if (row.kind === FinanceEntryKind.MEMBERSHIP) membership += row.amount;
  }
  return {
    income,
    expenses,
    balance: income - expenses,
    membershipShare: income ? membership / income : null,
  };
}

export function monthlyTotals(rows: FinanceAnalyticsRow[], year: number) {
  const income = Array<number>(12).fill(0);
  const expenses = Array<number>(12).fill(0);
  for (const row of rows) {
    if (row.year !== year) continue;
    if (row.kind === FinanceEntryKind.EXPENSE) expenses[row.month - 1] += row.amount;
    else income[row.month - 1] += row.amount;
  }
  return { income, expenses };
}

export function yearlyTotals(rows: FinanceAnalyticsRow[]) {
  const years = [...new Set(rows.map((r) => r.year))].sort((a, b) => a - b);
  const income = years.map(() => 0);
  const expenses = years.map(() => 0);
  for (const row of rows) {
    const i = years.indexOf(row.year);
    if (row.kind === FinanceEntryKind.EXPENSE) expenses[i] += row.amount;
    else income[i] += row.amount;
  }
  return { years, income, expenses };
}

// Categories of one side (income or expense) in the given year, largest first. The colour is
// assigned here so the stacked chart, donut and legend of a tab always agree.
export function summarizeCategories(
  rows: FinanceAnalyticsRow[],
  year: number,
  side: FinanceSide,
): FinanceCategorySummary[] {
  const byKey = new Map<string, FinanceCategorySummary>();
  for (const row of rows) {
    if (row.year !== year || sideOf(row.kind) !== side) continue;
    const key = categoryKey(row);
    let entry = byKey.get(key);
    if (!entry) {
      entry = {
        key,
        side,
        kind: row.kind,
        name: row.categoryName,
        total: 0,
        share: 0,
        color: '',
        monthly: Array<number>(12).fill(0),
      };
      byKey.set(key, entry);
    }
    entry.total += row.amount;
    entry.monthly[row.month - 1] += row.amount;
  }

  const categories = [...byKey.values()].sort((a, b) => b.total - a.total);
  const total = categories.reduce((sum, c) => sum + c.total, 0);
  categories.forEach((c, i) => {
    c.share = total ? c.total / total : 0;
    c.color = categoryColorForIndex(i);
  });
  return categories;
}
