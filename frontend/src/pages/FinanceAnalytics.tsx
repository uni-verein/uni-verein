import { ReactNode, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Tab,
  Tabs,
  Tooltip,
  Typography,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import { useTranslation } from 'react-i18next';
import { apiFile } from '../api';
import { useSnackbar } from '../hooks/useSnackbar';
import { FinanceCategoryDonut } from '../components/finance/FinanceCategoryDonut';
import { FinanceCategoryTable } from '../components/finance/FinanceCategoryTable';
import { FinanceCompareChart } from '../components/finance/FinanceCompareChart';
import { FinanceKpiCards } from '../components/finance/FinanceKpiCards';
import { FinanceStackedMonthChart } from '../components/finance/FinanceStackedMonthChart';
import {
  availableYears,
  computeKpis,
  FinanceSide,
  formatPercent,
  monthlyTotals,
  summarizeCategories,
  yearlyTotals,
} from '../components/finance/financeUtils';
import { useFinanceAnalytics } from '../components/finance/useFinanceAnalytics';
import { formatEuro } from '../components/income/incomeUtils';

const MONTH_KEYS = [
  'jan',
  'feb',
  'mar',
  'apr',
  'may',
  'jun',
  'jul',
  'aug',
  'sep',
  'oct',
  'nov',
  'dec',
];

type FinanceTab = 'overview' | 'income' | 'expenses' | 'categories';

function ChartCard({
  title,
  total,
  children,
}: {
  title: string;
  total?: string;
  children: ReactNode;
}) {
  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 2, height: '100%' }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', mb: 1 }}>
        <Typography variant="subtitle1" fontWeight={600}>
          {title}
        </Typography>
        {total && (
          <Typography variant="body2" color="text.secondary">
            {total}
          </Typography>
        )}
      </Box>
      {children}
    </Paper>
  );
}

