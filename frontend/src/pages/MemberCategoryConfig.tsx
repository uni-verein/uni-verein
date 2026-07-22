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
import GroupAddIcon from '@mui/icons-material/GroupAdd';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { MemberCategory } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
import { MobileListCard } from '../components/MobileListCard';
import { MemberCategoryDialog } from '../components/dialogs/MemberCategoryDialog';

export default function MemberCategoryConfig() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [memberCategories, setMemberCategories] = useState<MemberCategory[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editMemberCategory, setEditMemberCategory] = useState<MemberCategory | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const setMemberCategoryChange = useSnackbar();
  const { t } = useTranslation();

  const loadMemberCategories = async () => {
    try {
      const data = await api('/member-categories');
      if (data) {
        setMemberCategories(data.items);
      }
    } catch (error) {}
  };

  useEffect(() => {
    loadMemberCategories();
  }, []);

  const handleOpen = (memberCategory: MemberCategory | null = null) => {
    setEditMemberCategory(memberCategory);
    setOpenDialog(true);
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.memberCategoryConfig.confirm.deleteMessage'),
      buttonText: t('pages.memberCategoryConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/member-categories/${id}`, { method: 'DELETE' });
        setMemberCategoryChange({
          status: 'success',
          message: t('pages.memberCategoryConfig.snackbar.deleteSuccess'),
        });
        await loadMemberCategories();
        // @ts-ignore
      } catch (error: Error) {
        setApiError(
          t(
            'pages.memberCategoryConfig.apiError.' +
              (error.message === 'Bad Request' ? 'deleteFailedMember' : 'deleteFailed'),
          ),
        );
        setMemberCategoryChange({
          status: 'error',
          message: t(
            'pages.memberCategoryConfig.snackbar.' +
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
          {t('pages.memberCategoryConfig.title')}
        </Typography>
        <Button variant="contained" startIcon={<GroupAddIcon />} onClick={() => handleOpen()}>
          {t('pages.memberCategoryConfig.newButton')}
        </Button>
      </Box>

      {apiError && (
        <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
          {apiError}
        </Alert>
      )}

      {isMobile ? (
        <Box>
          {memberCategories.map((m) => {
            if (m.category === 'ALL') return null;

            return (
              <MobileListCard
                key={m.id.toString()}
                primary={<Typography sx={{ fontWeight: 600 }}>{m.name}</Typography>}
                secondaryRows={[
                  { label: t('pages.memberCategoryConfig.table.category'), value: m.category },
                ]}
                actions={
                  <>
                    <Tooltip title={t('pages.memberCategoryConfig.tooltip.edit')}>
                      <IconButton onClick={() => handleOpen(m)} color="primary" size="small">
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t('pages.memberCategoryConfig.tooltip.delete')}>
                      <IconButton onClick={() => handleDelete(m.id)} color="error" size="small">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </>
                }
              />
            );
          })}
        </Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table>
            <TableHead sx={{ bgcolor: 'grey.50' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.memberCategoryConfig.table.name')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.memberCategoryConfig.table.category')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.memberCategoryConfig.table.actions')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {memberCategories.map((m) => {
                if (m.category === 'ALL') return null;

                return (
                  <TableRow key={m.id.toString()} hover>
                    <TableCell>{m.name}</TableCell>
                    <TableCell>{m.category}</TableCell>
                    <TableCell align="right">
                      <Tooltip title={t('pages.memberCategoryConfig.tooltip.edit')}>
                        <IconButton onClick={() => handleOpen(m)} color="primary">
                          <EditIcon />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('pages.memberCategoryConfig.tooltip.delete')}>
                        <IconButton onClick={() => handleDelete(m.id)} color="error">
                          <DeleteIcon />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {openDialog && (
        <MemberCategoryDialog
          memberCategory={editMemberCategory}
          onClose={() => setOpenDialog(false)}
          onSaved={() => {
            setOpenDialog(false);
            loadMemberCategories();
          }}
          onError={setApiError}
        />
      )}
    </Box>
  );
}
