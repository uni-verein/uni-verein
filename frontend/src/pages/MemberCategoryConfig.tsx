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
  IconButton,
  Paper,
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
import GroupAddIcon from '@mui/icons-material/GroupAdd';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { MemberCategory } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';

export default function MemberCategoryConfig() {
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [memberCategories, setMemberCategories] = useState<MemberCategory[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editMemberCategory, setEditMemberCategory] = useState<MemberCategory | null>(null);
  const [errors, setErrors] = useState<{ name?: string; category?: string }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const setMemberCategoryChange = useSnackbar();
  const { t } = useTranslation();

  const [formData, setFormData] = useState({
    name: '',
    category: '',
  });

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
    if (memberCategory) {
      setEditMemberCategory(memberCategory);
      setFormData({
        name: memberCategory.name,
        category: memberCategory.category.toString().replace('.', ','),
      });
    } else {
      setEditMemberCategory(null);
      setFormData({ name: '', category: '' });
    }
    setOpenDialog(true);
  };

  const validate = () => {
    const newErrors: { name?: string } = {};

    if (!formData.name.trim()) {
      newErrors.name = t('pages.memberCategoryConfig.validation.nameEmpty');
    } else if (formData.name.length > 50) {
      newErrors.name = t('pages.memberCategoryConfig.validation.nameTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    let data = {
      name: formData.name,
      category: formData.category,
    };

    try {
      if (editMemberCategory) {
        let response = await api(`/member-categories/${editMemberCategory.id}`, {
          method: 'PUT',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          setApiError(t('pages.memberCategoryConfig.apiError.updateExists'));
          setMemberCategoryChange({
            status: 'error',
            message: t('pages.memberCategoryConfig.snackbar.updateExists'),
          });
        } else {
          setMemberCategoryChange({
            status: 'success',
            message: t('pages.memberCategoryConfig.snackbar.updateSuccess'),
          });
        }
      } else {
        let response = await api('/member-categories', {
          method: 'POST',
          body: JSON.stringify(data),
        });
        if (response === 409) {
          setApiError(t('pages.memberCategoryConfig.apiError.createExists'));
          setMemberCategoryChange({
            status: 'error',
            message: t('pages.memberCategoryConfig.snackbar.createExists'),
          });
        } else {
          setMemberCategoryChange({
            status: 'success',
            message: t('pages.memberCategoryConfig.snackbar.createSuccess'),
          });
        }
      }
    } catch (error) {
      setApiError(t('pages.memberCategoryConfig.apiError.saveFailed'));
      setMemberCategoryChange({
        status: 'error',
        message: t('pages.memberCategoryConfig.snackbar.saveFailed'),
      });
    }
    setOpenDialog(false);
    await loadMemberCategories();
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

      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3 }}>
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

      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} fullWidth maxWidth="xs">
        <DialogTitle>
          {editMemberCategory
            ? t('pages.memberCategoryConfig.dialog.titleEdit')
            : t('pages.memberCategoryConfig.dialog.titleCreate')}
        </DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.memberCategoryConfig.dialog.nameLabel')}
            fullWidth
            required
            value={formData.name}
            error={errors.name !== undefined}
            helperText={errors.name ?? `${formData.name.length}/100`}
            onChange={(e) => {
              setFormData({
                ...formData,
                name: e.target.value,
                category: e.target.value.replace(' ', '_').trim().toUpperCase(),
              });
              setErrors({ ...errors, name: undefined });
            }}
          />
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.memberCategoryConfig.dialog.categoryLabel')}
            fullWidth
            value={formData.category}
            error={errors.category !== undefined}
            helperText={errors.category ?? `${formData.category.length}/100`}
            disabled={true}
          />
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button onClick={() => setOpenDialog(false)}>
            {t('pages.memberCategoryConfig.dialog.cancel')}
          </Button>
          <Button variant="contained" onClick={handleSave}>
            {t('pages.memberCategoryConfig.dialog.save')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
