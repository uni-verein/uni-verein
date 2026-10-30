import { Box, Card, CardContent, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { IncomeSummary } from '../../types';
import { formatEuro } from './incomeUtils';
import { IncomeLabels } from './useIncomeLabels';

interface Props {
  summary: IncomeSummary;
  chipColors: IncomeLabels['chipColors'];
  kindLabel: IncomeLabels['kindLabel'];
}

export function IncomeDistributionCard({ summary, chipColors, kindLabel }: Props) {
  const { t } = useTranslation();

  return (
    <Card
      elevation={0}
      sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3, mb: 3 }}
    >
      <CardContent>
        <Box
          sx={{
            display: 'flex',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 1,
            mb: 1.5,
          }}
        >
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            {t('pages.income.distribution.title')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t('pages.income.distribution.total', { amount: formatEuro(summary.paidAmount) })}
          </Typography>
        </Box>
        <Box
          role="img"
          aria-label={t('pages.income.distribution.title')}
          sx={{
            display: 'flex',
            height: 16,
            borderRadius: 1,
            overflow: 'hidden',
            gap: '2px',
            mb: 1.5,
          }}
        >
          {summary.breakdown.map((b) => (
            <Box
              key={`${b.kind}-${b.categoryId ?? ''}`}
              title={kindLabel(b)}
              sx={{
                width: `${(b.amount / summary.paidAmount) * 100}%`,
                bgcolor: chipColors(b.kind, b.categoryId),
              }}
            />
          ))}
        </Box>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
          {summary.breakdown.map((b) => (
            <Box
              key={`${b.kind}-${b.categoryId ?? ''}`}
              sx={{ display: 'flex', alignItems: 'center', gap: 1 }}
            >
              <Box
                sx={{
                  width: 10,
                  height: 10,
                  borderRadius: 0.5,
                  bgcolor: chipColors(b.kind, b.categoryId),
                }}
              />
              <Typography variant="body2">{kindLabel(b)}</Typography>
              <Typography variant="body2" color="text.secondary">
                {formatEuro(b.amount)} · {Math.round((b.amount / summary.paidAmount) * 100)} %
              </Typography>
            </Box>
          ))}
        </Box>
      </CardContent>
    </Card>
  );
}
