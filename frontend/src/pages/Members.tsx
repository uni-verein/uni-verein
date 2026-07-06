import React, { useCallback, useEffect, useState } from 'react';
import {
  Box,
  Button,
  ButtonProps,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import debounce from 'lodash.debounce';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import VisibilityIcon from '@mui/icons-material/Visibility';
import RestoreFromTrashIcon from '@mui/icons-material/RestoreFromTrash';
import RefreshIcon from '@mui/icons-material/Refresh';

import { api } from '../api';
import {
  BulkMail,
  ContributionPlans,
  Gender,
  Member,
  MemberCategory,
  Role,
  TaskWithinTheClub,
  UserRoleProps,
} from '../types';
import MemberForm from '../components/MemberForm';
import { TASK_WITHIN_THE_CLUB_LABELS } from '../utils';
import { NIL as NIL_UUID, UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';

export default function Members({ role }: UserRoleProps) {
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [members, setMembers] = useState<Member[]>([]);
  const [contributionPlans, setContributionPlans] = useState<ContributionPlans[]>([]);
  const [memberCategories, setMemberCategories] = useState<MemberCategory[]>([]);
  const [edit, setEdit] = useState<Member | null>(null);
  const [view, setView] = useState<boolean>(false);
  const setDeleteOrUpdateMember = useSnackbar();
  const { t } = useTranslation();

  const [search, setSearch] = useState('');
  const [task, setTask] = useState<TaskWithinTheClub | null>(null);
  const [status, setStatus] = useState<UUIDTypes | null>(null);
  const [showDeleted, setShowDeleted] = useState<boolean>(false);

  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);

  const load = async () => {
    const contributionPlan = await api('/contribution-plans');
    setContributionPlans(contributionPlan.items);
    const memberCategories = await api('/member-categories');
    setMemberCategories(memberCategories.items);
  };

  useEffect(() => {
    load();
  }, []);

  const fetchData = async (
    s: string | undefined,
    a: TaskWithinTheClub | null,
    st: UUIDTypes | null,
    d: boolean,
    p: number,
    l: number,
  ) => {
    setLoading(true);
    try {
      const offset = p * l;
      const params = new URLSearchParams({
        name: s !== undefined ? s : '',
        taskWithinTheClub: a !== null ? a : '',
        memberCategoryId: st !== null ? st.toString() : '',
        deleted: d.toString(),
        limit: l.toString(),
        offset: offset.toString(),
      });

      const response = await api(`/members?${params.toString()}`);
      setMembers(response.items);
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
    debouncedFetch(search, task, status, showDeleted, page, rowsPerPage);
  }, [search, task, status, showDeleted, page, rowsPerPage, debouncedFetch]);

  const getTaskLabel = (task: string): string => {
    return TASK_WITHIN_THE_CLUB_LABELS[task as TaskWithinTheClub] || 'unknownTask';
  };

  async function remove(id: UUIDTypes) {
    setConfirmDialog({
      message: t('pages.members.confirmDialog.deleteQuestion'),
      buttonText: t('pages.members.confirmDialog.delete'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/members/${id}`, { method: 'DELETE' });
        setDeleteOrUpdateMember({
          status: 'success',
          message: t('pages.members.responseMessages.deleted'),
        });
      } catch (e) {
        setDeleteOrUpdateMember({
          status: 'error',
          message: t('pages.members.responseMessages.deleteError'),
        });
      }
      await load();
      await fetchData(search, task, status, showDeleted, page, rowsPerPage);
    }
  }

  async function restore(id: UUIDTypes) {
    setConfirmDialog({
      message: t('pages.members.confirmDialog.restoreQuestion'),
      buttonText: t('pages.members.confirmDialog.restore'),
      confirmColor: 'success',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/members/${id}`, { method: 'POST' });
        setDeleteOrUpdateMember({
          status: 'success',
          message: t('pages.members.responseMessages.restored'),
        });
      } catch (e) {
        setDeleteOrUpdateMember({
          status: 'error',
          message: t('pages.members.responseMessages.restoreError'),
        });
      }
      await load();
      await fetchData(search, task, status, showDeleted, page, rowsPerPage);
    }
  }

  const handleCreateNew = () => {
    setEdit({
      bulkMail: BulkMail.ALLOWED,
      academicDegree: null,
      birthday: null,
      city: '',
      countryCode: null,
      contributionPlanId: null,
      courseOfStudy: '',
      email: '',
      endOfStudies: null,
      entryDate: new Date(),
      exitDate: null,
      firstName: '',
      gender: Gender.MALE,
      iban: '',
      bic: '',
      id: NIL_UUID,
      lastName: '',
      memberCategoryId: null,
      memberNumber: 0,
      middleName: '',
      phone: '',
      postalCode: '',
      sepaConsent: null,
      startOfStudies: null,
      street: '',
      taskWithinTheClub: TaskWithinTheClub.MEMBER,
      deletedAt: null,
    });
  };

  const getCategoryTranslation = (category: MemberCategory | undefined) => {
    if (category === undefined) return '';

    const translationKey = `pages.members.filter.${category.category}`;
    return t(translationKey).startsWith(translationKey) ? category.name : t(translationKey);
  };

  const isFiltered = search !== '' || task !== null || status !== null || showDeleted;
  const allId = memberCategories.find((x) => x.category === 'ALL')?.id.toString() ?? '';
  const selectValue = !status || status === '' ? allId : status;

  return (
    <Box>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        onClose={handleClose}
        confirmColor={confirmDialog.confirmColor}
      />

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, color: 'text.primary' }}>
          {t('pages.members.title')}
        </Typography>
        <Box>
          <Tooltip title={t('pages.members.refresh')}>
            <IconButton
              onClick={() => fetchData(search, task, status, showDeleted, page, rowsPerPage)}
            >
              <RefreshIcon />
            </IconButton>
          </Tooltip>
          <Button
            variant="contained"
            startIcon={<PersonAddIcon />}
            onClick={handleCreateNew}
            sx={{ borderRadius: 2, textTransform: 'none' }}
          >
            {t('pages.members.addMember')}
          </Button>
        </Box>
      </Box>

      {edit && (
        <MemberForm
          view={view}
          member={edit}
          contributionPlans={contributionPlans}
          memberCategories={memberCategories}
          onClose={() => {
            setEdit(null);
            setView(false);
            load();
            fetchData(search, task, status, showDeleted, page, rowsPerPage);
          }}
        />
      )}

      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={3}>
          <TextField
            label={t('pages.members.filter.searchName')}
            fullWidth
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(0);
            }}
          />
        </Grid>

        <Grid size={3}>
          <FormControl fullWidth>
            <InputLabel>{t('pages.members.filter.taskInClub')}</InputLabel>
            <Select
              value={task === null ? 'all' : task}
              label={t('pages.members.filter.taskInClub')}
              onChange={(e) => {
                setTask(
                  e.target.value in TaskWithinTheClub
                    ? TaskWithinTheClub[e.target.value as keyof typeof TaskWithinTheClub]
                    : null,
                );
                setPage(0);
              }}
            >
              <MenuItem value="all">{t('pages.members.filter.allTasks')}</MenuItem>
              {Object.entries(TASK_WITHIN_THE_CLUB_LABELS).map(([value, label]) => (
                <MenuItem key={value} value={value}>
                  {t(`components.taskWithinTheClubOptions.${label}`)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Grid>

        <Grid size={3}>
          <FormControl fullWidth>
            <InputLabel>{t('pages.members.filter.memberCategory')}</InputLabel>
            <Select
              value={selectValue}
              label={t('pages.members.filter.memberCategory')}
              onChange={(e) => {
                setStatus(e.target.value.toString());
                setPage(0);
              }}
            >
              {memberCategories.map((e) => {
                return (
                  <MenuItem key={e.id.toString()} value={e.id.toString()}>
                    {getCategoryTranslation(e)}
                  </MenuItem>
                );
              })}
            </Select>
          </FormControl>
        </Grid>
        {role === Role.ADMIN && (
          <Grid size={3}>
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
              label={t('pages.members.filter.showDeleted')}
            />
          </Grid>
        )}
        {isFiltered && (
          <Grid size={role === Role.ADMIN ? 12 : 3}>
            <Button
              variant="outlined"
              color="secondary"
              fullWidth
              onClick={() => {
                setSearch('');
                setTask(null);
                setStatus(null);
                setPage(0);
              }}
            >
              {t('pages.members.filter.resetFilter')}
            </Button>
          </Grid>
        )}
      </Grid>

      <TableContainer
        component={Paper}
        elevation={0}
        sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}
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

        <Table sx={{ minWidth: 650 }} aria-label={t('pages.members.table.ariaLabel')}>
          <TableHead sx={{ bgcolor: 'grey.50' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 'bold' }}>
                {t('pages.members.table.colNumber')}
              </TableCell>
              <TableCell sx={{ fontWeight: 'bold' }}>{t('pages.members.table.colName')}</TableCell>
              <TableCell sx={{ fontWeight: 'bold' }}>{t('pages.members.table.colTask')}</TableCell>
              <TableCell sx={{ fontWeight: 'bold' }}>{t('pages.members.table.colEmail')}</TableCell>
              <TableCell sx={{ fontWeight: 'bold' }}>
                {t('pages.members.table.colContribution')}
              </TableCell>
              <TableCell sx={{ fontWeight: 'bold' }}>
                {t('pages.members.table.colMemberCategory')}
              </TableCell>
              <TableCell align="right" sx={{ fontWeight: 'bold', minWidth: '130px' }}>
                {t('pages.members.table.colActions')}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {members.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                  {t('pages.members.table.noMembers')}
                </TableCell>
              </TableRow>
            ) : (
              members.map((m) => (
                <TableRow
                  key={m.id.toString()}
                  hover
                  sx={{ '&:last-child td, &:last-child th': { border: 0 } }}
                >
                  <TableCell>
                    <Chip
                      label={m.memberNumber}
                      size="small"
                      variant="outlined"
                      sx={{ fontWeight: 500 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>
                    {m.firstName} {m.middleName} {m.lastName}
                  </TableCell>
                  <TableCell color="text.secondary">
                    {t(`components.taskWithinTheClubOptions.${getTaskLabel(m.taskWithinTheClub)}`)}
                  </TableCell>
                  <TableCell color="text.secondary">{m.email}</TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', color: 'text.secondary' }}>
                    {m.contributionPlanId !== null
                      ? `${contributionPlans.find((x) => x.id === m.contributionPlanId)?.amount} €`
                      : t('pages.members.table.noContribution')}
                  </TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', color: 'text.secondary' }}>
                    {m.memberCategoryId !== null && m.memberCategoryId !== undefined
                      ? getCategoryTranslation(
                          memberCategories.find((x) => x.id === m.memberCategoryId),
                        )
                      : ''}
                  </TableCell>
                  <TableCell align="right">
                    {m.deletedAt !== null && (
                      <Tooltip title={t('pages.members.actions.restore')}>
                        <IconButton onClick={() => restore(m.id)} size="small" color="primary">
                          <RestoreFromTrashIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    {m.deletedAt === null && (
                      <Tooltip title={t('pages.members.actions.view')}>
                        <IconButton
                          onClick={() => {
                            setView(true);
                            setEdit(m);
                          }}
                          size="small"
                          color="primary"
                        >
                          <VisibilityIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    {m.deletedAt === null && (
                      <Tooltip title={t('pages.members.actions.edit')}>
                        <IconButton onClick={() => setEdit(m)} size="small" color="primary">
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    <Tooltip title={t('pages.members.actions.delete')}>
                      <IconButton onClick={() => remove(m.id)} size="small" color="error">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
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
          labelRowsPerPage={t('pages.members.table.rowsPerPage')}
        />
      </TableContainer>
    </Box>
  );
}
