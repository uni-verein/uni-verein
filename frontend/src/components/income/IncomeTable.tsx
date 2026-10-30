import {
  Box,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { formatDate, formatEuro } from './incomeUtils';
import {
  IncomeKindChip,
  IncomePaymentToggle,
  IncomeRowActions,
  IncomeRowsViewProps,
  IncomeStatusChip,
} from './IncomeRowParts';

const HEADER_KEYS = ['dueDate', 'payer', 'kind', 'description', 'amount', 'status'];

export function IncomeTable({
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
    <TableContainer
      component={Paper}
      elevation={0}
      sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3 }}
    >
      {loading && (
        <Box
          sx={{
            position: 'absolute',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            zIndex: 1,
          }}
        >
          <CircularProgress />
        </Box>
      )}

      <Table>
        <TableHead sx={{ bgcolor: 'action.hover' }}>
          <TableRow>
            {HEADER_KEYS.map((key) => (
              <TableCell key={key} sx={{ fontWeight: 700 }}>
                {t(`pages.income.table.${key}`)}
              </TableCell>
            ))}
            {canManage && (
              <>
                <TableCell sx={{ fontWeight: 700 }}>{t('pages.income.table.payment')}</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.income.table.action')}
                </TableCell>
              </>
            )}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((r) => (
            <TableRow key={`${r.source}-${r.id}`} hover>
              <TableCell>{formatDate(r.dueDate)}</TableCell>
              <TableCell>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                  {r.payer}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {labels.sourceLabel(r)}
                </Typography>
              </TableCell>
              <TableCell>
                <IncomeKindChip
                  row={r}
                  chipColors={labels.chipColors}
                  kindLabel={labels.kindLabel}
                />
              </TableCell>
              <TableCell sx={{ color: 'text.secondary' }}>{r.description ?? ''}</TableCell>
              <TableCell>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {formatEuro(r.amount)}
                </Typography>
              </TableCell>
              <TableCell>
                <IncomeStatusChip row={r} />
                {r.bookingNumber && (
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ display: 'block', mt: 0.5 }}
                  >
                    {r.bookingNumber}
                    {r.paymentDate ? ` · ${formatDate(r.paymentDate)}` : ''}
                  </Typography>
                )}
              </TableCell>
              {canManage && (
                <>
                  <TableCell>
                    <IncomePaymentToggle row={r} canManage={canManage} onToggle={onTogglePaid} />
                  </TableCell>
                  <TableCell align="right">
                    <IncomeRowActions
                      row={r}
                      isAdmin={isAdmin}
                      canManage={canManage}
                      onAcknowledgement={onAcknowledgement}
                      onEdit={onEdit}
                      onDelete={onDelete}
                      onRestore={onRestore}
                    />
                  </TableCell>
                </>
              )}
            </TableRow>
          ))}
          {rows.length === 0 && !loading && (
            <TableRow>
              <TableCell
                colSpan={canManage ? HEADER_KEYS.length + 2 : HEADER_KEYS.length}
                align="center"
                sx={{ py: 5, color: 'text.secondary' }}
              >
                {t('pages.income.table.empty')}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      {pagination}
    </TableContainer>
  );
}
