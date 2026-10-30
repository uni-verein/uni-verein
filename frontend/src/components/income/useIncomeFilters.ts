import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { IncomeKind, MembershipStatus } from '../../types';
import { ALL_TAB, CATEGORY_TAB_PREFIX, DATE_FORMAT } from './incomeUtils';

export function useIncomeFilters() {
  const defaultDateFrom = useMemo(() => dayjs().startOf('year').format(DATE_FORMAT), []);
  const defaultDateTo = useMemo(() => dayjs().endOf('year').format(DATE_FORMAT), []);

  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPageState] = useState(10);
  const [tab, setTabState] = useState(ALL_TAB);
  const [search, setSearchState] = useState('');
  const [showUnpaid, setShowUnpaidState] = useState<boolean | undefined>(undefined);
  const [membershipStatus, setMembershipStatusState] = useState<MembershipStatus | undefined>(
    undefined,
  );
  const [memberNumber, setMemberNumberState] = useState('');
  const [showDeleted, setShowDeletedState] = useState(false);
  const [dateFrom, setDateFromState] = useState<string | null>(defaultDateFrom);
  const [dateTo, setDateToState] = useState<string | null>(defaultDateTo);

  const withPageReset =
    <T>(setter: (value: T) => void) =>
    (value: T) => {
      setter(value);
      setPage(0);
    };

  const setTab = withPageReset(setTabState);
  const setSearch = withPageReset(setSearchState);
  const setShowUnpaid = withPageReset(setShowUnpaidState);
  const setMembershipStatus = withPageReset(setMembershipStatusState);
  const setMemberNumber = withPageReset(setMemberNumberState);
  const setShowDeleted = withPageReset(setShowDeletedState);
  const setDateFrom = withPageReset(setDateFromState);
  const setDateTo = withPageReset(setDateToState);
  const setRowsPerPage = withPageReset(setRowsPerPageState);

  const isFiltered =
    search !== '' ||
    showUnpaid !== undefined ||
    membershipStatus !== undefined ||
    memberNumber !== '' ||
    showDeleted ||
    dateFrom !== defaultDateFrom ||
    dateTo !== defaultDateTo;

  const rangeParams = useMemo(() => {
    const params = new URLSearchParams();
    if (dateFrom) params.append('dateFrom', dateFrom);
    if (dateTo) params.append('dateTo', dateTo);
    return params.toString();
  }, [dateFrom, dateTo]);

  const rowsQuery = useMemo(() => {
    const params = new URLSearchParams(rangeParams);
    params.append('limit', rowsPerPage.toString());
    params.append('offset', (page * rowsPerPage).toString());
    if (search) params.append('name', search);
    if (showUnpaid !== undefined) params.append('unpaid', showUnpaid.toString());
    if (membershipStatus !== undefined && tab === IncomeKind.MEMBERSHIP) {
      params.append('membershipStatus', membershipStatus);
    }
    if (memberNumber !== '' && tab === IncomeKind.MEMBERSHIP) {
      params.append('memberNumber', memberNumber);
    }
    if (showDeleted) params.append('deleted', 'true');
    if (tab.startsWith(CATEGORY_TAB_PREFIX)) {
      params.append('categoryId', tab.substring(CATEGORY_TAB_PREFIX.length));
    } else if (tab !== ALL_TAB) {
      params.append('kind', tab);
    }
    return params.toString();
  }, [
    rangeParams,
    rowsPerPage,
    page,
    search,
    showUnpaid,
    membershipStatus,
    memberNumber,
    showDeleted,
    tab,
  ]);

  const selectedYear = useMemo(() => {
    if (dateFrom === null && dateTo === null) return 'all';
    const year = dateFrom ? dayjs(dateFrom).year() : null;
    return year !== null && dateFrom === `${year}-01-01` && dateTo === `${year}-12-31`
      ? String(year)
      : 'custom';
  }, [dateFrom, dateTo]);

  const selectYear = (value: string) => {
    if (value === 'all') {
      setDateFromState(null);
      setDateToState(null);
    } else {
      setDateFromState(`${value}-01-01`);
      setDateToState(`${value}-12-31`);
    }
    setPage(0);
  };

  const resetFilters = () => {
    setSearchState('');
    setShowUnpaidState(undefined);
    setMembershipStatusState(undefined);
    setMemberNumberState('');
    setShowDeletedState(false);
    setDateFromState(defaultDateFrom);
    setDateToState(defaultDateTo);
    setPage(0);
  };

  const clearDateRange = () => {
    setDateFromState(null);
    setDateToState(null);
    setPage(0);
  };

  return {
    page,
    setPage,
    rowsPerPage,
    setRowsPerPage,
    tab,
    setTab,
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
    rangeParams,
    rowsQuery,
    selectedYear,
    selectYear,
    resetFilters,
    clearDateRange,
  };
}
