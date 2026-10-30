import React, { useCallback, useEffect, useState } from 'react';
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
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import { IncomeCategory } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';
import { useTranslation } from 'react-i18next';
import { MobileListCard } from '../components/MobileListCard';
import { IncomeCategoryDialog } from '../components/dialogs/IncomeCategoryDialog';

export default function IncomeCategoryConfig() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [categories, setCategories] = useState<IncomeCategory[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editing, setEditing] = useState<IncomeCategory | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const setIncomeCategoryChange = useSnackbar();
  const { t } = useTranslation();

  const loadCategories = useCallback(async () => {
    try {
      const data = await api('/income-categories');
      if (data) {
        setCategories(data.items);
      }
    } catch {
      // best-effort: table just stays empty if this fails
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadCategories();
  }, [loadCategories]);

  const closeDialog = () => {
    setOpenDialog(false);
    setEditing(null);
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.incomeCategoryConfig.confirm.deleteMessage'),
      buttonText: t('pages.incomeCategoryConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });

    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/income-categories/${id}`, { method: 'DELETE' });

        setIncomeCategoryChange({
          status: 'success',
          message: t('pages.incomeCategoryConfig.snackbar.deleteSuccess'),
        });

        await loadCategories();
      } catch (error) {
        const message = error instanceof Error ? error.message : '';
        setApiError(
          t(
            'pages.incomeCategoryConfig.apiError.' +
              (message === 'Bad Request' ? 'deleteFailedInUse' : 'deleteFailed'),
          ),
        );

        setIncomeCategoryChange({
          status: 'error',
          message: t(
            'pages.incomeCategoryConfig.snackbar.' +
              (message === 'Bad Request' ? 'deleteFailedInUse' : 'deleteFailed'),
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
          {t('pages.incomeCategoryConfig.title')}
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpenDialog(true)}>
          {t('pages.incomeCategoryConfig.newButton')}
        </Button>
      </Box>

      {apiError && (
        <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
          {apiError}
        </Alert>
      )}

      {isMobile ? (
        <Box>
          {categories.map((c) => (
            <MobileListCard
              key={c.id.toString()}
              primary={<Typography sx={{ fontWeight: 600 }}>{c.name}</Typography>}
              secondaryRows={[
                {
                  label: t('pages.incomeCategoryConfig.table.taxArea'),
                  value: t(`components.taxArea.${c.taxArea}`),
                },
              ]}
              actions={
                <Box sx={{ display: 'flex' }}>
                  <Tooltip title={t('pages.incomeCategoryConfig.tooltip.edit')}>
                    <IconButton onClick={() => setEditing(c)} size="small">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={t('pages.incomeCategoryConfig.tooltip.delete')}>
                    <IconButton onClick={() => handleDelete(c.id)} color="error" size="small">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Box>
              }
            />
          ))}
        </Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table>
            <TableHead sx={{ bgcolor: 'action.hover' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.incomeCategoryConfig.table.name')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.incomeCategoryConfig.table.taxArea')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.incomeCategoryConfig.table.actions')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {categories.map((c) => (
                <TableRow key={c.id.toString()} hover>
                  <TableCell>{c.name}</TableCell>
                  <TableCell>{t(`components.taxArea.${c.taxArea}`)}</TableCell>
                  <TableCell align="right">
                    <Tooltip title={t('pages.incomeCategoryConfig.tooltip.edit')}>
                      <IconButton onClick={() => setEditing(c)}>
                        <EditIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t('pages.incomeCategoryConfig.tooltip.delete')}>
                      <IconButton onClick={() => handleDelete(c.id)} color="error">
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

      {(openDialog || editing) && (
        <IncomeCategoryDialog
          category={editing ?? undefined}
          onClose={closeDialog}
          onSaved={() => {
            closeDialog();
            loadCategories();
          }}
          onError={setApiError}
        />
      )}
    </Box>
  );
}
