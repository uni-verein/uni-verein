import { BarChart } from '@mui/x-charts/BarChart';
import { useTranslation } from 'react-i18next';
import { formatEuro } from '../income/incomeUtils';
import { categoryLabel, FinanceCategorySummary } from './financeUtils';

interface FinanceStackedMonthChartProps {
  categories: FinanceCategorySummary[];
  monthLabels: string[];
}

export function FinanceStackedMonthChart({
  categories,
  monthLabels,
}: FinanceStackedMonthChartProps) {
  const { t } = useTranslation();

  return (
    <BarChart
      height={320}
      xAxis={[{ scaleType: 'band', data: monthLabels }]}
      series={categories.map((c) => ({
        id: c.key,
        data: c.monthly,
        label: categoryLabel(c, t),
        color: c.color,
        stack: 'total',
        valueFormatter: (value: number | null) => (value === null ? '' : formatEuro(value)),
      }))}
      localeText={{ noData: t('pages.financeAnalytics.noData') }}
    />
  );
}
