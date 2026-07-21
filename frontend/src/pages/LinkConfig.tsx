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
import { Link } from '../types';
import { api } from '../api';
import AddLinkIcon from '@mui/icons-material/AddLink';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { UUIDTypes } from 'uuid';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../components/SnackbarContext';
import { useTranslation } from 'react-i18next';
import { DynamicIcon } from '../components/muiIcons';
import { MobileListCard } from '../components/MobileListCard';
import { LinkDialog } from '../components/dialogs/LinkDialog';

interface Data {
  items: Link[];
  total: number;
}

export default function LinkConfig() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });

  const [links, setLinks] = useState<Data>({ items: [], total: 0 });
  const [openDialog, setOpenDialog] = useState(false);
  const [editLink, setEditLink] = useState<Link | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [fetching, setFetching] = useState(true);
  const setConfigDeleteOrUpdate = useSnackbar();

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
    setEditLink(link);
    setOpenDialog(true);
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

  return (
    <Box sx={{ p: { xs: 0, sm: 3 } }}>
      <ConfirmDialog
        open={open}
        message={confirmDialog.message}
        buttonText={confirmDialog.buttonText}
        confirmColor={confirmDialog.confirmColor}
        onClose={handleClose}
      />

      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: 2,
          mb: 3,
        }}
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

      {isMobile ? (
        <Box>
          {links.items.map((row) => (
            <MobileListCard
              key={row.id!.toString()}
              primary={<Typography sx={{ fontWeight: 600 }}>{row.name}</Typography>}
              secondaryRows={[
                { label: t('pages.linkConfig.table.link'), value: row.link },
                {
                  label: t('pages.linkConfig.table.icon'),
                  value: row.icon ? (
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <DynamicIcon name={row.icon} fontSize="small" color="action" />
                      <Typography variant="body2" color="text.secondary">
                        {row.icon}
                      </Typography>
                    </Box>
                  ) : (
                    '—'
                  ),
                },
              ]}
              actions={
                <>
                  <Tooltip title={t('pages.linkConfig.tooltip.edit')}>
                    <IconButton onClick={() => handleOpen(row)} color="primary" size="small">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={t('pages.linkConfig.tooltip.delete')}>
                    <IconButton onClick={() => handleDelete(row.id!)} color="error" size="small">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </>
              }
            />
          ))}
          {!fetching && links.items.length === 0 && (
            <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
              {t('pages.linkConfig.table.empty')}
            </Typography>
          )}
        </Box>
      ) : (
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
      )}

      {openDialog && (
        <LinkDialog
          link={editLink}
          onClose={() => setOpenDialog(false)}
          onSaved={() => {
            setOpenDialog(false);
            loadConfig();
          }}
          onError={setApiError}
        />
      )}
    </Box>
  );
}
