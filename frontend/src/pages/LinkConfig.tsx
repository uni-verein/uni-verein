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
  InputAdornment,
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
import { Link } from '../types';
import { api } from '../api';
import AddLinkIcon from '@mui/icons-material/AddLink';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import ImageSearchIcon from '@mui/icons-material/ImageSearch';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
import { IconPickerDialog } from '../components/IconPickerDialog';
import { DynamicIcon } from '../components/muiIcons';

interface Data {
  items: Link[];
  total: number;
}

export default function LinkConfig() {
  const { t } = useTranslation();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });

  const [links, setLinks] = useState<Data>({ items: [], total: 0 });
  const [openDialog, setOpenDialog] = useState(false);
  const [openIconPicker, setOpenIconPicker] = useState(false);
  const [editLink, setEditLink] = useState<Link | null>(null);
  const [errors, setErrors] = useState<{ link?: string; name?: string; icon?: string }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [fetching, setFetching] = useState(true);
  const setConfigDeleteOrUpdate = useSnackbar();

  const [formData, setFormData] = useState({
    link: '',
    name: '',
    icon: '',
  });

  const loadConfig = async () => {
    try {
      const data = await api('/link');
      if (data) {
        setLinks(data);
      }
    } catch {
      setLinks({ items: [], total: 0 });
    } finally {
      setFetching(false);
    }
  };

  useEffect(() => {
    loadConfig();
  }, []);

  const handleOpen = (link: Link | null = null) => {
    if (link) {
      setEditLink(link);
      setFormData({ link: link.link, name: link.name, icon: link.icon });
    } else {
      setEditLink(null);
      setFormData({ link: '', name: '', icon: '' });
    }
    setErrors({});
    setOpenDialog(true);
  };

  const validate = () => {
    const newErrors: { link?: string; name?: string; icon?: string } = {};

    if (!formData.link.trim()) {
      newErrors.link = t('pages.linkConfig.validation.linkEmpty');
    } else if (formData.link.length > 100) {
      newErrors.link = t('pages.linkConfig.validation.linkTooLong');
    }

    if (!formData.name.trim()) {
      newErrors.name = t('pages.linkConfig.validation.nameEmpty');
    } else if (formData.name.length > 20) {
      newErrors.name = t('pages.linkConfig.validation.nameTooLong');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    try {
      if (editLink) {
        await api(`/link/${editLink.id}`, {
          method: 'PATCH',
          body: JSON.stringify(formData),
        });
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.linkConfig.snackbar.saveSuccess'),
        });
      } else {
        await api('/link', {
          method: 'POST',
          body: JSON.stringify(formData),
        });
        setConfigDeleteOrUpdate({
          status: 'success',
          message: t('pages.linkConfig.snackbar.createSuccess'),
        });
      }
      setOpenDialog(false);
      loadConfig();
    } catch {
      setApiError(t('pages.linkConfig.errors.saveFailed'));
    }
  };

  const handleDelete = async (id: UUIDTypes) => {
    setConfirmDialog({
      message: t('pages.linkConfig.confirm.deleteMessage'),
      buttonText: t('pages.linkConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });

    const confirmed = await confirm();
    if (!confirmed) return;

    try {
      await api(`/link/${id}`, { method: 'DELETE' });
      setConfigDeleteOrUpdate({
        status: 'success',
        message: t('pages.linkConfig.snackbar.deleteSuccess'),
      });
      loadConfig();
    } catch {
      setApiError(t('pages.linkConfig.errors.deleteFailed'));
    }
  };

  const handleIconSelect = (iconName: string) => {
    setFormData({ ...formData, icon: iconName });
    setErrors({ ...errors, icon: undefined });
  };

  return (
    <Box sx={{ p: 3 }}>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        confirmColor={confirmDialog.confirmColor}
        onClose={handleClose}
      />

      <Box
        sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3 }}
      >
        <Box>
          <Typography variant="h5" fontWeight={700}>
            {t('pages.linkConfig.title')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t('pages.linkConfig.description')}
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddLinkIcon />} onClick={() => handleOpen()}>
          {t('pages.linkConfig.buttons.add')}
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
              <TableCell sx={{ fontWeight: 700, width: '25%' }}>
                {t('pages.linkConfig.table.link')}
              </TableCell>
              <TableCell sx={{ fontWeight: 700, width: '25%' }}>
                {t('pages.linkConfig.table.name')}
              </TableCell>
              <TableCell sx={{ fontWeight: 700, width: '25%' }}>
                {t('pages.linkConfig.table.icon')}
              </TableCell>
              <TableCell align="right" sx={{ fontWeight: 700, width: '25%' }}>
                {t('pages.linkConfig.table.actions')}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {links.items.map((row) => (
              <TableRow key={row.id!.toString()} hover>
                <TableCell>{row.link}</TableCell>
                <TableCell>{row.name}</TableCell>
                <TableCell>
                  {row.icon ? (
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <DynamicIcon name={row.icon} fontSize="small" color="action" />
                      <Typography variant="body2" color="text.secondary">
                        {row.icon}
                      </Typography>
                    </Box>
                  ) : (
                    <Typography variant="body2" color="text.disabled">
                      —
                    </Typography>
                  )}
                </TableCell>

                <TableCell align="right">
                  <Tooltip title={t('pages.linkConfig.tooltip.edit')}>
                    <IconButton onClick={() => handleOpen(row)} color="primary">
                      <EditIcon />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={t('pages.linkConfig.tooltip.delete')}>
                    <IconButton onClick={() => handleDelete(row.id!)} color="error">
                      <DeleteIcon />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}

            {!fetching && links.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={4} align="center" sx={{ py: 4 }}>
                  <Typography variant="body2" color="text.secondary">
                    {t('pages.linkConfig.table.empty')}
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} fullWidth maxWidth="xs">
        <DialogTitle>
          {editLink
            ? t('pages.linkConfig.dialog.titleEdit')
            : t('pages.linkConfig.dialog.titleCreate')}
        </DialogTitle>

        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            sx={{ mt: 1 }}
            label={t('pages.linkConfig.fields.link.label')}
            fullWidth
            value={formData.link}
            error={!!errors.link}
            helperText={errors.link ?? `${formData.link.length}/100`}
            placeholder={t('pages.linkConfig.fields.link.placeholder')}
            onChange={(e) => {
              setFormData({ ...formData, link: e.target.value });
              setErrors({ ...errors, link: undefined });
            }}
          />

          <TextField
            sx={{ mt: 1 }}
            label={t('pages.linkConfig.fields.name.label')}
            fullWidth
            value={formData.name}
            error={!!errors.name}
            helperText={errors.name ?? `${formData.name.length}/20`}
            placeholder={t('pages.linkConfig.fields.name.placeholder')}
            onChange={(e) => {
              setFormData({ ...formData, name: e.target.value });
              setErrors({ ...errors, name: undefined });
            }}
          />

          <TextField
            label={t('pages.linkConfig.fields.icon.label')}
            fullWidth
            value={formData.icon}
            error={!!errors.icon}
            helperText={errors.icon}
            inputProps={{ readOnly: true }}
            onClick={() => setOpenIconPicker(true)}
            sx={{ cursor: 'pointer' }}
            InputProps={{
              startAdornment: formData.icon ? (
                <InputAdornment position="start">
                  <DynamicIcon name={formData.icon} fontSize="small" color="action" />
                </InputAdornment>
              ) : null,
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={() => setOpenIconPicker(true)} edge="end" size="small">
                    <ImageSearchIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
        </DialogContent>

        <DialogActions sx={{ p: 3 }}>
          <Button onClick={() => setOpenDialog(false)}>
            {t('pages.linkConfig.dialog.cancel')}
          </Button>
          <Button variant="contained" onClick={handleSave}>
            {t('pages.linkConfig.dialog.save')}
          </Button>
        </DialogActions>
      </Dialog>

      <IconPickerDialog
        open={openIconPicker}
        selectedIcon={formData.icon}
        onSelect={handleIconSelect}
        onClose={() => setOpenIconPicker(false)}
      />
    </Box>
  );
}
