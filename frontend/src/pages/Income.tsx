import { useState } from 'react';
import {
  Alert,
  Box,
  Typography,
  Button,
  Tab,
  Tabs,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  IconButton,
  Tooltip,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import RefreshIcon from '@mui/icons-material/Refresh';
import { useTranslation } from 'react-i18next';
import { IncomeKind, IncomeRow, Role, UserRoleProps } from '../types';
import ResponsiveTablePagination from '../components/ResponsiveTablePagination';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import IncomeForm from '../components/dialogs/IncomeForm';
import { PaymentDateDialog } from '../components/dialogs/PaymentDateDialog';
import { useSnackbar } from '../hooks/useSnackbar';
import { ALL_TAB, CATEGORY_TAB_PREFIX } from '../components/income/incomeUtils';
import { useIncomeFilters } from '../components/income/useIncomeFilters';
import { useIncomeData } from '../components/income/useIncomeData';
import { useIncomeActions } from '../components/income/useIncomeActions';
import { useIncomeLabels } from '../components/income/useIncomeLabels';
import { IncomeKpiCards } from '../components/income/IncomeKpiCards';
import { IncomeDistributionCard } from '../components/income/IncomeDistributionCard';
import { IncomeFilters } from '../components/income/IncomeFilters';
import { IncomeTable } from '../components/income/IncomeTable';
import { IncomeMobileList } from '../components/income/IncomeMobileList';

export default function Income({ role }: UserRoleProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const isAdmin = role === Role.ADMIN;
  const canManage = role === Role.ADMIN || role === Role.FINANCIAL_MANAGER;

  const filters = useIncomeFilters();
  const { rows, summary, categories, years, totalCount, loading, reload } = useIncomeData(
    filters.rowsQuery,
    filters.rangeParams,
  );
  const {
    markAsPaid,
    remove,
    restore,
    issueAcknowledgement,
    confirmDialogProps,
    paymentDateDialogProps,
  } = useIncomeActions(reload);
  const labels = useIncomeLabels(categories);
  const setIncomeChange = useSnackbar();

  const [formIncome, setFormIncome] = useState<IncomeRow | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const openForm = (income: IncomeRow | null) => {
    setFormIncome(income);
    setFormOpen(true);
  };

  const pagination = (
    <ResponsiveTablePagination
      component="div"
      count={totalCount}
      rowsPerPage={filters.rowsPerPage}
      page={filters.page}
      onPageChange={(_, newPage) => filters.setPage(newPage)}
      onRowsPerPageChange={(e) => filters.setRowsPerPage(parseInt(e.target.value, 10))}
      labelRowsPerPage={t('pages.income.table.rowsPerPage')}
    />
  );

  const rowsViewProps = {
    rows,
    loading,
    isAdmin,
    canManage,
    labels,
    pagination,
    onTogglePaid: markAsPaid,
    onAcknowledgement: issueAcknowledgement,
    onEdit: openForm,
    onDelete: remove,
    onRestore: restore,
  };

  return (
    <Box>
      <ConfirmDialog {...confirmDialogProps} />
      <PaymentDateDialog {...paymentDateDialogProps} />
      {formOpen && (
        <IncomeForm
          income={formIncome}
          categories={categories}
          onClose={() => setFormOpen(false)}
          onSaved={() => {
            setFormOpen(false);
            setIncomeChange({
              status: 'success',
              message: t(
                formIncome
                  ? 'pages.income.responseMessages.updated'
                  : 'pages.income.responseMessages.created',
              ),
            });
            reload();
          }}
        />
      )}

      <Box
        sx={{
          mb: 4,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: 2,
        }}
      >
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
            {t('pages.income.title')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t('pages.income.description')}
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="income-year-label">{t('pages.income.year.label')}</InputLabel>
            <Select
              labelId="income-year-label"
              value={filters.selectedYear}
              label={t('pages.income.year.label')}
              onChange={(e) => filters.selectYear(e.target.value)}
            >
              {years.map((year) => (
                <MenuItem key={year} value={String(year)}>
                  {year}
                </MenuItem>
              ))}
              <MenuItem value="all">{t('pages.income.year.all')}</MenuItem>
              {filters.selectedYear === 'custom' && (
                <MenuItem value="custom" disabled>
                  {t('pages.income.year.custom')}
                </MenuItem>
              )}
            </Select>
          </FormControl>
          <Tooltip title={t('pages.income.button.refresh')}>
            <IconButton onClick={reload} aria-label={t('pages.income.button.refresh')}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
          {canManage && (
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => openForm(null)}>
              {t('pages.income.button.create')}
            </Button>
          )}
        </Box>
      </Box>

      <IncomeKpiCards summary={summary} />

      {summary.paidAmount > 0 && (
        <IncomeDistributionCard
          summary={summary}
          chipColors={labels.chipColors}
          kindLabel={labels.kindLabel}
        />
      )}

      <Tabs
        value={filters.tab}
        onChange={(_, value: string) => filters.setTab(value)}
        variant="scrollable"
        scrollButtons="auto"
        allowScrollButtonsMobile
        aria-label={t('pages.income.tabs.label')}
        sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}
      >
        <Tab value={ALL_TAB} label={t('pages.income.tabs.all')} />
        <Tab value={IncomeKind.MEMBERSHIP} label={t('pages.income.tabs.membership')} />
        {categories.map((c) => (
          <Tab key={c.id.toString()} value={`${CATEGORY_TAB_PREFIX}${c.id}`} label={c.name} />
        ))}
      </Tabs>

      <IncomeFilters filters={filters} isAdmin={isAdmin} />

      {filters.tab === IncomeKind.MEMBERSHIP && (
        <Alert severity="info" sx={{ mb: 3 }}>
          {t('pages.income.hint.contributions')}
        </Alert>
      )}

      {isMobile ? <IncomeMobileList {...rowsViewProps} /> : <IncomeTable {...rowsViewProps} />}
    </Box>
  );
}
