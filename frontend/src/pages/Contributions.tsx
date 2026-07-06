import React, { useCallback, useEffect, useState } from 'react';
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
  Chip,
  Grid,
  Card,
  CardContent,
  TablePagination,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import FilterAltOffIcon from '@mui/icons-material/FilterAltOff';
import EuroIcon from '@mui/icons-material/Euro';
import { api } from '../api';
import { useTranslation } from 'react-i18next';
import { Role, UserRoleProps } from '../types';
import debounce from 'lodash.debounce';

export default function Contributions({ role }: UserRoleProps) {
  const [data, setData] = useState<any[]>([]);
  const [paymentData, setPaymentData] = useState<{ openPayments: number; openAmount: number }>({
    openPayments: 0,
    openAmount: 0,
  });
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);
  const [search, setSearch] = useState('');
  const [showUnpaid, setShowUnpaid] = useState<boolean | undefined>(undefined);
  const { t } = useTranslation();
  const isFiltered = search !== '' || showUnpaid !== undefined;

  const fetchData = async (p: number, l: number, n: string, u: boolean | undefined) => {
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

      const response = await api(`/contributions?${params.toString()}`);
      setData(response.items);
      setTotalCount(response.total);
    } catch (error) {
      console.error('Loading error:', error);
    } finally {
      setLoading(false);
    }
  };

  const fetchInfo = async () => {
    try {
      const response = await api(`/contributions/info`);
      setPaymentData(response);
    } catch (error) {
      console.error('Loading error:', error);
    }
  };

  const debouncedFetch = useCallback(
    // @ts-ignore
    debounce((...args: any) => fetchData(...args), 500),
    [],
  );

  useEffect(() => {
    fetchInfo().catch();
  }, []);

  useEffect(() => {
    debouncedFetch(page, rowsPerPage, search, showUnpaid);
  }, [page, rowsPerPage, search, showUnpaid, debouncedFetch]);

  const markAsPaid = async (id: number, paid: boolean) => {
    await api(`/contributions/${id}?paid=${paid ? 'true' : 'false'}`, { method: 'POST' });
    debouncedFetch(page, rowsPerPage, search, showUnpaid);
    fetchInfo().catch();
  };

  return (
    <Box>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {t('pages.contributions.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('pages.contributions.description')}
        </Typography>
      </Box>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={3}>
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

        <Grid size={3}>
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

        <Grid size={3}>
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

        {isFiltered && (
          <Grid size={3}>
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

      <TableContainer
        component={Paper}
        elevation={0}
        sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3 }}
      >
        <Table>
          <TableHead sx={{ bgcolor: 'grey.50' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>
                {t('pages.contributions.table.member')}
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
              <TableRow key={c.id} hover>
                <TableCell sx={{ fontWeight: 500 }}>{c.name}</TableCell>
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
                <TableCell align="right">
                  {role !== Role.USER && (
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
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        <TablePagination
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
    </Box>
  );
}
