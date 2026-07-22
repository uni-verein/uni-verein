import { useEffect, useState } from 'react';
import { api } from '../api';
import {
  Box,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  CircularProgress,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { AuditLogCard } from '../components/AuditLogCard';
import { AuditLogTableRow } from '../components/AuditLogTableRow';
import { useTranslation } from 'react-i18next';
import ResponsiveTablePagination from '../components/ResponsiveTablePagination';

export default function Audit() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const [logs, setLogs] = useState<any[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);
  const { t } = useTranslation();

  const fetchData = async (p: number, l: number) => {
    setLoading(true);
    try {
      const offset = p * l;
      const params = new URLSearchParams({
        limit: l.toString(),
        offset: offset.toString(),
      });

      const response = await api(`/audit?${params.toString()}`);
      setLogs(response.items);
      setTotalCount(response.total);
    } catch (error) {
      console.error('error while loading', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData(page, rowsPerPage);
  }, [page, rowsPerPage]);

  return (
    <Box sx={{ p: { xs: 0, sm: 3 } }}>
      <Typography variant="h5" fontWeight="bold" gutterBottom>
        {t('pages.audit.title')}
      </Typography>

      {loading ? (
        <Box display="flex" justifyContent="center" mt={6}>
          <CircularProgress />
        </Box>
      ) : isMobile ? (
        <Box>
          {logs.length === 0 ? (
            <Typography color="text.secondary" align="center" sx={{ py: 4 }}>
              {t('pages.audit.noEntries')}
            </Typography>
          ) : (
            logs.map((l) => <AuditLogCard key={l.id} l={l} />)
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
            labelRowsPerPage={t('pages.audit.rowsPerPage')}
          />
        </Box>
      ) : (
        <TableContainer component={Paper} elevation={2}>
          <Table size="small">
            <TableHead>
              <TableRow sx={{ backgroundColor: 'primary.main' }}>
                <TableCell sx={{ width: 40 }} />
                {[
                  t('pages.audit.tableHeaders.time'),
                  t('pages.audit.tableHeaders.user'),
                  t('pages.audit.tableHeaders.action'),
                  t('pages.audit.tableHeaders.entity'),
                  t('pages.audit.tableHeaders.data'),
                ].map((header) => (
                  <TableCell
                    key={header}
                    sx={{ color: 'primary.contrastText', fontWeight: 'bold' }}
                  >
                    {header}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>

            <TableBody>
              {logs.map((l, index) => (
                <AuditLogTableRow key={l.id} l={l} index={index} />
              ))}

              {logs.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                    <Typography color="text.secondary">{t('pages.audit.noEntries')}</Typography>
                  </TableCell>
                </TableRow>
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
            labelRowsPerPage={t('pages.audit.rowsPerPage')}
          />
        </TableContainer>
      )}
    </Box>
  );
}
