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
  Chip,
  CircularProgress,
  Collapse,
  IconButton,
  TablePagination,
} from '@mui/material';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp';
import { RedactedText } from '../components/RedactedText';
import { useTranslation } from 'react-i18next';

const actionColors: Record<string, 'success' | 'error' | 'warning' | 'info'> = {
  CREATE: 'success',
  DELETE: 'error',
  UPDATE: 'warning',
  READ: 'info',
};

function AuditRow({ l, index }: { l: any; index: number }) {
  const [open, setOpen] = useState(false);
  const { t } = useTranslation();

  const rawData = typeof l.data === 'object' ? JSON.stringify(l.data, null, 2) : l.data;

  return (
    <>
      <TableRow
        onClick={() => setOpen((prev) => !prev)}
        sx={{
          backgroundColor: index % 2 === 0 ? 'inherit' : 'action.hover',
          '&:hover': { backgroundColor: 'action.selected' },
          cursor: 'pointer',
        }}
      >
        <TableCell padding="checkbox">
          <IconButton
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              setOpen((prev) => !prev);
            }}
          >
            {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
        <TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary' }}>
          {new Date(l.timestamp).toLocaleString()}
        </TableCell>
        <TableCell>{l.userName}</TableCell>
        <TableCell>
          <Chip
            label={l.action}
            color={actionColors[l.action] ?? 'default'}
            size="small"
            variant="outlined"
          />
        </TableCell>
        <TableCell>{l.entity}</TableCell>
        <TableCell
          sx={{
            maxWidth: 300,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            color: 'text.secondary',
            fontFamily: 'monospace',
            fontSize: '0.8rem',
          }}
        >
          <RedactedText text={rawData} />
        </TableCell>
      </TableRow>

      <TableRow>
        <TableCell colSpan={6} sx={{ py: 0, border: open ? undefined : 'none' }}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box
              sx={{
                m: 1.5,
                p: 2,
                backgroundColor: 'action.hover',
                borderRadius: 1,
                borderLeft: '3px solid',
                borderColor: 'primary.main',
              }}
            >
              <Typography variant="caption" color="text.secondary" fontWeight="bold">
                {t('pages.audit.dataLabel')}
              </Typography>
              <Typography
                component="pre"
                variant="body2"
                sx={{
                  mt: 0.5,
                  fontFamily: 'monospace',
                  fontSize: '0.8rem',
                  whiteSpace: 'pre-wrap',
                  wordBreak: 'break-all',
                  margin: 0,
                }}
              >
                <RedactedText text={rawData} />
              </Typography>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}

export default function Audit() {
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
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" fontWeight="bold" gutterBottom>
        {t('pages.audit.title')}
      </Typography>

      {loading ? (
        <Box display="flex" justifyContent="center" mt={6}>
          <CircularProgress />
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
                <AuditRow key={l.id} l={l} index={index} />
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
            labelRowsPerPage={t('pages.audit.rowsPerPage')}
          />
        </TableContainer>
      )}
    </Box>
  );
}
