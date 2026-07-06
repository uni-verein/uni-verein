import React, { useEffect, useState } from 'react';
import {
  Box,
  Typography,
  Button,
  Paper,
  Grid,
  Card,
  CardContent,
  Stack,
  Alert,
  AlertTitle,
  ButtonProps,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Divider,
} from '@mui/material';
import RestoreIcon from '@mui/icons-material/Restore';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import DownloadFileIcon from '@mui/icons-material/Download';
import CloudDownloadIcon from '@mui/icons-material/CloudDownload';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { api, apiFile } from '../api';
import { MemberCategory } from '../types';
import { useSnackbar } from '../components/SnackbarContext';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useIndexedTranslation } from '../hooks/useIndexedTranslation';

export default function Backup() {
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [loading, setLoading] = useState(false);
  const [memberCount, setMemberCount] = useState<number>(0);
  const [importErrors, setImportErrors] = useState<
    Array<{ translationKey: string; values: string[] }>
  >([]);
  const [importErrorDialogOpen, setImportErrorDialogOpen] = useState(false);
  const setSnackbar = useSnackbar();
  const { ti } = useIndexedTranslation();

  const load = async (query: MemberCategory | null) => {
    const data = await api('/members/count' + (query === null ? '' : `?memberCategory=${query}`));
    if (data) {
      setMemberCount(data?.count ?? 0);
    }
  };

  useEffect(() => {
    load(null);
  }, []);

  const download = async () => {
    setLoading(true);
    try {
      const res = await apiFile('/backup', { method: 'GET' });
      if (!res.ok)
        setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.downloadError') });

      if (res.status === 200) {
        const blob = await res.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Verein_Backup_${new Date().toISOString().split('T')[0]}.sql`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setSnackbar({ status: 'success', message: ti('pages.backup.snackbar.downloadSuccess') });
      }
    } catch (e) {
      console.error(e);
      setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.downloadError') });
    } finally {
      setLoading(false);
    }
  };

  const restore = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setConfirmDialog({
      message: ti('pages.backup.systemRestore.confirmMessage'),
      buttonText: ti('pages.backup.systemRestore.confirmButton'),
      confirmColor: 'error',
    });
    const confirmed = await confirm();
    if (confirmed) {
      const formData = new FormData();
      formData.append('file', file);
      const res = await apiFile('/backup/restore', { method: 'POST', body: formData });
      if (!res.ok) {
        setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.restoreError') });
      } else {
        setSnackbar({ status: 'success', message: ti('pages.backup.snackbar.restoreSuccess') });
      }

      await load(null);
    }
  };

  const downloadExampleCsv = async () => {
    setLoading(true);
    try {
      const res = await apiFile('/import/example', { method: 'GET' });
      if (!res.ok) {
        setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.exampleError') });
      }

      if (res.status === 200) {
        const blob = await res.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `example_${new Date().toISOString().split('T')[0]}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setSnackbar({ status: 'success', message: ti('pages.backup.snackbar.downloadSuccess') });
      }
    } catch (e) {
      setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.exampleError') });
    } finally {
      setLoading(false);
    }
  };

  const importMemberCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setConfirmDialog({
      message: ti('pages.backup.csvImport.confirmMessage'),
      buttonText: ti('pages.backup.csvImport.confirmButton'),
      confirmColor: 'success',
    });
    const confirmed = await confirm();
    if (confirmed) {
      const formData = new FormData();
      formData.append('file', file);
      const res = await apiFile('/import/upload', { method: 'POST', body: formData });
      if (!res.ok) {
        setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.importError') });

        res
          .json()
          .then((x) => {
            setImportErrors(x.errorResultTranslations);
            setImportErrorDialogOpen(true);
          })
          .catch((e) => console.log(e));

        if (res.status === 404) {
        }
      } else {
        setSnackbar({ status: 'success', message: ti('pages.backup.snackbar.importSuccess') });
      }
      await load(null);
    }
  };

  const downloadCsv = async () => {
    setLoading(true);
    try {
      const res = await apiFile('/import/export', { method: 'GET' });
      if (!res.ok) {
        setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.exportError') });
      }

      if (res.status === 200) {
        const blob = await res.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `export_${new Date().toISOString().split('T')[0]}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setSnackbar({ status: 'success', message: ti('pages.backup.snackbar.exportSuccess') });
      }
    } catch (e) {
      setSnackbar({ status: 'error', message: ti('pages.backup.snackbar.exportError') });
    } finally {
      setLoading(false);
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

      {/* Import Error Dialog */}
      <Dialog
        open={importErrorDialogOpen}
        onClose={() => setImportErrorDialogOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>
          <Stack direction="row" spacing={1} alignItems="center">
            <ErrorOutlineIcon color="error" />
            <Typography variant="h6" fontWeight={700}>
              {ti('pages.backup.csvImport.importErrors.title')}
            </Typography>
          </Stack>
        </DialogTitle>

        <DialogContent dividers>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {ti('pages.backup.csvImport.importErrors.description')}
          </Typography>
          <List disablePadding>
            {importErrors.map((error, index) => (
              <React.Fragment key={index}>
                <ListItem alignItems="flex-start" sx={{ px: 0 }}>
                  <ListItemIcon sx={{ minWidth: 32, mt: 0.5 }}>
                    <ErrorOutlineIcon color="error" fontSize="small" />
                  </ListItemIcon>
                  <ListItemText
                    primary={ti(`pages.backup.csvImport.${error.translationKey}`, error.values)}
                    primaryTypographyProps={{
                      variant: 'body2',
                    }}
                  />
                </ListItem>
                {index < importErrors.length - 1 && <Divider component="li" />}
              </React.Fragment>
            ))}
          </List>
        </DialogContent>

        <DialogActions>
          <Button variant="contained" color="error" onClick={() => setImportErrorDialogOpen(false)}>
            {ti('pages.backup.csvImport.importErrors.closeButton')}
          </Button>
        </DialogActions>
      </Dialog>

      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          {ti('pages.backup.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {ti('pages.backup.subtitle')}
        </Typography>
      </Box>

      <Alert severity="info" sx={{ mb: 4, borderRadius: 2 }}>
        <AlertTitle sx={{ fontWeight: 700 }}>{ti('pages.backup.alert.title')}</AlertTitle>
        {ti('pages.backup.alert.message')}
      </Alert>

      <Grid container spacing={3}>
        <Grid size={12}>
          <Card variant="outlined" sx={{ height: '100%', borderRadius: 3 }}>
            <CardContent>
              <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
                <CloudDownloadIcon color="primary" />
                <Typography variant="h6" fontWeight={600}>
                  {ti('pages.backup.systemBackup.heading')}
                </Typography>
              </Stack>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
                {ti('pages.backup.systemBackup.description')}
              </Typography>
              <Button
                variant="contained"
                startIcon={<CloudDownloadIcon />}
                onClick={download}
                disabled={loading}
                fullWidth
              >
                {ti('pages.backup.systemBackup.button')}
              </Button>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={12}>
          <Card variant="outlined" sx={{ height: '100%', borderRadius: 3, borderStyle: 'dashed' }}>
            <CardContent>
              <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
                <RestoreIcon color="error" />
                <Typography variant="h6" fontWeight={600}>
                  {ti('pages.backup.systemRestore.heading')}
                </Typography>
              </Stack>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
                {ti('pages.backup.systemRestore.description')}
              </Typography>
              <Button
                variant="outlined"
                color="error"
                component="label"
                startIcon={<RestoreIcon />}
                fullWidth
              >
                {ti('pages.backup.systemRestore.button')}
                <input type="file" hidden onChange={restore} />
              </Button>
            </CardContent>
          </Card>
        </Grid>

        {memberCount === 0 ? (
          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 3, mt: 2, borderRadius: 3, bgcolor: 'grey.50' }}>
              <Grid container spacing={2} alignItems="center">
                <Grid size={12}>
                  <Stack direction="row" spacing={2} alignItems="center">
                    <UploadFileIcon color="action" />
                    <Box>
                      <Typography variant="subtitle1" fontWeight={600}>
                        {ti('pages.backup.csvImport.heading')}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {ti('pages.backup.csvImport.description')}
                      </Typography>
                    </Box>
                  </Stack>
                </Grid>

                <Grid size={6} sx={{ textAlign: 'right' }}>
                  <Button
                    variant="outlined"
                    component="label"
                    startIcon={<DownloadFileIcon />}
                    onClick={downloadExampleCsv}
                    disabled={loading}
                    fullWidth
                  >
                    {ti('pages.backup.csvExample.button')}
                  </Button>
                </Grid>

                <Grid size={6} sx={{ textAlign: 'right' }}>
                  <Button
                    variant="outlined"
                    component="label"
                    startIcon={<UploadFileIcon />}
                    fullWidth
                  >
                    {ti('pages.backup.csvImport.button')}
                    <input type="file" hidden onChange={importMemberCsv} />
                  </Button>
                </Grid>
              </Grid>
            </Paper>
          </Grid>
        ) : (
          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 3, mt: 2, borderRadius: 3, bgcolor: 'grey.50' }}>
              <Grid container spacing={2} alignItems="center">
                <Grid size={12}>
                  <Stack direction="row" spacing={2} alignItems="center">
                    <DownloadFileIcon color="action" />
                    <Box>
                      <Typography variant="subtitle1" fontWeight={600}>
                        {ti('pages.backup.csvExport.heading')}
                      </Typography>
                    </Box>
                  </Stack>
                </Grid>
                <Grid size={12} sx={{ textAlign: 'center' }}>
                  <Button
                    variant="outlined"
                    component="label"
                    startIcon={<DownloadFileIcon />}
                    onClick={downloadCsv}
                    disabled={loading}
                    fullWidth
                  >
                    {ti('pages.backup.csvExport.button')}
                  </Button>
                </Grid>
              </Grid>
            </Paper>
          </Grid>
        )}
      </Grid>
    </Box>
  );
}
