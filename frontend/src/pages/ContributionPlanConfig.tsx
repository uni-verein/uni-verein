import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  ButtonProps,
  IconButton,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { api } from '../api';
import AddchartIcon from '@mui/icons-material/Addchart';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { ContributionPlans, Interval } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
import { MobileListCard } from '../components/MobileListCard';
import { ContributionPlanDialog } from '../components/dialogs/ContributionPlanDialog';

export default function ContributionPlanConfig() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [contributionPlans, setContributionPlans] = useState<ContributionPlans[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editContributionPlan, setEditContributionPlan] = useState<ContributionPlans | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const setContributionChange = useSnackbar();
  const { t } = useTranslation();

  const loadContributionPlans = async () => {
    try {
      const data = await api('/contribution-plans');
      if (data) {
        setContributionPlans(data.items);
      }
    } catch (error) {}
  };

  useEffect(() => {
    loadContributionPlans();
  }, []);
  const handleOpen = (contributionPlan: ContributionPlans | null = null) => {
    setEditContributionPlan(contributionPlan);
    setOpenDialog(true);
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.contributionPlanConfig.confirm.deleteMessage'),
      buttonText: t('pages.contributionPlanConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/contribution-plans/${id}`, { method: 'DELETE' });
        setContributionChange({
          status: 'success',
          message: t('pages.contributionPlanConfig.snackbar.deleteSuccess'),
        });
        await loadContributionPlans();
        // @ts-ignore
      } catch (error: Error) {
        setApiError(
          t(
            'pages.contributionPlanConfig.apiError.' +
              (error.message === 'Bad Request' ? 'deleteFailedMember' : 'deleteFailed'),
          ),
        );
        setContributionChange({
          status: 'error',
          message: t(
            'pages.contributionPlanConfig.snackbar.' +
              (error.message === 'Bad Request' ? 'deleteFailedMember' : 'deleteFailed'),
          ),
        });
      }
    }
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

      <Box
        sx={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 2, mb: 3 }}
      >
        <Typography variant="h5" fontWeight={700}>
          {t('pages.contributionPlanConfig.title')}
        </Typography>
        <Button variant="contained" startIcon={<AddchartIcon />} onClick={() => handleOpen()}>
          {t('pages.contributionPlanConfig.newButton')}
        </Button>
      </Box>

      {apiError && (
        <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
          {apiError}
        </Alert>
      )}

      {isMobile ? (
        <Box>
          {contributionPlans.map((u) => (
            <MobileListCard
              key={u.id.toString()}
              primary={<Typography sx={{ fontWeight: 600 }}>{u.name}</Typography>}
              secondaryRows={[
                {
                  label: t('pages.contributionPlanConfig.table.amount'),
                  value: `${u.amount.toString().replace('.', ',')} €`,
                },
                {
                  label: t('pages.contributionPlanConfig.table.interval'),
                  value: t(
                    `pages.contributionPlanConfig.dialog.${u.interval === Interval.MONTHLY ? 'intervalMonthly' : 'intervalYearly'}`,
                  ),
                },
              ]}
              actions={
                <>
                  <Tooltip title={t('pages.contributionPlanConfig.tooltip.edit')}>
                    <IconButton onClick={() => handleOpen(u)} color="primary" size="small">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={t('pages.contributionPlanConfig.tooltip.delete')}>
                    <IconButton onClick={() => handleDelete(u.id)} color="error" size="small">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </>
              }
            />
          ))}
        </Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table>
            <TableHead sx={{ bgcolor: 'grey.50' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributionPlanConfig.table.name')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributionPlanConfig.table.amount')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.contributionPlanConfig.table.interval')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.contributionPlanConfig.table.actions')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {contributionPlans.map((u) => (
                <TableRow key={u.id.toString()} hover>
                  <TableCell>{u.name}</TableCell>
                  <TableCell>{u.amount.toString().replace('.', ',')}</TableCell>
                  <TableCell>
                    {t(
                      `pages.contributionPlanConfig.dialog.${u.interval === Interval.MONTHLY ? 'intervalMonthly' : 'intervalYearly'}`,
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title={t('pages.contributionPlanConfig.tooltip.edit')}>
                      <IconButton onClick={() => handleOpen(u)} color="primary">
                        <EditIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t('pages.contributionPlanConfig.tooltip.delete')}>
                      <IconButton onClick={() => handleDelete(u.id)} color="error">
                        <DeleteIcon />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {openDialog && (
        <ContributionPlanDialog
          contributionPlan={editContributionPlan}
          onClose={() => setOpenDialog(false)}
          onSaved={() => {
            setOpenDialog(false);
            loadContributionPlans();
          }}
          onError={setApiError}
        />
      )}
    </Box>
  );
}
