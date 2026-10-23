import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Button,
  ButtonProps,
  Chip,
  Grid,
  Card,
  CardContent,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Checkbox,
  FormControlLabel,
  IconButton,
  Tooltip,
  CircularProgress,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import FilterAltOffIcon from '@mui/icons-material/FilterAltOff';
import EuroIcon from '@mui/icons-material/Euro';
import DeleteIcon from '@mui/icons-material/Delete';
import RestoreFromTrashIcon from '@mui/icons-material/RestoreFromTrash';
import { api } from '../api';
import { useTranslation } from 'react-i18next';
import { Contribution, ContributionInfo, MembershipStatus, Role, UserRoleProps } from '../types';
import { UUIDTypes } from 'uuid';
import ResponsiveTablePagination from '../components/ResponsiveTablePagination';
import debounce from 'lodash.debounce';
import { MobileListCard } from '../components/MobileListCard';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';

export default function Contributions({ role }: UserRoleProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [data, setData] = useState<Contribution[]>([]);
  const [paymentData, setPaymentData] = useState<ContributionInfo>({
    openPayments: 0,
    openAmount: 0,
  });
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);
  const [search, setSearch] = useState('');
  const [showUnpaid, setShowUnpaid] = useState<boolean | undefined>(undefined);
  const [membershipStatus, setMembershipStatus] = useState<MembershipStatus | undefined>(undefined);
  const [showDeleted, setShowDeleted] = useState(false);
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const setContributionChange = useSnackbar();
  const { t } = useTranslation();
  const isAdmin = role === Role.ADMIN;
  const canManage = role === Role.ADMIN || role === Role.FINANCIAL_MANAGER;
  const isFiltered =
    search !== '' || showUnpaid !== undefined || membershipStatus !== undefined || showDeleted;

  const fetchData = useCallback(
    async (
      p: number,
      l: number,
      n: string,
      u: boolean | undefined,
      ms: MembershipStatus | undefined,
      d: boolean,
    ) => {
      setLoading(true);
      try {
        const offset = p * l;
        const params = new URLSearchParams({
          name: n !== undefined ? n : '',
          limit: l.toString(),
          offset: offset.toString(),
        });

        if (u !== undefined) {
          params.append('unpaid', u.toString());
        }

        if (ms !== undefined) {
          params.append('membershipStatus', ms);
        }

        if (d) {
          params.append('deleted', 'true');
        }

        const response = await api(`/contributions?${params.toString()}`);
        setData(response.items);
        setTotalCount(response.total);
      } catch (error) {
        console.error('Loading error:', error);
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  const fetchInfo = useCallback(async () => {
    try {
      const response = await api(`/contributions/info`);
      setPaymentData(response);
    } catch (error) {
      console.error('Loading error:', error);
    }
  }, []);

  const debouncedFetch = useMemo(
    () =>
      debounce(
        (
          p: number,
          l: number,
          n: string,
          u: boolean | undefined,
          ms: MembershipStatus | undefined,
          d: boolean,
        ) => fetchData(p, l, n, u, ms, d),
        500,
      ),
    [fetchData],
  );

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchInfo().catch();
  }, [fetchInfo]);

  useEffect(() => {
    debouncedFetch(page, rowsPerPage, search, showUnpaid, membershipStatus, showDeleted);
  }, [page, rowsPerPage, search, showUnpaid, membershipStatus, showDeleted, debouncedFetch]);

  const reload = async () => {
    await fetchData(page, rowsPerPage, search, showUnpaid, membershipStatus, showDeleted);
    fetchInfo().catch();
  };

  const markAsPaid = async (id: UUIDTypes, paid: boolean) => {
    await api(`/contributions/${id}?paid=${paid ? 'true' : 'false'}`, { method: 'POST' });
    debouncedFetch(page, rowsPerPage, search, showUnpaid, membershipStatus, showDeleted);
    fetchInfo().catch();
  };

  const remove = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.contributions.confirmDialog.deleteQuestion'),
      buttonText: t('pages.contributions.confirmDialog.delete'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (!confirmed) return;
    try {
      await api(`/contributions/${id}`, { method: 'DELETE' });
      setContributionChange({
        status: 'success',
        message: t('pages.contributions.responseMessages.deleted'),
      });
    } catch {
      setContributionChange({
        status: 'error',
        message: t('pages.contributions.responseMessages.deleteError'),
      });
    }
    await reload();
  };

  const restore = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.contributions.confirmDialog.restoreQuestion'),
      buttonText: t('pages.contributions.confirmDialog.restore'),
      confirmColor: 'success',
    });
    const confirmed = await confirm();
    if (!confirmed) return;
    try {
      await api(`/contributions/${id}/restore`, { method: 'POST' });
      setContributionChange({
        status: 'success',
        message: t('pages.contributions.responseMessages.restored'),
      });
    } catch {
      setContributionChange({
        status: 'error',
        message: t('pages.contributions.responseMessages.restoreError'),
      });
    }
    await reload();
  };

  const renderActions = (c: Contribution) => {
    if (c.deletedAt !== null) {
      return isAdmin ? (
        <Tooltip title={t('pages.contributions.button.restore')}>
          <IconButton onClick={() => restore(c.id)} size="small" color="primary">
            <RestoreFromTrashIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      ) : null;
    }
    if (!canManage) return null;
    return (
      <>
        <Button
          variant={!c.paid ? 'contained' : 'outlined'}
          color={!c.paid ? 'success' : 'warning'}
          size="small"
          startIcon={<EuroIcon />}
          onClick={() => markAsPaid(c.id, !c.paid)}
          sx={{ textTransform: 'none', borderRadius: 1.5 }}
        >
          {t(`pages.contributions.button.${!c.paid ? 'markAsPaid' : 'markAsUnPaid'}`)}
        </Button>
        {!c.paid && (
          <Tooltip title={t('pages.contributions.button.delete')}>
            <IconButton onClick={() => remove(c.id)} size="small" color="error" sx={{ ml: 1 }}>
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </>
    );
  };

  return (
    <Box>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
      />
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {t('pages.contributions.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('pages.contributions.description')}
        </Typography>
      </Box>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Card
            elevation={0}
            sx={{
              border: '1px solid',
              borderColor: 'warning.light',
              borderRadius: 3,
              bgcolor: 'warning.50',
              height: '100%',
            }}
          >
            <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2, height: '100%' }}>
              <PendingActionsIcon color="warning" sx={{ fontSize: 40 }} />
              <Box>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  {paymentData.openAmount.toFixed(2)} €
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {t('pages.contributions.openDemands', { count: paymentData.openPayments })}
                </Typography>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Card
            elevation={0}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 3,
              height: '100%',
            }}
          >
            <CardContent
              sx={{
                display: 'flex',
                alignItems: 'center',
                height: '100%',
                boxSizing: 'border-box',
              }}
            >
              <TextField
                label={t('pages.contributions.filter.searchName')}
                fullWidth
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(0);
                }}
                variant="standard"
                slotProps={{ input: { disableUnderline: false } }}
              />
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Card
            elevation={0}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 3,
              height: '100%',
            }}
          >
            <CardContent
              sx={{
                display: 'flex',
                alignItems: 'center',
                height: '100%',
                boxSizing: 'border-box',
              }}
            >
              <FormControl fullWidth>
                <InputLabel>{t('pages.contributions.filter.status')}</InputLabel>
                <Select
                  value={showUnpaid === undefined ? 'all' : String(showUnpaid)}
                  label={t('pages.contributions.filter.status')}
                  onChange={(e) => {
                    setShowUnpaid(e.target.value === 'all' ? undefined : e.target.value === 'true');
                    setPage(0);
                  }}
                >
                  <MenuItem value="all">{t('pages.contributions.filter.all')}</MenuItem>
                  <MenuItem value="false">{t('pages.contributions.filter.paid')}</MenuItem>
                  <MenuItem value="true">{t('pages.contributions.filter.unpaid')}</MenuItem>
                </Select>
              </FormControl>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Card
            elevation={0}
            sx={{
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 3,
              height: '100%',
            }}
          >
            <CardContent
              sx={{
                display: 'flex',
                alignItems: 'center',
                height: '100%',
                boxSizing: 'border-box',
              }}
            >
              <FormControl fullWidth>
                <InputLabel>{t('pages.contributions.filter.membershipStatus')}</InputLabel>
                <Select
                  value={membershipStatus ?? 'all'}
                  label={t('pages.contributions.filter.membershipStatus')}
                  onChange={(e) => {
                    const value = e.target.value;
                    setMembershipStatus(value === 'all' ? undefined : (value as MembershipStatus));
                    setPage(0);
                  }}
                >
                  <MenuItem value="all">
                    {t('pages.contributions.filter.allMembershipStatus')}
                  </MenuItem>
                  {Object.values(MembershipStatus).map((value) => (
                    <MenuItem key={value} value={value}>
                      {t(`components.membershipStatusOptions.${value}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </CardContent>
          </Card>
        </Grid>

        {isAdmin && (
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <Card
              elevation={0}
              sx={{
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 3,
                height: '100%',
              }}
            >
              <CardContent
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  height: '100%',
                  boxSizing: 'border-box',
                }}
              >
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
                  label={t('pages.contributions.filter.showDeleted')}
                />
              </CardContent>
            </Card>
          </Grid>
        )}

        {isFiltered && (
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <Card
              elevation={0}
              sx={{
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 3,
                height: '100%',
              }}
            >
              <CardContent
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  height: '100%',
                  boxSizing: 'border-box',
                }}
              >
                <Button
                  variant="outlined"
                  color="secondary"
                  fullWidth
                  startIcon={<FilterAltOffIcon />}
                  onClick={() => {
                    setSearch('');
                    setShowUnpaid(undefined);
                    setMembershipStatus(undefined);
                    setShowDeleted(false);
                    setPage(0);
                  }}
                >
                  {t('pages.contributions.filter.resetFilter')}
                </Button>
              </CardContent>
            </Card>
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
          {data.map((c) => (
            <MobileListCard
              key={c.id.toString()}
              primary={
                <Box sx={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
                  <Typography sx={{ fontWeight: 600 }}>
                    {c.memberNumber} · {c.name}
                  </Typography>
                  {c.paid ? (
                    <Chip
                      label={t('pages.contributions.status.paid')}
                      color="success"
                      size="small"
                      icon={<CheckCircleIcon />}
                      sx={{ borderRadius: 1, fontWeight: 600 }}
                    />
                  ) : (
                    <Chip
                      label={t('pages.contributions.status.open')}
                      color="warning"
                      size="small"
                      variant="outlined"
                      sx={{ borderRadius: 1, fontWeight: 600 }}
                    />
                  )}
                </Box>
              }
              secondaryRows={[
                {
                  label: t('pages.contributions.table.membershipStatus'),
                  value: t(`components.membershipStatusOptions.${c.membershipStatus}`),
                },
                {
                  label: t('pages.contributions.table.amount'),
                  value: c.amount.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' }),
                },
                {
                  label: t('pages.contributions.table.dueDate'),
                  value: new Date(c.dueDate).toLocaleDateString('de-DE'),
                },
              ]}
              actions={renderActions(c)}
            />
          ))}
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
            labelRowsPerPage={t('pages.contributions.table.rowsPerPage')}
          />
        </Box>
      ) : (
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
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.memberNumber')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.member')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.membershipStatus')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.amount')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.dueDate')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.status')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.contributions.table.action')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.map((c) => (
                <TableRow key={c.id.toString()} hover>
                  <TableCell>
                    <Chip
                      label={c.memberNumber}
                      size="small"
                      variant="outlined"
                      sx={{ fontWeight: 500 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>{c.name}</TableCell>
                  <TableCell color="text.secondary">
                    {t(`components.membershipStatusOptions.${c.membershipStatus}`)}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {c.amount.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' })}
                    </Typography>
                  </TableCell>
                  <TableCell color="text.secondary">
                    {new Date(c.dueDate).toLocaleDateString('de-DE')}
                  </TableCell>
                  <TableCell>
                    {c.paid ? (
                      <Chip
                        label={t('pages.contributions.status.paid')}
                        color="success"
                        size="small"
                        icon={<CheckCircleIcon />}
                        sx={{ borderRadius: 1, fontWeight: 600 }}
                      />
                    ) : (
                      <Chip
                        label={t('pages.contributions.status.open')}
                        color="warning"
                        size="small"
                        variant="outlined"
                        sx={{ borderRadius: 1, fontWeight: 600 }}
                      />
                    )}
                  </TableCell>
                  <TableCell align="right">{renderActions(c)}</TableCell>
                </TableRow>
              ))}
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
            labelRowsPerPage={t('pages.contributions.table.rowsPerPage')}
          />
        </TableContainer>
      )}
    </Box>
  );
}
