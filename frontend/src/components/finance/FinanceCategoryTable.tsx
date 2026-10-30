import { useState } from 'react';
import {
  Box,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { formatEuro } from '../income/incomeUtils';
import { categoryLabel, FinanceCategorySummary, formatPercent } from './financeUtils';

interface FinanceCategoryTableProps {
  income: FinanceCategorySummary[];
  expenses: FinanceCategorySummary[];
  months: number;
}

const hideOnMobile = { display: { xs: 'none', sm: 'table-cell' } };

export function FinanceCategoryTable({ income, expenses, months }: FinanceCategoryTableProps) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');

  const query = search.trim().toLowerCase();
  const rows = [...income, ...expenses]
    .map((c) => ({ category: c, label: categoryLabel(c, t) }))
    .filter((r) => !query || r.label.toLowerCase().includes(query));

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <TextField
        size="small"
        type="search"
        label={t('pages.financeAnalytics.table.search')}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>{t('pages.financeAnalytics.table.category')}</TableCell>
              <TableCell>{t('pages.financeAnalytics.table.type')}</TableCell>
              <TableCell align="right">{t('pages.financeAnalytics.table.total')}</TableCell>
              <TableCell align="right" sx={hideOnMobile}>
                {t('pages.financeAnalytics.table.share')}
              </TableCell>
              <TableCell align="right" sx={hideOnMobile}>
                {t('pages.financeAnalytics.table.monthlyAverage')}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map(({ category, label }) => (
              <TableRow key={`${category.side}:${category.key}`}>
                <TableCell>{label}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={t(`pages.financeAnalytics.table.${category.side}`)}
                    color={category.side === 'income' ? 'primary' : 'error'}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell align="right">{formatEuro(category.total)}</TableCell>
                <TableCell align="right" sx={hideOnMobile}>
                  {formatPercent(category.share)}
                </TableCell>
                <TableCell align="right" sx={hideOnMobile}>
                  {formatEuro(category.total / months)}
                </TableCell>
              </TableRow>
            ))}
            {rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} align="center">
                  <Typography variant="body2" color="text.secondary">
                    {t('pages.financeAnalytics.noData')}
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
