import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  ButtonProps,
  CircularProgress,
  IconButton,
  Paper,
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
import RateReviewIcon from '@mui/icons-material/RateReview';
import DeleteIcon from '@mui/icons-material/Delete';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { ContributionPlans, MemberCategory, PendingSelfEnrollment } from '../types';
import ResponsiveTablePagination from './ResponsiveTablePagination';
import { MobileListCard } from './MobileListCard';
import { ConfirmDialog } from './dialogs/ConfirmDialog';
import PendingEnrollmentForm from './dialogs/PendingEnrollmentForm';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';

export function PendingEnrollmentsTab({
  onMemberCreated,
  onPendingCountChange,
  contributionPlans,
  memberCategories,
}: {
  onMemberCreated: () => void;
  onPendingCountChange?: (count: number) => void;
  contributionPlans: ContributionPlans[];
  memberCategories: MemberCategory[];
}) {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [rejectReason, setRejectReason] = useState('');
  const rejectReasonRef = useRef('');
  const setSnackbar = useSnackbar();

  const [items, setItems] = useState<PendingSelfEnrollment[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);
  const [editId, setEditId] = useState<string | null>(null);

  const fetchData = useCallback(
    async (p: number, l: number) => {
      setLoading(true);
      try {
        const params = new URLSearchParams({
          offset: (p * l).toString(),
          limit: l.toString(),
        });
        const response = await api(`/pending-self-enrollments?${params.toString()}`);
        setItems(response.items);
        setTotalCount(response.total);
        onPendingCountChange?.(response.total);
      } catch (error) {
        console.error('Loading error:', error);
      } finally {
        setLoading(false);
      }
    },
    [onPendingCountChange],
  );

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchData(page, rowsPerPage);
  }, [page, rowsPerPage, fetchData]);

  const reject = async (item: PendingSelfEnrollment) => {
    setRejectReason('');
    rejectReasonRef.current = '';
    setConfirmDialog({
      message: t('components.pendingEnrollments.confirmDialog.rejectQuestion', {
        name: `${item.firstName} ${item.lastName}`,
      }),
      buttonText: t('components.pendingEnrollments.confirmDialog.reject'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (!confirmed) return;

    try {
      await api(`/pending-self-enrollments/${item.id}/reject`, {
        method: 'POST',
        body: JSON.stringify({ reason: rejectReasonRef.current.trim() || null }),
      });
      setSnackbar({
        status: 'success',
        message: t('components.pendingEnrollments.responseMessages.rejected'),
      });
    } catch {
      setSnackbar({
        status: 'error',
        message: t('components.pendingEnrollments.responseMessages.rejectError'),
      });
    }
    await fetchData(page, rowsPerPage);
  };

  return (
    <Box>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
        textFieldLabel={t('components.pendingEnrollments.confirmDialog.reasonLabel')}
        textFieldValue={rejectReason}
        onTextFieldChange={(value) => {
          setRejectReason(value);
          rejectReasonRef.current = value;
        }}
      />

      <Alert severity="info" sx={{ mb: 2 }}>
        {t('components.pendingEnrollments.info')}
      </Alert>

      {isMobile ? (
        <Box sx={{ position: 'relative' }}>
          {loading && (
            <Box
              sx={{
                position: 'absolute',
                top: 16,
                left: '50%',
                transform: 'translateX(-50%)',
                zIndex: 1,
              }}
            >
              <CircularProgress size={28} />
            </Box>
          )}
          {items.length === 0 ? (
            <Typography align="center" color="text.secondary" sx={{ py: 3 }}>
              {t('components.pendingEnrollments.table.noItems')}
            </Typography>
          ) : (
            items.map((item) => (
              <MobileListCard
                key={item.id.toString()}
                primary={
                  <Typography sx={{ fontWeight: 600 }}>
                    {item.firstName} {item.middleName} {item.lastName}
                  </Typography>
                }
                secondaryRows={[
                  { label: t('components.pendingEnrollments.table.colEmail'), value: item.email },
                  {
                    label: t('components.pendingEnrollments.table.colMemberCategory'),
                    value:
                      item.memberCategoryName ??
                      t('components.pendingEnrollments.table.categoryNotAssigned'),
                  },
                  {
                    label: t('components.pendingEnrollments.table.colSubmittedAt'),
                    value: new Date(item.submittedAt).toLocaleString(),
                  },
                ]}
                actions={
                  <>
                    <Tooltip title={t('components.pendingEnrollments.actions.edit')}>
                      <IconButton
                        onClick={() => setEditId(item.id.toString())}
                        size="small"
                        color="primary"
                      >
                        <RateReviewIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t('components.pendingEnrollments.actions.reject')}>
                      <IconButton onClick={() => reject(item)} size="small" color="error">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </>
                }
              />
            ))
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
            labelRowsPerPage={t('components.pendingEnrollments.table.rowsPerPage')}
          />
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          elevation={0}
          sx={{
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 2,
            position: 'relative',
          }}
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

          <Table
            sx={{ minWidth: 650 }}
            aria-label={t('components.pendingEnrollments.table.ariaLabel')}
          >
            <TableHead sx={{ bgcolor: 'action.hover' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('components.pendingEnrollments.table.colName')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('components.pendingEnrollments.table.colEmail')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('components.pendingEnrollments.table.colMemberCategory')}
                </TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>
                  {t('components.pendingEnrollments.table.colSubmittedAt')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 'bold', minWidth: '110px' }}>
                  {t('components.pendingEnrollments.table.colActions')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                    {t('components.pendingEnrollments.table.noItems')}
                  </TableCell>
                </TableRow>
              ) : (
                items.map((item) => (
                  <TableRow
                    key={item.id.toString()}
                    hover
                    sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                  >
                    <TableCell sx={{ fontWeight: 500 }}>
                      {item.firstName} {item.middleName} {item.lastName}
                    </TableCell>
                    <TableCell color="text.secondary">{item.email}</TableCell>
                    <TableCell sx={{ fontFamily: 'monospace', color: 'text.secondary' }}>
                      {item.memberCategoryName ??
                        t('components.pendingEnrollments.table.categoryNotAssigned')}
                    </TableCell>
                    <TableCell color="text.secondary">
                      {new Date(item.submittedAt).toLocaleString()}
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title={t('components.pendingEnrollments.actions.edit')}>
                        <IconButton
                          onClick={() => setEditId(item.id.toString())}
                          size="small"
                          color="primary"
                        >
                          <RateReviewIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('components.pendingEnrollments.actions.reject')}>
                        <IconButton onClick={() => reject(item)} size="small" color="error">
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))
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
            labelRowsPerPage={t('components.pendingEnrollments.table.rowsPerPage')}
          />
        </TableContainer>
      )}

      {editId && (
        <PendingEnrollmentForm
          id={editId}
          contributionPlans={contributionPlans}
          memberCategories={memberCategories}
          onMemberCreated={onMemberCreated}
          onClose={() => {
            setEditId(null);
            fetchData(page, rowsPerPage);
          }}
        />
      )}
    </Box>
  );
}
