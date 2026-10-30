import { useTheme } from '@mui/material';
import { BarChart } from '@mui/x-charts/BarChart';
import { useTranslation } from 'react-i18next';
import { formatEuro } from '../income/incomeUtils';

interface FinanceCompareChartProps {
  labels: string[];
  income: number[];
  expenses: number[];
}

// Income and expenses side by side per period (month or year).
export function FinanceCompareChart({ labels, income, expenses }: FinanceCompareChartProps) {
  const { t } = useTranslation();
  const theme = useTheme();
  const valueFormatter = (value: number | null) => (value === null ? '' : formatEuro(value));

  return (
    <BarChart
      height={320}
      xAxis={[{ scaleType: 'band', data: labels }]}
      series={[
        {
          data: income,
          label: t('pages.financeAnalytics.kpi.income'),
          color: theme.palette.primary.main,
          valueFormatter,
        },
        {
          data: expenses,
          label: t('pages.financeAnalytics.kpi.expenses'),
          color: theme.palette.error.main,
          valueFormatter,
        },
      ]}
      localeText={{ noData: t('pages.financeAnalytics.noData') }}
    />
  );
}
