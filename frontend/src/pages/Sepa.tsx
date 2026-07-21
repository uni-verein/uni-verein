import React, { useCallback, useEffect, useState } from 'react';
import {
  Box,
  Typography,
  Button,
  Alert,
  Grid,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import { api, apiFile } from '../api';
import { UUIDTypes } from 'uuid';
import { useTranslation } from 'react-i18next';
import debounce from 'lodash.debounce';
import { MobileListCard } from '../components/MobileListCard';
import ResponsiveTablePagination from '../components/ResponsiveTablePagination';

function formatDate(date: Date, t: any): string {
  const now = new Date();

  const isToday =
    date.getFullYear() === now.getFullYear() &&
    date.getMonth() === now.getMonth() &&
    date.getDate() === now.getDate();

  const time = date.toLocaleTimeString('de-DE', {
    hour: '2-digit',
    minute: '2-digit',
  });

  if (isToday) {
    return t('pages.sepa.lastExport.time', { time: time });
  }

  const dateStr = date.toLocaleDateString('de-DE', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });

  return t('pages.sepa.lastExport.dateTime', { date: dateStr, time: time });
}

export default function Sepa() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<
    Array<{
      id: UUIDTypes;
      name: string;
      amount: number;
      exportedCases: number;
      exportedDate: string;
    }>
  >([]);
  const [apiError, setApiError] = useState<string | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);

  const fetchData = async (p: number, l: number) => {
    setLoading(true);
    try {
      const offset = p * l;
      const params = new URLSearchParams({
        limit: l.toString(),
        offset: offset.toString(),
      });

      const response = await api(`/sepa/exports?${params.toString()}`);
      setData(response.items);
      setTotalCount(response.total);
    } catch (error) {
      console.error('Loading error:', error);
    } finally {
      setLoading(false);
    }
  };

  const debouncedFetch = useCallback(
    // @ts-ignore
    debounce((...args: any) => fetchData(...args), 500),
    [],
  );

  useEffect(() => {
    debouncedFetch(page, rowsPerPage);
  }, [page, rowsPerPage, debouncedFetch]);

  const download = async (id: UUIDTypes, name: string) => {
    setLoading(true);
    try {
      const res = await apiFile(`/sepa/export/${id}`, { method: 'GET' });
      if (!res.ok && res.status !== 404 && res.status !== 400) throw new Error('Server-Error');

      if (res.status === 404) setApiError(t('pages.sepa.apiError.notConfigured'));

      if (res.status === 207 || res.status === 400) setApiError((await res.json()).message);

      if (res.status === 200) {
        const blob = await res.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
      }
    } catch (error) {
      setApiError(t('pages.sepa.apiError.serverError'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {t('pages.sepa.title')}
        </Typography>
        <Typography
          variant="body2"
          color="text.secondary"
          dangerouslySetInnerHTML={{ __html: t('pages.sepa.card.description') }}
        />
      </Box>

      {apiError && (
        <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
          {apiError}
        </Alert>
      )}

      {isMobile ? (
        <Box>
          {data.length === 0 ? (
            <Typography align="center" color="text.secondary" sx={{ py: 3 }}>
              {t('pages.sepa.table.noSepa')}
            </Typography>
          ) : (
            data.map((e) => (
              <MobileListCard
                key={e.id.toString()}
                primary={<Typography sx={{ fontWeight: 600 }}>{e.name}</Typography>}
                secondaryRows={[
                  {
                    label: t('pages.sepa.table.amount'),
                    value: e.amount.toLocaleString('de-DE', {
                      style: 'currency',
                      currency: 'EUR',
                    }),
                  },
                  {
                    label: t('pages.sepa.table.exportedDate'),
                    value: formatDate(new Date(e.exportedDate), t),
                  },
                  { label: t('pages.sepa.table.exportedCases'), value: e.exportedCases },
                ]}
                actions={
                  <Button
                    variant="contained"
                    size="small"
                    startIcon={<DownloadIcon />}
                    onClick={() => download(e.id, e.name)}
                    sx={{ textTransform: 'none', borderRadius: 1.5 }}
                  >
                    {t('pages.sepa.buttons.download')}
                  </Button>
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
            labelRowsPerPage={t('pages.sepa.table.rowsPerPage')}
          />
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          elevation={0}
          sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}
        >
          <Table sx={{ minWidth: 650 }} aria-label={t('pages.members.table.ariaLabel')}>
            <TableHead sx={{ bgcolor: 'grey.50' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>{t('pages.sepa.table.name')}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t('pages.sepa.table.amount')}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t('pages.sepa.table.exportedDate')}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.sepa.table.exportedCases')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.sepa.table.action')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                    {t('pages.sepa.table.noSepa')}
                  </TableCell>
                </TableRow>
              ) : (
                data.map((e) => (
                  <TableRow key={e.id.toString()} hover>
                    <TableCell sx={{ fontWeight: 500 }}>{e.name}</TableCell>
                    <TableCell sx={{ fontWeight: 500 }}>
                      {e.amount.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' })}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 500 }}>
                      {formatDate(new Date(e.exportedDate), t)}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 500 }}>{e.exportedCases}</TableCell>
                    <TableCell align="right">
                      <Button
                        variant="contained"
                        size="small"
                        startIcon={<DownloadIcon />}
                        onClick={() => download(e.id, e.name)}
                        sx={{ textTransform: 'none', borderRadius: 1.5 }}
                      >
                        {t('pages.sepa.buttons.download')}
                      </Button>
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
            labelRowsPerPage={t('pages.sepa.table.rowsPerPage')}
          />
        </TableContainer>
      )}

      <Grid size={12} mt={3}>
        <Alert
          severity="warning"
          icon={<WarningAmberIcon />}
          sx={{ borderRadius: 3, border: '1px solid', borderColor: 'warning.light' }}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            {t('pages.sepa.warning.title')}
          </Typography>
          <Typography variant="caption">{t('pages.sepa.warning.message')}</Typography>
        </Alert>
      </Grid>
    </Box>
  );
}
