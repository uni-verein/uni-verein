import { ReactNode } from 'react';
import {
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  TextField,
} from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import 'dayjs/locale/de';
import FilterAltOffIcon from '@mui/icons-material/FilterAltOff';
import { useTranslation } from 'react-i18next';
import { AutoFitGrid } from '../AutoFitGrid';
import { IncomeKind, MembershipStatus } from '../../types';
import { DATE_FORMAT } from './incomeUtils';
import { useIncomeFilters } from './useIncomeFilters';

function FilterCard({ children }: { children: ReactNode }) {
  return <Box>{children}</Box>;
}

interface Props {
  filters: ReturnType<typeof useIncomeFilters>;
  isAdmin: boolean;
}

export function IncomeFilters({ filters, isAdmin }: Props) {
  const { t } = useTranslation();
  const {
    tab,
    search,
    setSearch,
    showUnpaid,
    setShowUnpaid,
    membershipStatus,
    setMembershipStatus,
    memberNumber,
    setMemberNumber,
    showDeleted,
    setShowDeleted,
    dateFrom,
    setDateFrom,
    dateTo,
    setDateTo,
    isFiltered,
    resetFilters,
    clearDateRange,
  } = filters;
  const isContributionTab = tab === IncomeKind.MEMBERSHIP;

  return (
    <AutoFitGrid sx={{ mb: 3 }}>
      <FilterCard>
        <TextField
          label={t(
            isContributionTab ? 'pages.income.filter.searchMember' : 'pages.income.filter.search',
          )}
          fullWidth
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </FilterCard>

      <FilterCard>
        <FormControl fullWidth>
          <InputLabel id="income-status-filter-label">{t('pages.income.filter.status')}</InputLabel>
          <Select
            labelId="income-status-filter-label"
            value={showUnpaid === undefined ? 'all' : String(showUnpaid)}
            label={t('pages.income.filter.status')}
            onChange={(e) =>
              setShowUnpaid(e.target.value === 'all' ? undefined : e.target.value === 'true')
            }
          >
            <MenuItem value="all">{t('pages.income.filter.all')}</MenuItem>
            <MenuItem value="false">{t('pages.income.filter.paid')}</MenuItem>
            <MenuItem value="true">{t('pages.income.filter.unpaid')}</MenuItem>
          </Select>
        </FormControl>
      </FilterCard>

      {isContributionTab && (
        <FilterCard>
          <FormControl fullWidth>
            <InputLabel id="income-membership-status-filter-label">
              {t('pages.income.filter.membershipStatus')}
            </InputLabel>
            <Select
              labelId="income-membership-status-filter-label"
              value={membershipStatus ?? 'all'}
              label={t('pages.income.filter.membershipStatus')}
              onChange={(e) => {
                const value = e.target.value;
                setMembershipStatus(value === 'all' ? undefined : (value as MembershipStatus));
              }}
            >
              <MenuItem value="all">{t('pages.income.filter.allMembershipStatus')}</MenuItem>
              {Object.values(MembershipStatus).map((value) => (
                <MenuItem key={value} value={value}>
                  {t(`components.membershipStatusOptions.${value}`)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </FilterCard>
      )}

      {isContributionTab && (
        <FilterCard>
          <TextField
            label={t('pages.income.filter.memberNumber')}
            fullWidth
            value={memberNumber}
            onChange={(e) => setMemberNumber(e.target.value.replace(/\D/g, '').slice(0, 9))}
            slotProps={{ htmlInput: { inputMode: 'numeric' } }}
          />
        </FilterCard>
      )}

      <FilterCard>
        <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
          <DatePicker
            label={t('pages.income.filter.dateFrom')}
            format="DD.MM.YYYY"
            value={dateFrom ? dayjs(dateFrom) : null}
            onChange={(v) => setDateFrom(v && v.isValid() ? v.format(DATE_FORMAT) : null)}
            slotProps={{ textField: { fullWidth: true } }}
          />
        </LocalizationProvider>
      </FilterCard>

      <FilterCard>
        <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
          <DatePicker
            label={t('pages.income.filter.dateTo')}
            format="DD.MM.YYYY"
            value={dateTo ? dayjs(dateTo) : null}
            onChange={(v) => setDateTo(v && v.isValid() ? v.format(DATE_FORMAT) : null)}
            slotProps={{ textField: { fullWidth: true } }}
          />
        </LocalizationProvider>
      </FilterCard>

      {(dateFrom !== null || dateTo !== null) && (
        <FilterCard>
          <Button variant="outlined" fullWidth onClick={clearDateRange}>
            {t('pages.income.filter.allTime')}
          </Button>
        </FilterCard>
      )}

      {isAdmin && (
        <FilterCard>
          <FormControlLabel
            control={
              <Checkbox checked={showDeleted} onChange={(e) => setShowDeleted(e.target.checked)} />
            }
            label={t('pages.income.filter.showDeleted')}
          />
        </FilterCard>
      )}

      {isFiltered && (
        <FilterCard>
          <Button
            variant="outlined"
            color="secondary"
            fullWidth
            startIcon={<FilterAltOffIcon />}
            onClick={resetFilters}
          >
            {t('pages.income.filter.resetFilter')}
          </Button>
        </FilterCard>
      )}
    </AutoFitGrid>
  );
}
