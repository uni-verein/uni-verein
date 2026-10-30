import { Box, CircularProgress, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { MobileListCard } from '../MobileListCard';
import { formatDate, formatEuro } from './incomeUtils';
import {
  IncomeKindChip,
  IncomePaymentToggle,
  IncomeRowActions,
  IncomeRowsViewProps,
  IncomeStatusChip,
} from './IncomeRowParts';

export function IncomeMobileList({
  rows,
  loading,
  isAdmin,
  canManage,
  labels,
  pagination,
  onTogglePaid,
  onAcknowledgement,
  onEdit,
  onDelete,
  onRestore,
}: IncomeRowsViewProps) {
  const { t } = useTranslation();

  return (
    <Box sx={{ position: 'relative' }}>
      {loading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
          <CircularProgress />
        </Box>
      )}
      {rows.map((r) => (
        <MobileListCard
          key={`${r.source}-${r.id}`}
          primary={
            <Box sx={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
              <Typography sx={{ fontWeight: 600 }}>{r.payer}</Typography>
              <IncomeStatusChip row={r} />
            </Box>
          }
          secondaryRows={[
            {
              label: t('pages.income.table.kind'),
              value: (
                <IncomeKindChip
                  row={r}
                  chipColors={labels.chipColors}
                  kindLabel={labels.kindLabel}
                />
              ),
            },
            { label: t('pages.income.table.source'), value: labels.sourceLabel(r) },
            ...(r.description
              ? [{ label: t('pages.income.table.description'), value: r.description }]
              : []),
            { label: t('pages.income.table.amount'), value: formatEuro(r.amount) },
            { label: t('pages.income.table.dueDate'), value: formatDate(r.dueDate) },
            ...(r.bookingNumber
              ? [{ label: t('pages.income.table.bookingNumber'), value: r.bookingNumber }]
              : []),
            ...(r.paymentDate
              ? [{ label: t('pages.income.table.paymentDate'), value: formatDate(r.paymentDate) }]
              : []),
          ]}
          actions={
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <IncomePaymentToggle row={r} canManage={canManage} onToggle={onTogglePaid} />
              <IncomeRowActions
                row={r}
                isAdmin={isAdmin}
                canManage={canManage}
                onAcknowledgement={onAcknowledgement}
                onEdit={onEdit}
                onDelete={onDelete}
                onRestore={onRestore}
              />
            </Box>
          }
        />
      ))}
      {pagination}
    </Box>
  );
}