export default function FinanceAnalytics() {
  const { t } = useTranslation();
  const { rows, loading, failed } = useFinanceAnalytics();
  const now = new Date();
  const [year, setYear] = useState<number>(now.getFullYear());
  const [tab, setTab] = useState<FinanceTab>('overview');
  const [exporting, setExporting] = useState(false);
  const setFinanceChange = useSnackbar();

  const monthLabels = MONTH_KEYS.map((key) => t(`pages.financeAnalytics.months.${key}`));
  const kpis = computeKpis(rows, year);
  const monthly = monthlyTotals(rows, year);
  const yearly = yearlyTotals(rows);
  const incomeCategories = summarizeCategories(rows, year, 'income');
  const expenseCategories = summarizeCategories(rows, year, 'expense');
  const monthsInYear = year === now.getFullYear() ? now.getMonth() + 1 : 12;

  const takeaway = () => {
    if (rows.every((r) => r.year !== year)) return null;
    const result =
      kpis.balance === 0
        ? t('pages.financeAnalytics.takeaway.balanced', { year })
        : t(`pages.financeAnalytics.takeaway.${kpis.balance > 0 ? 'surplus' : 'deficit'}`, {
            year,
            amount: formatEuro(Math.abs(kpis.balance)),
          });
    if (kpis.membershipShare === null) return result;
    return `${result} ${t('pages.financeAnalytics.takeaway.membershipShare', {
      share: formatPercent(kpis.membershipShare),
    })}`;
  };

  const exportEur = async () => {
    setExporting(true);
    try {
      const response = await apiFile(`/finance/eur?year=${year}`);
      if (!response.ok) throw new Error('API Error');

      const filename =
        /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(
          response.headers.get('content-disposition') ?? '',
        )?.[1] ?? `EUER_${year}.xlsx`;
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement('a');
      link.href = url;
      link.download = decodeURIComponent(filename);
      link.click();
      URL.revokeObjectURL(url);

      setFinanceChange({
        status: 'success',
        message: t('pages.financeAnalytics.exportEur.success', { year }),
      });
    } catch {
      setFinanceChange({
        status: 'error',
        message: t('pages.financeAnalytics.exportEur.error'),
      });
    } finally {
      setExporting(false);
    }
  };

  const sideTab = (side: FinanceSide) => {
    const categories = side === 'income' ? incomeCategories : expenseCategories;
    const total = side === 'income' ? kpis.income : kpis.expenses;
    const totalLabel = `${t('pages.financeAnalytics.total')}: ${formatEuro(total)}`;
    return (
      <Grid container spacing={3} alignItems="stretch">
        <Grid size={{ xs: 12, md: 7 }}>
          <ChartCard
            title={t(`pages.financeAnalytics.${side}ByMonth`, { year })}
            total={totalLabel}
          >
            <FinanceStackedMonthChart categories={categories} monthLabels={monthLabels} />
          </ChartCard>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <ChartCard title={t(`pages.financeAnalytics.${side}ByCategory`, { year })}>
            <FinanceCategoryDonut categories={categories} />
          </ChartCard>
        </Grid>
      </Grid>
    );
  };

  return (
    <Box>
      <Box
        sx={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 2, mb: 3 }}
      >
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            {t('pages.financeAnalytics.title')}
          </Typography>
          {!loading && !failed && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {takeaway()}
            </Typography>
          )}
        </Box>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <Tooltip title={t('pages.financeAnalytics.exportEur.tooltip', { year })}>
            <span>
              <Button
                variant="outlined"
                size="large"
                startIcon={
                  exporting ? <CircularProgress size={20} color="inherit" /> : <DownloadIcon />
                }
                disabled={exporting}
                onClick={exportEur}
                sx={{ height: 56 }}
              >
                {t('pages.financeAnalytics.exportEur.button')}
              </Button>
            </span>
          </Tooltip>
          <FormControl sx={{ minWidth: 120 }}>
            <InputLabel id="finance-analytics-year-label">
              {t('pages.financeAnalytics.year')}
            </InputLabel>
            <Select
              labelId="finance-analytics-year-label"
              value={year}
              label={t('pages.financeAnalytics.year')}
              onChange={(e) => setYear(Number(e.target.value))}
            >
              {availableYears(rows, now.getFullYear()).map((y) => (
                <MenuItem key={y} value={y}>
                  {y}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>
      </Box>

      {loading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {failed && <Alert severity="error">{t('pages.financeAnalytics.loadError')}</Alert>}

      {!loading && !failed && (
        <>
          <Tabs
            value={tab}
            onChange={(_, value: FinanceTab) => setTab(value)}
            variant="scrollable"
            scrollButtons="auto"
            allowScrollButtonsMobile
            sx={{ mb: 3, borderBottom: 1, borderColor: 'divider' }}
          >
            <Tab value="overview" label={t('pages.financeAnalytics.tabs.overview')} />
            <Tab value="income" label={t('pages.financeAnalytics.tabs.income')} />
            <Tab value="expenses" label={t('pages.financeAnalytics.tabs.expenses')} />
            <Tab value="categories" label={t('pages.financeAnalytics.tabs.categories')} />
          </Tabs>

          {tab === 'overview' && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
              <FinanceKpiCards kpis={kpis} />
              <ChartCard
                title={t('pages.financeAnalytics.compareByMonth', { year })}
                total={`${t('pages.financeAnalytics.kpi.balance')}: ${formatEuro(kpis.balance)}`}
              >
                <FinanceCompareChart
                  labels={monthLabels}
                  income={monthly.income}
                  expenses={monthly.expenses}
                />
              </ChartCard>
              <ChartCard title={t('pages.financeAnalytics.compareByYear')}>
                <FinanceCompareChart
                  labels={yearly.years.map(String)}
                  income={yearly.income}
                  expenses={yearly.expenses}
                />
              </ChartCard>
            </Box>
          )}
          {tab === 'income' && sideTab('income')}
          {tab === 'expenses' && sideTab('expense')}
          {tab === 'categories' && (
            <FinanceCategoryTable
              income={incomeCategories}
              expenses={expenseCategories}
              months={monthsInYear}
            />
          )}
        </>
      )}
    </Box>
  );
}
