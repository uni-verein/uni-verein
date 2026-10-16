import { Box, Button, DialogActions } from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import CloseIcon from '@mui/icons-material/Close';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import EventBusyIcon from '@mui/icons-material/EventBusy';
import { useTranslation } from 'react-i18next';
import { CalendarEvent } from '../../types';

export function CalendarEventDialogActions({
  readOnly,
  calendarEvent,
  hasOccurrence,
  saving,
  onDelete,
  onDeleteOccurrence,
  onCancel,
  onEdit,
}: {
  readOnly: boolean;
  calendarEvent: CalendarEvent | null;
  hasOccurrence: boolean;
  saving: boolean;
  onDelete: () => void;
  onDeleteOccurrence: () => void;
  onCancel: () => void;
  onEdit: () => void;
}) {
  const { t } = useTranslation();

  return (
    <DialogActions sx={{ p: 3, gap: 1, flexWrap: 'wrap' }}>
      {readOnly && calendarEvent?.canDelete && (
        <Button color="error" startIcon={<DeleteIcon />} onClick={onDelete}>
          {calendarEvent.recurrenceRule
            ? t('components.calendarEventForm.buttons.deleteSeries')
            : t('components.calendarEventForm.buttons.delete')}
        </Button>
      )}
      {readOnly && calendarEvent?.canEdit && calendarEvent.recurrenceRule && hasOccurrence && (
        <Button color="error" startIcon={<EventBusyIcon />} onClick={onDeleteOccurrence}>
          {t('components.calendarEventForm.buttons.deleteOccurrence')}
        </Button>
      )}
      <Box sx={{ flexGrow: 1 }} />
      {readOnly && calendarEvent?.canEdit && (
        <Button
          variant="contained"
          startIcon={<EditIcon />}
          onClick={(e) => {
            e.preventDefault();
            onEdit();
          }}
        >
          {t('components.calendarEventForm.buttons.edit')}
        </Button>
      )}
      <Button onClick={onCancel} color="inherit" startIcon={<CloseIcon />}>
        {readOnly
          ? t('components.calendarEventForm.buttons.close')
          : t('components.calendarEventForm.buttons.cancel')}
      </Button>
      {!readOnly && (
        <Button type="submit" variant="contained" startIcon={<SaveIcon />} disabled={saving}>
          {t('components.calendarEventForm.buttons.save')}
        </Button>
      )}
    </DialogActions>
  );
}
