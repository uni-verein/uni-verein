import GroupsIcon from '@mui/icons-material/Groups';
import SavingsIcon from '@mui/icons-material/Savings';
import TrendingDownIcon from '@mui/icons-material/TrendingDown';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import { useTranslation } from 'react-i18next';
import { formatEuro } from '../income/incomeUtils';
import { AutoFitGrid } from '../AutoFitGrid';
import { KpiCard } from '../income/IncomeKpiCards';
import { FinanceKpis, formatPercent } from './financeUtils';

export function FinanceKpiCards({ kpis }: { kpis: FinanceKpis }) {
  const { t } = useTranslation();

  return (
    <AutoFitGrid>
      <KpiCard
        icon={<TrendingUpIcon color="primary" sx={{ fontSize: 40 }} />}
        value={formatEuro(kpis.income)}
        caption={t('pages.financeAnalytics.kpi.income')}
      />
      <KpiCard
        icon={<TrendingDownIcon color="error" sx={{ fontSize: 40 }} />}
        value={formatEuro(kpis.expenses)}
        caption={t('pages.financeAnalytics.kpi.expenses')}
      />
      <KpiCard
        icon={<SavingsIcon color={kpis.balance < 0 ? 'error' : 'success'} sx={{ fontSize: 40 }} />}
        value={formatEuro(kpis.balance)}
        caption={t('pages.financeAnalytics.kpi.balance')}
      />
      <KpiCard
        icon={<GroupsIcon color="secondary" sx={{ fontSize: 40 }} />}
        value={kpis.membershipShare === null ? '–' : formatPercent(kpis.membershipShare)}
        caption={t('pages.financeAnalytics.kpi.membershipShare')}
      />
    </AutoFitGrid>
  );
}
