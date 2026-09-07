import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { UUIDTypes } from 'uuid';
import {
  Box,
  Button,
  ButtonProps,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import debounce from 'lodash.debounce';
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong';
import VisibilityIcon from '@mui/icons-material/Visibility';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import DeleteForeverIcon from '@mui/icons-material/DeleteForever';
import RestoreFromTrashIcon from '@mui/icons-material/RestoreFromTrash';
import RefreshIcon from '@mui/icons-material/Refresh';
import DownloadIcon from '@mui/icons-material/Download';
import EuroIcon from '@mui/icons-material/Euro';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';

import { api } from '../api';
import { Receipt, ReceiptCategory, Role, UserRoleProps } from '../types';
import ReceiptForm from '../components/dialogs/ReceiptForm';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { MarkReceiptPaidDialog } from '../components/dialogs/MarkReceiptPaidDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';
import { MobileListCard } from '../components/MobileListCard';
import ResponsiveTablePagination from '../components/ResponsiveTablePagination';
import { useTranslation } from 'react-i18next';

// Keep in sync with ReceiptsController.EditWindow on the backend.
const RECEIPT_EDIT_WINDOW_MS = 15 * 60 * 1000;

function isReceiptEditable(receipt: Receipt, userId: string | undefined): boolean {
  return (
    receipt.deletedAt === null &&
    !receipt.paid &&
    receipt.userId.toString() === userId &&
    Date.now() - new Date(receipt.createdAt).getTime() <= RECEIPT_EDIT_WINDOW_MS
  );
}

function ReceiptStatusChip({ paid, t }: { paid: boolean; t: (key: string) => string }) {
  return paid ? (
    <Chip
      label={t('pages.receipts.status.paid')}
      color="success"
      size="small"
      icon={<CheckCircleIcon />}
      sx={{ borderRadius: 1, fontWeight: 600 }}
    />
  ) : (
    <Chip
      label={t('pages.receipts.status.open')}
      color="warning"
      size="small"
      variant="outlined"
      sx={{ borderRadius: 1, fontWeight: 600 }}
    />
  );
}

export default function Receipts({ role, userId }: UserRoleProps & { userId?: UUIDTypes }) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const isPrivileged = role === Role.ADMIN || role === Role.FINANCIAL_MANAGER;
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const setSnackbar = useSnackbar();
  const { t } = useTranslation();

  const [receipts, setReceipts] = useState<Receipt[]>([]);
  const [categories, setCategories] = useState<ReceiptCategory[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [viewReceipt, setViewReceipt] = useState<Receipt | null>(null);
  const [editReceipt, setEditReceipt] = useState<Receipt | null>(null);
  const [markPaidReceipt, setMarkPaidReceipt] = useState<Receipt | null>(null);

  const [dateFrom, setDateFrom] = useState<Date | null>(null);
  const [dateTo, setDateTo] = useState<Date | null>(null);
  const [categoryId, setCategoryId] = useState<string>('');
  const [showDeleted, setShowDeleted] = useState(false);
  const [paidFilter, setPaidFilter] = useState<boolean | undefined>(undefined);

  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);

  const loadCategories = useCallback(async () => {
    const data = await api('/receipt-categories');
    if (data) setCategories(data.items);
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadCategories();
  }, [loadCategories]);

  const fetchData = useCallback(
    async (
      from: Date | null,
      to: Date | null,
      cat: string,
      deleted: boolean,
      paid: boolean | undefined,
      p: number,
      l: number,
    ) => {
      setLoading(true);
      try {
        const offset = p * l;
        const params = new URLSearchParams({
          limit: l.toString(),
          offset: offset.toString(),
        });
        if (from) params.set('dateFrom', from.toISOString());
        if (to) params.set('dateTo', to.toISOString());
        if (cat) params.set('categoryId', cat);
        if (deleted) params.set('deleted', 'true');
        if (paid !== undefined) params.set('paid', paid ? 'true' : 'false');

        const response = await api(`/receipts?${params.toString()}`);
        setReceipts(response.items);
        setTotalCount(response.total);
      } catch (error) {
        console.error('Loading error:', error);
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  const debouncedFetch = useMemo(
    () =>
      debounce(
        (
          from: Date | null,
          to: Date | null,
          cat: string,
          deleted: boolean,
          paid: boolean | undefined,
          p: number,
          l: number,
        ) => fetchData(from, to, cat, deleted, paid, p, l),
        400,
      ),
    [fetchData],
  );

  useEffect(() => {
    debouncedFetch(dateFrom, dateTo, categoryId, showDeleted, paidFilter, page, rowsPerPage);
  }, [dateFrom, dateTo, categoryId, showDeleted, paidFilter, page, rowsPerPage, debouncedFetch]);

  const reload = () =>
    fetchData(dateFrom, dateTo, categoryId, showDeleted, paidFilter, page, rowsPerPage);

  async function remove(id: string) {
    setConfirmDialog({
      message: t('pages.receipts.confirmDialog.deleteQuestion'),
      buttonText: t('pages.receipts.confirmDialog.delete'),
      confirmColor: 'error',
    });

    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/receipts/${id}`, { method: 'DELETE' });
        setSnackbar({ status: 'success', message: t('pages.receipts.responseMessages.deleted') });
      } catch {
        setSnackbar({ status: 'error', message: t('pages.receipts.responseMessages.deleteError') });
      }
      reload();
    }
  }

  async function hardRemove(id: string) {
    setConfirmDialog({
      message: t('pages.receipts.confirmDialog.hardDeleteQuestion'),
      buttonText: t('pages.receipts.confirmDialog.hardDelete'),
      confirmColor: 'error',
    });

    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/receipts/${id}/hard`, { method: 'DELETE' });
        setSnackbar({
          status: 'success',
          message: t('pages.receipts.responseMessages.hardDeleted'),
        });
      } catch {
        setSnackbar({
          status: 'error',
          message: t('pages.receipts.responseMessages.hardDeleteError'),
        });
      }
      reload();
    }
  }

  async function restore(id: string) {
    setConfirmDialog({
      message: t('pages.receipts.confirmDialog.restoreQuestion'),
      buttonText: t('pages.receipts.confirmDialog.restore'),
      confirmColor: 'success',
    });

    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/receipts/${id}`, { method: 'POST' });
        setSnackbar({ status: 'success', message: t('pages.receipts.responseMessages.restored') });
      } catch {
        setSnackbar({
          status: 'error',
          message: t('pages.receipts.responseMessages.restoreError'),
        });
      }
      reload();
    }
  }

  function exportZip() {
    const params = new URLSearchParams();
    if (dateFrom) params.set('dateFrom', dateFrom.toISOString());
    if (dateTo) params.set('dateTo', dateTo.toISOString());
    if (categoryId) params.set('categoryId', categoryId);

    const token = localStorage.getItem('token');
    if (token) params.set('access_token', token);

    setExporting(true);
    const a = document.createElement('a');
    a.href = `/api/receipts/export?${params.toString()}`;
    a.download = `receipts_export_${new Date().toISOString().split('T')[0]}.zip`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setSnackbar({ status: 'success', message: t('pages.receipts.responseMessages.exportSuccess') });
    setExporting(false);
  }

  const isFiltered =
    dateFrom !== null ||
    dateTo !== null ||
    categoryId !== '' ||
    showDeleted ||
    paidFilter !== undefined;

  const formatAmount = (amount: number) =>
    amount.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' });

  const rows = (
    <>
      {receipts.map((r) => (
        <React.Fragment key={r.id.toString()}>
          {isMobile ? (
            <MobileListCard
              primary={
                <Box
                  sx={{ display: 'flex', justifyContent: 'space-between', width: '100%', gap: 1 }}
                >
                  <Typography sx={{ fontWeight: 600 }}>
                    {formatAmount(r.amount)} · {dayjs(r.receiptDate).format('DD.MM.YYYY')}
                  </Typography>
                  <ReceiptStatusChip paid={r.paid} t={t} />
                </Box>
              }
              secondaryRows={[
                {
                  label: t('pages.receipts.table.colCategory'),
                  value: r.categoryName ?? '-',
                },
                { label: t('pages.receipts.table.colVendor'), value: r.vendor ?? '-' },
                ...(isPrivileged
                  ? [
                      {
                        label: t('pages.receipts.table.colSubmittedBy'),
                        value: r.userName ?? t('pages.receipts.deletedUser'),
                      },
                    ]
                  : []),
              ]}
              actions={
                <>
                  <Tooltip title={t('pages.receipts.actions.view')}>
                    <IconButton onClick={() => setViewReceipt(r)} size="small" color="primary">
                      <VisibilityIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  {isReceiptEditable(r, userId?.toString()) && (
                    <Tooltip title={t('pages.receipts.actions.edit')}>
                      <IconButton onClick={() => setEditReceipt(r)} size="small" color="primary">
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {isPrivileged && r.deletedAt === null && !r.paid && (
                    <Tooltip title={t('pages.receipts.actions.markAsPaid')}>
                      <IconButton
                        onClick={() => setMarkPaidReceipt(r)}
                        size="small"
                        color="success"
                      >
                        <EuroIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {r.deletedAt === null && (
                    <Tooltip title={t('pages.receipts.actions.delete')}>
                      <IconButton
                        onClick={() => remove(r.id.toString())}
                        size="small"
                        color="error"
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {role === Role.ADMIN && r.deletedAt !== null && (
                    <Tooltip title={t('pages.receipts.actions.restore')}>
                      <IconButton
                        onClick={() => restore(r.id.toString())}
                        size="small"
                        color="primary"
                      >
                        <RestoreFromTrashIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {role === Role.ADMIN && (
                    <Tooltip title={t('pages.receipts.actions.hardDelete')}>
                      <IconButton
                        onClick={() => hardRemove(r.id.toString())}
                        size="small"
                        color="error"
                      >
                        <DeleteForeverIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </>
              }
            />
          ) : (
            <TableRow hover sx={{ '&:last-child td, &:last-child th': { border: 0 } }}>
              <TableCell>{dayjs(r.receiptDate).format('DD.MM.YYYY')}</TableCell>
              <TableCell sx={{ fontFamily: 'monospace' }}>{formatAmount(r.amount)}</TableCell>
              <TableCell>{r.categoryName ?? '-'}</TableCell>
              <TableCell>{r.vendor ?? '-'}</TableCell>
              <TableCell>
                <ReceiptStatusChip paid={r.paid} t={t} />
              </TableCell>
              {isPrivileged && (
                <TableCell>{r.userName ?? t('pages.receipts.deletedUser')}</TableCell>
              )}
              <TableCell align="right">
                <Tooltip title={t('pages.receipts.actions.view')}>
                  <IconButton onClick={() => setViewReceipt(r)} size="small" color="primary">
                    <VisibilityIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                {isReceiptEditable(r, userId?.toString()) && (
                  <Tooltip title={t('pages.receipts.actions.edit')}>
                    <IconButton onClick={() => setEditReceipt(r)} size="small" color="primary">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                {isPrivileged && r.deletedAt === null && !r.paid && (
                  <Tooltip title={t('pages.receipts.actions.markAsPaid')}>
                    <IconButton onClick={() => setMarkPaidReceipt(r)} size="small" color="success">
                      <EuroIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                {r.deletedAt === null && (
                  <Tooltip title={t('pages.receipts.actions.delete')}>
                    <IconButton onClick={() => remove(r.id.toString())} size="small" color="error">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                {role === Role.ADMIN && r.deletedAt !== null && (
                  <Tooltip title={t('pages.receipts.actions.restore')}>
                    <IconButton
                      onClick={() => restore(r.id.toString())}
                      size="small"
                      color="primary"
                    >
                      <RestoreFromTrashIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                {role === Role.ADMIN && (
                  <Tooltip title={t('pages.receipts.actions.hardDelete')}>
                    <IconButton
                      onClick={() => hardRemove(r.id.toString())}
                      size="small"
                      color="error"
                    >
                      <DeleteForeverIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
              </TableCell>
            </TableRow>
          )}
        </React.Fragment>
      ))}
    </>
  );

  return (
    <Box>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
      />

      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 2,
          mb: 3,
        }}
      >
        <Typography variant="h5" sx={{ fontWeight: 700, color: 'text.primary' }}>
          {t('pages.receipts.title')}
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', width: { xs: '100%', sm: 'auto' } }}>
          <Tooltip title={t('pages.receipts.refresh')}>
            <IconButton onClick={reload}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
          {isPrivileged && (
            <Tooltip title={t('pages.receipts.exportZip')}>
              <span>
                <IconButton onClick={exportZip} disabled={exporting}>
                  <DownloadIcon />
                </IconButton>
              </span>
            </Tooltip>
          )}
          <Button
            variant="contained"
            startIcon={<ReceiptLongIcon />}
            onClick={() => setCreateOpen(true)}
            sx={{ borderRadius: 2, textTransform: 'none' }}
          >
            {t('pages.receipts.addReceipt')}
          </Button>
        </Box>
      </Box>

      {createOpen && (
        <ReceiptForm
          mode="create"
          receipt={null}
          categories={categories}
          role={role}
          onClose={() => setCreateOpen(false)}
          onSaved={() => {
            setCreateOpen(false);
            reload();
          }}
        />
      )}

      {viewReceipt && (
        <ReceiptForm
          mode="view"
          receipt={viewReceipt}
          categories={categories}
          role={role}
          onClose={() => setViewReceipt(null)}
          onSaved={() => setViewReceipt(null)}
        />
      )}

      {editReceipt && (
        <ReceiptForm
          mode="edit"
          receipt={editReceipt}
          categories={categories}
          role={role}
          onClose={() => setEditReceipt(null)}
          onSaved={() => {
            setEditReceipt(null);
            setSnackbar({
              status: 'success',
              message: t('pages.receipts.responseMessages.updated'),
            });
            reload();
          }}
        />
      )}

      {markPaidReceipt && (
        <MarkReceiptPaidDialog
          receipt={markPaidReceipt}
          onClose={() => setMarkPaidReceipt(null)}
          onSaved={() => {
            setMarkPaidReceipt(null);
            setSnackbar({
              status: 'success',
              message: t('pages.receipts.responseMessages.markedPaid'),
            });
            reload();
          }}
        />
      )}

      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
            <DatePicker
              label={t('pages.receipts.filter.dateFrom')}
              format="DD.MM.YYYY"
              value={dateFrom ? dayjs(dateFrom) : null}
              onChange={(v) => {
                setDateFrom(v ? v.toDate() : null);
                setPage(0);
              }}
              slotProps={{ textField: { fullWidth: true } }}
            />
          </LocalizationProvider>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
            <DatePicker
              label={t('pages.receipts.filter.dateTo')}
              format="DD.MM.YYYY"
              value={dateTo ? dayjs(dateTo) : null}
              onChange={(v) => {
                setDateTo(v ? v.toDate() : null);
                setPage(0);
              }}
              slotProps={{ textField: { fullWidth: true } }}
            />
          </LocalizationProvider>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <FormControl fullWidth>
            <InputLabel id="receipts-category-filter-label" shrink>
              {t('pages.receipts.filter.category')}
            </InputLabel>
            <Select
              labelId="receipts-category-filter-label"
              value={categoryId}
              label={t('pages.receipts.filter.category')}
              displayEmpty
              onChange={(e) => {
                setCategoryId(e.target.value);
                setPage(0);
              }}
            >
              <MenuItem value="">{t('pages.receipts.filter.allCategories')}</MenuItem>
              {categories.map((c) => (
                <MenuItem key={c.id.toString()} value={c.id.toString()}>
                  {c.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <FormControl fullWidth>
            <InputLabel id="receipts-status-filter-label" shrink>
              {t('pages.receipts.filter.status')}
            </InputLabel>
            <Select
              labelId="receipts-status-filter-label"
              value={paidFilter === undefined ? 'all' : String(paidFilter)}
              label={t('pages.receipts.filter.status')}
              displayEmpty
              onChange={(e) => {
                setPaidFilter(e.target.value === 'all' ? undefined : e.target.value === 'true');
                setPage(0);
              }}
            >
              <MenuItem value="all">{t('pages.receipts.filter.all')}</MenuItem>
              <MenuItem value="false">{t('pages.receipts.filter.open')}</MenuItem>
              <MenuItem value="true">{t('pages.receipts.filter.paid')}</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        {role === Role.ADMIN && (
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <FormControlLabel
              control={
                <Checkbox
                  checked={showDeleted}
                  onChange={(e) => {
                    setShowDeleted(e.target.checked);
                    setPage(0);
                  }}
                />
              }
              label={t('pages.receipts.filter.showDeleted')}
            />
          </Grid>
        )}
        {isFiltered && (
          <Grid size={{ xs: 12 }}>
            <Button
              variant="outlined"
              color="secondary"
              fullWidth
              onClick={() => {
                setDateFrom(null);
                setDateTo(null);
                setCategoryId('');
                setShowDeleted(false);
                setPaidFilter(undefined);
                setPage(0);
              }}
            >
              {t('pages.receipts.filter.resetFilter')}
            </Button>
          </Grid>
        )}
      </Grid>

      {isMobile ? (
        <Box sx={{ position: 'relative' }}>
          {loading && (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
              <CircularProgress />
            </Box>
          )}
          {!loading && receipts.length === 0 ? (
            <Typography color="text.secondary" sx={{ textAlign: 'center', py: 3 }}>
              {t('pages.receipts.table.noReceipts')}
            </Typography>
          ) : (
            rows
          )}
          <ResponsiveTablePagination
            component="div"
            count={totalCount}
            rowsPerPage={rowsPerPage}
            page={page}
            onPageChange={(_, newPage) => setPage(newPage)}
            onRowsPerPageChange={(e) => {
              setRowsPerPage(parseInt(e.target.value, 10));
              setPage(0);
            }}
            labelRowsPerPage={t('pages.receipts.table.rowsPerPage')}
          />
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          elevation={0}
          sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}
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

          <Table sx={{ minWidth: 650 }} aria-label={t('pages.receipts.table.ariaLabel')}>
            <TableHead sx={{ bgcolor: 'action.hover' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('pages.receipts.table.colDate')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('pages.receipts.table.colAmount')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('pages.receipts.table.colCategory')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('pages.receipts.table.colVendor')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('pages.receipts.table.colStatus')}
                </TableCell>
                {isPrivileged && (
                  <TableCell sx={{ fontWeight: 'bold' }}>
                    {t('pages.receipts.table.colSubmittedBy')}
                  </TableCell>
                )}
                <TableCell align="right" sx={{ fontWeight: 'bold', minWidth: '130px' }}>
                  {t('pages.receipts.table.colActions')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {receipts.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={isPrivileged ? 7 : 6}
                    align="center"
                    sx={{ py: 3, color: 'text.secondary' }}
                  >
                    {t('pages.receipts.table.noReceipts')}
                  </TableCell>
                </TableRow>
              ) : (
                rows
              )}
            </TableBody>
          </Table>

          <ResponsiveTablePagination
            component="div"
            count={totalCount}
            rowsPerPage={rowsPerPage}
            page={page}
            onPageChange={(_, newPage) => setPage(newPage)}
            onRowsPerPageChange={(e) => {
              setRowsPerPage(parseInt(e.target.value, 10));
              setPage(0);
            }}
            labelRowsPerPage={t('pages.receipts.table.rowsPerPage')}
          />
        </TableContainer>
      )}
    </Box>
  );
}
