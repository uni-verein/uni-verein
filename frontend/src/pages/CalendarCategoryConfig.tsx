import { useCallback, useEffect, useState } from 'react';
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
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { CalendarCategory } from '../types';
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog';
import { useConfirm } from '../hooks/useConfirm';
import { useSnackbar } from '../hooks/useSnackbar';
import { MobileListCard } from '../components/MobileListCard';
import { CalendarCategoryDialog } from '../components/dialogs/CalendarCategoryDialog';
import { PublicCalendarSettings } from '../components/PublicCalendarSettings';
import PublicIcon from '@mui/icons-material/Public';

function ColorDot({ color }: { color: string }) {
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-block',
        width: 14,
        height: 14,
        borderRadius: '50%',
        bgcolor: color,
        mr: 1,
        verticalAlign: 'middle',
      }}
    />
  );
}

export default function CalendarCategoryConfig() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });
  const [categories, setCategories] = useState<CalendarCategory[]>([]);
  const [dialog, setDialog] = useState<{ open: boolean; category?: CalendarCategory }>({
    open: false,
  });
  const [apiError, setApiError] = useState<string | null>(null);
  const setSnackbar = useSnackbar();
  const { t } = useTranslation();

  const loadCategories = useCallback(async () => {
    try {
      const data = await api('/calendar/categories');
      if (data) setCategories(data.items);
    } catch {
      // best-effort: table just stays empty if this fails
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadCategories();
  }, [loadCategories]);

  const handleDelete = async (category: CalendarCategory) => {
    setConfirmDialog({
      message: t('pages.calendarCategoryConfig.confirm.deleteMessage', { name: category.name }),
      buttonText: t('pages.calendarCategoryConfig.confirm.deleteButton'),
      confirmColor: 'error',
    });

    if (!(await confirm())) return;
    try {
      await api(`/calendar/categories/${category.id}`, { method: 'DELETE' });
      setSnackbar({
        status: 'success',
        message: t('pages.calendarCategoryConfig.snackbar.deleteSuccess'),
      });
      await loadCategories();
    } catch {
      setApiError(t('pages.calendarCategoryConfig.apiError.deleteFailed'));
    }
  };

  const actions = (c: CalendarCategory, size: 'small' | 'medium') => (
    <>
      <Tooltip title={t('pages.calendarCategoryConfig.tooltip.edit')}>
        <IconButton onClick={() => setDialog({ open: true, category: c })} size={size}>
          <EditIcon fontSize={size} />
        </IconButton>
      </Tooltip>
      <Tooltip title={t('pages.calendarCategoryConfig.tooltip.delete')}>
        <IconButton onClick={() => handleDelete(c)} color="error" size={size}>
          <DeleteIcon fontSize={size} />
        </IconButton>
      </Tooltip>
    </>
  );

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
          {t('pages.calendarCategoryConfig.title')}
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setDialog({ open: true })}
        >
          {t('pages.calendarCategoryConfig.newButton')}
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
              key={c.id}
              primary={
                <Typography sx={{ fontWeight: 600 }}>
                  <ColorDot color={c.color} />
                  {c.name}
                  {c.isPublic && (
                    <PublicIcon
                      fontSize="small"
                      color="info"
                      sx={{ ml: 1, verticalAlign: 'middle' }}
                      titleAccess={t('pages.calendarCategoryConfig.table.public')}
                    />
                  )}
                </Typography>
              }
              actions={actions(c, 'small')}
            />
          ))}
        </Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table>
            <TableHead sx={{ bgcolor: 'action.hover' }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.calendarCategoryConfig.table.name')}
                </TableCell>
                <TableCell sx={{ fontWeight: 700 }}>
                  {t('pages.calendarCategoryConfig.table.public')}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {t('pages.calendarCategoryConfig.table.actions')}
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {categories.map((c) => (
                <TableRow key={c.id} hover>
                  <TableCell>
                    <ColorDot color={c.color} />
                    {c.name}
                  </TableCell>
                  <TableCell>
                    {c.isPublic ? (
                      <PublicIcon
                        color="info"
                        titleAccess={t('pages.calendarCategoryConfig.table.public')}
                      />
                    ) : (
                      '–'
                    )}
                  </TableCell>
                  <TableCell align="right">{actions(c, 'medium')}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <PublicCalendarSettings categories={categories} />

      {dialog.open && (
        <CalendarCategoryDialog
          category={dialog.category}
          onClose={() => setDialog({ open: false })}
          onSaved={() => {
            setDialog({ open: false });
            loadCategories();
          }}
          onError={setApiError}
        />
      )}
    </Box>
  );
}
