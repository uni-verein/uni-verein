import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  ButtonProps,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormHelperText,
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
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { api } from '../api';
import AddchartIcon from '@mui/icons-material/Addchart';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { ContributionPlans, Interval } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';

export default function ContributionPlanConfig() {
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [contributionPlans, setContributionPlans] = useState<ContributionPlans[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editContributionPlan, setEditContributionPlan] = useState<ContributionPlans | null>(null);
  const [errors, setErrors] = useState<{ name?: string; amount?: string; interval?: string }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const setContributionChange = useSnackbar();
  const { t } = useTranslation();

  const euroRegex = /^\d*(?:[.,]\d{0,2})?$/;

  const [formData, setFormData] = useState({
    name: '',
    amount: '0',
    interval: '',
  });

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
    if (contributionPlan) {
      setEditContributionPlan(contributionPlan);
      setFormData({
        name: contributionPlan.name,
        amount: contributionPlan.amount.toString().replace('.', ','),
        interval: contributionPlan.interval,
      });
    } else {
      setEditContributionPlan(null);
      setFormData({ name: '', amount: '0', interval: '' });
    }
    setOpenDialog(true);
  };

  const validate = () => {
    const newErrors: { name?: string; amount?: string; interval?: string } = {};

    if (!formData.name.trim()) {
      newErrors.name = t('pages.contributionPlanConfig.validation.nameEmpty');
    } else if (formData.name.length > 50) {
      newErrors.name = t('pages.contributionPlanConfig.validation.nameTooLong');
    }

    const amount = parseFloat(String(formData.amount).replace(',', '.'));
    if (!formData.amount.toString().trim()) {
      newErrors.amount = t('pages.contributionPlanConfig.validation.amountEmpty');
    } else if (isNaN(amount) || amount <= 0) {
      newErrors.amount = t('pages.contributionPlanConfig.validation.amountInvalid');
    }

    if (!formData.interval.trim()) {
      newErrors.interval = t('pages.contributionPlanConfig.validation.intervalEmpty');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    let data = {
      name: formData.name,
      amount: Number(formData.amount.replace(',', '.')),
      interval: formData.interval,
    };

    try {
      if (editContributionPlan) {
        let response = await api(`/contribution-plans/${editContributionPlan.id}`, {
          method: 'PATCH',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          setApiError(t('pages.contributionPlanConfig.apiError.updateExists'));
          setContributionChange({
            status: 'error',
            message: t('pages.contributionPlanConfig.snackbar.updateExists'),
          });
        } else {
          setContributionChange({
            status: 'success',
            message: t('pages.contributionPlanConfig.snackbar.updateSuccess'),
          });
        }
      } else {
        let response = await api('/contribution-plans', {
          method: 'POST',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          setApiError(t('pages.contributionPlanConfig.apiError.createExists'));
          setContributionChange({
            status: 'error',
            message: t('pages.contributionPlanConfig.snackbar.createExists'),
          });
        } else {
          setContributionChange({
            status: 'success',
            message: t('pages.contributionPlanConfig.snackbar.createSuccess'),
          });
        }
      }
    } catch (error) {
      setApiError(t('pages.contributionPlanConfig.apiError.saveFailed'));
      setContributionChange({
        status: 'error',
        message: t('pages.contributionPlanConfig.snackbar.saveFailed'),
      });
    }
    setOpenDialog(false);
    await loadContributionPlans();
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

      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3 }}>
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

      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} fullWidth maxWidth="xs">
        <DialogTitle>
          {editContributionPlan
            ? t('pages.contributionPlanConfig.dialog.titleEdit')
            : t('pages.contributionPlanConfig.dialog.titleCreate')}
        </DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.contributionPlanConfig.dialog.nameLabel')}
            fullWidth
            value={formData.name}
            error={errors.name !== undefined}
            helperText={errors.name ?? `${formData.name.length}/100`}
            onChange={(e) => {
              setFormData({ ...formData, name: e.target.value });
              setErrors({ ...errors, name: undefined });
            }}
          />
          <TextField
            type="text"
            label={t('pages.contributionPlanConfig.dialog.amountLabel')}
            fullWidth
            value={formData.amount.replace('.', ',')}
            error={errors.amount !== undefined}
            helperText={errors.amount}
            onChange={(e) => {
              const value = e.target.value;

              if (value === '' || euroRegex.test(value)) {
                setErrors({ ...errors, amount: undefined });
                setFormData({
                  ...formData,
                  amount: value.replace('.', ','),
                });
              } else {
                setErrors({
                  ...errors,
                  amount: t('pages.contributionPlanConfig.validation.amountFormat'),
                });
              }
            }}
          />
          <FormControl fullWidth>
            <InputLabel>{t('pages.contributionPlanConfig.dialog.intervalLabel')}</InputLabel>
            <Select
              error={errors.interval !== undefined}
              value={formData.interval || ''}
              label={t('pages.contributionPlanConfig.dialog.intervalLabel')}
              onChange={(e) => {
                setFormData({ ...formData, interval: e.target.value });
                setErrors({ ...errors, interval: undefined });
              }}
            >
              <MenuItem value={Interval.YEARLY}>
                {t('pages.contributionPlanConfig.dialog.intervalYearly')}
              </MenuItem>
              <MenuItem value={Interval.MONTHLY}>
                {t('pages.contributionPlanConfig.dialog.intervalMonthly')}
              </MenuItem>
            </Select>
            {errors.interval && <FormHelperText error={true}>{errors.interval}</FormHelperText>}
          </FormControl>
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button onClick={() => setOpenDialog(false)}>
            {t('pages.contributionPlanConfig.dialog.cancel')}
          </Button>
          <Button variant="contained" onClick={handleSave}>
            {t('pages.contributionPlanConfig.dialog.save')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
