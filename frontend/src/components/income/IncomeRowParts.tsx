import { ReactNode } from 'react';
import { Box, Button, Chip, IconButton, Tooltip } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import EuroIcon from '@mui/icons-material/Euro';
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong';
import RestoreFromTrashIcon from '@mui/icons-material/RestoreFromTrash';
import { useTranslation } from 'react-i18next';
import { IncomeRow, IncomeSource } from '../../types';
import { IncomeLabels } from './useIncomeLabels';

export interface IncomeRowsViewProps {
  rows: IncomeRow[];
  loading: boolean;
  isAdmin: boolean;
  canManage: boolean;
  labels: IncomeLabels;
  pagination: ReactNode;
  onTogglePaid: (row: IncomeRow, paid: boolean) => void;
  onAcknowledgement: (row: IncomeRow) => void;
  onEdit: (row: IncomeRow) => void;
  onDelete: (row: IncomeRow) => void;
  onRestore: (row: IncomeRow) => void;
}

export function IncomeStatusChip({ row }: { row: IncomeRow }) {
  const { t } = useTranslation();

  return row.paid ? (
    <Chip
      label={t('pages.income.status.paid')}
      color="success"
      size="small"
      icon={<CheckCircleIcon />}
      sx={{ borderRadius: 1, fontWeight: 600 }}
    />
  ) : (
    <Chip
      label={t('pages.income.status.open')}
      color="warning"
      size="small"
      variant="outlined"
      sx={{ borderRadius: 1, fontWeight: 600 }}
    />
  );
}

export function IncomeKindChip({
  row,
  chipColors,
  kindLabel,
}: {
  row: IncomeRow;
  chipColors: IncomeLabels['chipColors'];
  kindLabel: IncomeLabels['kindLabel'];
}) {
  const color = chipColors(row.kind, row.categoryId);
  return (
    <Chip
      label={kindLabel(row)}
      size="small"
      variant="outlined"
      sx={{ fontWeight: 500, color, borderColor: color }}
    />
  );
}

export function IncomePaymentToggle({
  row,
  canManage,
  onToggle,
}: {
  row: IncomeRow;
  canManage: boolean;
  onToggle: (row: IncomeRow, paid: boolean) => void;
}) {
  const { t } = useTranslation();

  if (row.deletedAt !== null || !canManage) return null;
  return (
    <Tooltip title={row.acknowledgementNumber ? t('pages.income.tooltip.reopenBlocked') : ''}>
      <span>
        <Button
          variant={!row.paid ? 'contained' : 'outlined'}
          color={!row.paid ? 'success' : 'warning'}
          size="small"
          startIcon={<EuroIcon />}
          disabled={row.paid && !!row.acknowledgementNumber}
          onClick={() => onToggle(row, !row.paid)}
          sx={{ textTransform: 'none', borderRadius: 1.5, whiteSpace: 'nowrap' }}
        >
          {t(`pages.income.button.${!row.paid ? 'markAsPaid' : 'markAsUnPaid'}`)}
        </Button>
      </span>
    </Tooltip>
  );
}

// Icon actions always occupy the same three slots (acknowledgement, edit, delete / restore), so the
// icons line up from row to row even when some of them are not available.
function ActionSlot({ children }: { children: ReactNode }) {
  return (
    <Box
      sx={{
        width: 34,
        height: 34,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {children}
    </Box>
  );
}

export function IncomeRowActions({
  row,
  isAdmin,
  canManage,
  onAcknowledgement,
  onEdit,
  onDelete,
  onRestore,
}: {
  row: IncomeRow;
  isAdmin: boolean;
  canManage: boolean;
  onAcknowledgement: (row: IncomeRow) => void;
  onEdit: (row: IncomeRow) => void;
  onDelete: (row: IncomeRow) => void;
  onRestore: (row: IncomeRow) => void;
}) {
  const { t } = useTranslation();

  if (row.deletedAt !== null) {
    return isAdmin ? (
      <ActionSlot>
        <Tooltip title={t('pages.income.button.restore')}>
          <IconButton onClick={() => onRestore(row)} size="small" color="primary">
            <RestoreFromTrashIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </ActionSlot>
    ) : null;
  }
  if (!canManage) return null;

  return (
    <Box sx={{ display: 'inline-flex', justifyContent: 'flex-end' }}>
      <ActionSlot>
        {row.paid && (
          <Tooltip
            title={
              row.acknowledgementNumber
                ? t('pages.income.button.acknowledgementDownload', {
                    number: row.acknowledgementNumber,
                  })
                : t('pages.income.button.acknowledgement')
            }
          >
            <IconButton
              onClick={() => onAcknowledgement(row)}
              size="small"
              color="primary"
              aria-label={t('pages.income.button.acknowledgement')}
            >
              <ReceiptLongIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </ActionSlot>
      <ActionSlot>
        {row.source === IncomeSource.MANUAL && !row.acknowledgementNumber && (
          <Tooltip title={t('pages.income.button.edit')}>
            <IconButton onClick={() => onEdit(row)} size="small" color="primary">
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </ActionSlot>
      <ActionSlot>
        {!row.paid && (
          <Tooltip title={t('pages.income.button.delete')}>
            <IconButton onClick={() => onDelete(row)} size="small" color="error">
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </ActionSlot>
    </Box>
  );
}
