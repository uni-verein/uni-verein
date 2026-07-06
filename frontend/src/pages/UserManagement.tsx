import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  ButtonProps,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
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
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import { api } from '../api';
import { Role, User, UserManagementProps } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';

export default function UserManagement({ userId, accountView }: UserManagementProps) {
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [users, setUsers] = useState<User[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editUser, setEditUser] = useState<User | null>(null);
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    role: Role.USER,
  });
  const [errors, setErrors] = useState<{ username?: string; email?: string; password?: string }>(
    {},
  );
  const [apiError, setApiError] = useState<string | null>(null);
  const setUserCreateOrUpdate = useSnackbar();
  const { t } = useTranslation();

  const loadUsers = async () => {
    try {
      const data = await api(`${accountView ? `/users/account` : '/users'}`);
      if (!accountView) {
        setUsers(data.items);
      } else {
        setUsers([data]);
      }
    } catch (e) {
      setApiError(t('pages.userManagement.apiError.loadFailed'));
    }
  };

  useEffect(() => {
    loadUsers().catch();
  }, []);

  const handleOpen = (user: any = null) => {
    setErrors({});
    setApiError(null);
    setUserCreateOrUpdate({ status: null, message: '' });

    if (user) {
      setEditUser(user);
      setFormData({ username: user.username, email: user.email, password: '', role: user.role });
    } else {
      setEditUser(null);
      setFormData({ username: '', email: '', password: '', role: Role.USER });
    }
    setOpenDialog(true);
  };

  const validate = () => {
    const newErrors: { username?: string; email?: string; password?: string } = {};

    if (!formData.username.trim()) {
      newErrors.username = t('pages.userManagement.validation.usernameEmpty');
    } else if (formData.username.length > 50) {
      newErrors.username = t('pages.userManagement.validation.usernameTooLong');
    }

    if (!editUser && !formData.password) {
      newErrors.password = t('pages.userManagement.validation.passwordEmpty');
    } else if (
      (!editUser && formData.password.length < 11) ||
      (editUser && formData.password.length > 0 && formData.password.length < 11)
    ) {
      newErrors.password = t('pages.userManagement.validation.passwordTooShort');
    } else if (formData.password.length > 50) {
      newErrors.password = t('pages.userManagement.validation.passwordTooLong');
    }

    if (formData.email.trim() && formData.email.length > 50) {
      newErrors.email = t('pages.userManagement.validation.emailTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      if (editUser) {
        let response = await api(accountView ? `/users/account` : `/users/${editUser.id}`, {
          method: 'PATCH',
          body: JSON.stringify(formData),
        });
        if (response === 409) {
          setApiError(t('pages.userManagement.apiError.updateDuplicate'));
          setUserCreateOrUpdate({
            status: 'error',
            message: t('pages.userManagement.snackbar.usernameDuplicate'),
          });
        } else {
          setUserCreateOrUpdate({
            status: 'success',
            message: t('pages.userManagement.snackbar.updateSuccess'),
          });
        }
      } else {
        let response = await api('/users', { method: 'POST', body: JSON.stringify(formData) });
        if (response === 409) {
          setApiError(t('pages.userManagement.apiError.createDuplicate'));
          setUserCreateOrUpdate({
            status: 'error',
            message: t('pages.userManagement.snackbar.usernameDuplicate'),
          });
        } else {
          setUserCreateOrUpdate({
            status: 'success',
            message: t('pages.userManagement.snackbar.createSuccess'),
          });
        }
      }
      setOpenDialog(false);
      await loadUsers();
    } catch (e) {
      setApiError(t('pages.userManagement.apiError.saveFailed'));
      setUserCreateOrUpdate({
        status: 'error',
        message: t('pages.userManagement.snackbar.saveFailed'),
      });
    }
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.userManagement.confirm.deleteMessage'),
      buttonText: t('pages.userManagement.confirm.deleteButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      try {
        await api(`/users/${id}`, { method: 'DELETE' });
        setUserCreateOrUpdate({
          status: 'success',
          message: t('pages.userManagement.snackbar.deleteSuccess'),
        });
        await loadUsers();
      } catch (e) {
        setApiError(t('pages.userManagement.apiError.deleteFailed'));
        setUserCreateOrUpdate({
          status: 'error',
          message: t('pages.userManagement.snackbar.deleteFailed'),
        });
      }
    }
  };

  const getRoleTranslation = (role: Role): string => {
    switch (role) {
      case Role.ADMIN:
        return t('pages.userManagement.dialog.roles.admin');
      case Role.USER:
        return t('pages.userManagement.dialog.roles.user');
      case Role.FINANCIAL_MANAGER:
        return t('pages.userManagement.dialog.roles.financialManager');
      default:
        return '';
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
          {t('pages.userManagement.title')}
        </Typography>
        {!accountView && (
          <Button variant="contained" startIcon={<PersonAddIcon />} onClick={() => handleOpen()}>
            {t('pages.userManagement.buttons.newUser')}
          </Button>
        )}
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
                {t('pages.userManagement.table.username')}
              </TableCell>
              <TableCell sx={{ fontWeight: 700 }}>
                {t('pages.userManagement.table.email')}
              </TableCell>
              <TableCell sx={{ fontWeight: 700 }}>{t('pages.userManagement.table.role')}</TableCell>
              <TableCell align="right" sx={{ fontWeight: 700 }}>
                {t('pages.userManagement.table.actions')}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((u) => (
              <TableRow key={u.id.toString()} hover>
                <TableCell>{u.username}</TableCell>
                <TableCell>{u.email}</TableCell>
                <TableCell>
                  <Chip
                    label={getRoleTranslation(u.role)}
                    color={u.role === Role.ADMIN ? 'secondary' : 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell align="right">
                  <Tooltip title={t('pages.userManagement.tooltip.edit')}>
                    <IconButton onClick={() => handleOpen(u)} color="primary">
                      <EditIcon />
                    </IconButton>
                  </Tooltip>
                  {!accountView && (
                    <Tooltip title={t('pages.userManagement.tooltip.delete')}>
                      <IconButton onClick={() => handleDelete(u.id)} color="error">
                        <DeleteIcon />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create/Edit Dialog */}
      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} fullWidth maxWidth="xs">
        <DialogTitle>
          {editUser
            ? t('pages.userManagement.dialog.titleEdit')
            : t('pages.userManagement.dialog.titleCreate')}
        </DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.userManagement.dialog.fields.username')}
            fullWidth
            value={formData.username}
            onChange={(e) => {
              setFormData({ ...formData, username: e.target.value });
              if (errors.username !== undefined) setErrors({ ...errors, username: undefined });
            }}
            error={!!errors.username}
            helperText={errors.username ?? `${formData.username.length}/50`}
            slotProps={{ htmlInput: { maxLength: 50 } }}
          />
          <TextField
            label={
              editUser
                ? t('pages.userManagement.dialog.fields.passwordEdit')
                : t('pages.userManagement.dialog.fields.passwordCreate')
            }
            type="password"
            fullWidth
            value={formData.password}
            autoComplete={'new-password'}
            onChange={(e) => {
              setFormData({ ...formData, password: e.target.value });
              if (errors.password !== undefined) setErrors({ ...errors, password: undefined });
            }}
            error={!!errors.password}
            helperText={errors.password ?? `${formData.password.length}/50`}
            slotProps={{ htmlInput: { maxLength: 50 } }}
          />
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.userManagement.dialog.fields.email')}
            fullWidth
            value={formData.email}
            onChange={(e) => {
              setFormData({ ...formData, email: e.target.value });
              if (errors.email !== undefined) setErrors({ ...errors, email: undefined });
            }}
            error={!!errors.email}
            helperText={errors.email ?? `${formData.email !== null ? formData.email.length : 0}/50`}
            slotProps={{ htmlInput: { maxLength: 50 } }}
          />
          {!accountView && (
            <FormControl fullWidth>
              <InputLabel>{t('pages.userManagement.dialog.fields.role')}</InputLabel>
              <Select
                value={formData.role}
                label={t('pages.userManagement.dialog.fields.role')}
                onChange={(e) => setFormData({ ...formData, role: e.target.value })}
                disabled={userId === editUser?.id}
              >
                <MenuItem value={Role.USER}>{t('pages.userManagement.dialog.roles.user')}</MenuItem>
                <MenuItem value={Role.ADMIN}>
                  {t('pages.userManagement.dialog.roles.admin')}
                </MenuItem>
                <MenuItem value={Role.FINANCIAL_MANAGER}>
                  {t('pages.userManagement.dialog.roles.financialManager')}
                </MenuItem>
              </Select>
            </FormControl>
          )}
        </DialogContent>
        <DialogActions sx={{ p: 3 }}>
          <Button onClick={() => setOpenDialog(false)}>
            {t('pages.userManagement.buttons.cancel')}
          </Button>
          <Button variant="contained" onClick={handleSave}>
            {t('pages.userManagement.buttons.save')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
