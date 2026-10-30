import { ReactNode } from 'react';
import { Box, Card, CardContent, Typography } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import GroupsIcon from '@mui/icons-material/Groups';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import VolunteerActivismIcon from '@mui/icons-material/VolunteerActivism';
import { useTranslation } from 'react-i18next';
import { AutoFitGrid } from '../AutoFitGrid';
import { IncomeKind, IncomeSummary } from '../../types';
import { formatEuro } from './incomeUtils';

export function KpiCard({
  icon,
  value,
  caption,
  highlight,
}: {
  icon: ReactNode;
  value: string;
  caption: string;
  highlight?: boolean;
}) {
  return (
    <Card
      elevation={0}
      sx={{
        border: '1px solid',
        borderColor: highlight ? 'warning.light' : 'divider',
        borderRadius: 3,
        bgcolor: highlight ? 'warning.50' : undefined,
        height: '100%',
      }}
    >
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2, height: '100%' }}>
        {icon}
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {value}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {caption}
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
}

export function IncomeKpiCards({ summary }: { summary: IncomeSummary }) {
  const { t } = useTranslation();

  const membershipPaid =
    summary.breakdown.find((b) => b.kind === IncomeKind.MEMBERSHIP)?.amount ?? 0;
  const otherPaid = summary.paidAmount - membershipPaid;

  return (
    <AutoFitGrid sx={{ mb: 3 }}>
      <KpiCard
        highlight
        icon={<PendingActionsIcon color="warning" sx={{ fontSize: 40 }} />}
        value={formatEuro(summary.openAmount)}
        caption={t('pages.income.kpi.open', { count: summary.openCount })}
      />
      <KpiCard
        icon={<CheckCircleIcon color="success" sx={{ fontSize: 40 }} />}
        value={formatEuro(summary.paidAmount)}
        caption={t('pages.income.kpi.received', { count: summary.paidCount })}
      />
      <KpiCard
        icon={<GroupsIcon color="primary" sx={{ fontSize: 40 }} />}
        value={formatEuro(membershipPaid)}
        caption={t('pages.income.kpi.membership', {
          share: summary.paidAmount ? Math.round((membershipPaid / summary.paidAmount) * 100) : 0,
        })}
      />
      <KpiCard
        icon={<VolunteerActivismIcon color="secondary" sx={{ fontSize: 40 }} />}
        value={formatEuro(otherPaid)}
        caption={t('pages.income.kpi.other')}
      />
    </AutoFitGrid>
  );
}
