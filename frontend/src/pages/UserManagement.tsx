import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  ButtonProps,
  Chip,
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
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import { api } from '../api';
import { Role, User, UserManagementProps } from '../types';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
import { MobileListCard } from '../components/MobileListCard';
import { UserDialog } from '../components/dialogs/UserDialog';

export default function UserManagement({ userId, accountView }: UserManagementProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [users, setUsers] = useState<User[]>([]);
  const [openDialog, setOpenDialog] = useState(false);
  const [editUser, setEditUser] = useState<User | null>(null);
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

  const handleOpen = (user: User | null = null) => {
    setApiError(null);
    setUserCreateOrUpdate({ status: null, message: '' });
    setEditUser(user);
    setOpenDialog(true);
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

      <Box
        sx={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 2, mb: 3 }}
      >
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

      {isMobile ? (
        <Box>
          {users.map((u) => (
            <MobileListCard
              key={u.id.toString()}
              primary={
                <Box sx={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
                  <Typography sx={{ fontWeight: 600 }}>{u.username}</Typography>
                  <Chip
                    label={getRoleTranslation(u.role)}
                    color={u.role === Role.ADMIN ? 'secondary' : 'default'}
                    size="small"
                  />
                </Box>
              }
              secondaryRows={[{ label: t('pages.userManagement.table.email'), value: u.email }]}
              actions={
                <>
                  <Tooltip title={t('pages.userManagement.tooltip.edit')}>
                    <IconButton onClick={() => handleOpen(u)} color="primary" size="small">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  {!accountView && (
                    <Tooltip title={t('pages.userManagement.tooltip.delete')}>
                      <IconButton onClick={() => handleDelete(u.id)} color="error" size="small">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
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
                  {t('pages.userManagement.table.username')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.userManagement.table.email')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.userManagement.table.role')}
                </TableCell>
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
      )}

      {openDialog && (
        <UserDialog
          user={editUser}
          currentUserId={userId}
          accountView={accountView}
          onClose={() => setOpenDialog(false)}
          onSaved={() => {
            setOpenDialog(false);
            loadUsers();
          }}
          onError={setApiError}
        />
      )}
    </Box>
  );
}
