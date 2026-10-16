import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import { ConfirmDialog } from './ConfirmDialog';
import { CalendarEventDetails } from '../calendarEvent/CalendarEventDetails';
import { CalendarEventDialogActions } from '../calendarEvent/CalendarEventDialogActions';
import { CalendarEventFields } from '../calendarEvent/CalendarEventFields';
import { SubscriptionToggle } from '../calendarEvent/SubscriptionToggle';
import { useCalendarEventForm } from '../calendarEvent/useCalendarEventForm';

export default function CalendarEventForm({
  eventId,
  initialStart,
  initialEnd,
  initialAllDay,
  occurrence,
  currentUserId,
  publicCalendarEnabled = false,
  onClose,
  onChanged,
}: {
  eventId?: string;
  initialStart?: Dayjs;
  initialEnd?: Dayjs;
  initialAllDay?: boolean;
  // Start and end of the clicked occurrence when the event is part of a series.
  occurrence?: { start: string; end: string };
  currentUserId: string | undefined;
  publicCalendarEnabled?: boolean;
  onClose: () => void;
  onChanged: () => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const form = useCalendarEventForm({
    eventId,
    initialStart,
    initialEnd,
    initialAllDay,
    occurrence,
    onClose,
    onChanged,
  });
  const { mode, calendarEvent } = form;

  const readOnly = mode === 'view';
  const isOwner = !calendarEvent || calendarEvent.owner.id === currentUserId;

  return (
    <>
      <ConfirmDialog {...form.confirmDialog} confirmColor="error" />
      <Dialog
        open={true}
        onClose={onClose}
        fullWidth={!isMobile}
        fullScreen={isMobile}
        maxWidth="sm"
        slotProps={{ paper: { sx: { borderRadius: isMobile ? 0 : 3, p: 1 } } }}
      >
        <form onSubmit={form.save} noValidate>
          <DialogTitle
            component="div"
            sx={{
              display: 'flex',
              alignItems: 'flex-start',
              justifyContent: 'space-between',
              gap: 1,
            }}
          >
            <Typography variant="h5" sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
              {mode === 'view' && (calendarEvent?.title ?? '')}
              {mode === 'edit' && t('components.calendarEventForm.title.edit')}
              {mode === 'create' && t('components.calendarEventForm.title.new')}
            </Typography>
            {mode === 'view' && calendarEvent && (
              <SubscriptionToggle
                subscribed={calendarEvent.isSubscribed}
                onToggle={form.toggleSubscription}
              />
            )}
          </DialogTitle>

          <Divider sx={{ my: 1 }} />

          <DialogContent>
            {form.loading ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
                <CircularProgress />
              </Box>
            ) : readOnly ? (
              calendarEvent && (
                <CalendarEventDetails
                  event={calendarEvent}
                  occurrence={occurrence}
                  onReply={form.reply}
                />
              )
            ) : (
              <CalendarEventFields
                values={form.values}
                errors={form.errors}
                onChange={form.changeValues}
                onClearError={form.clearError}
                autoFocus={mode === 'create'}
                categories={form.categories}
                isOwner={isOwner}
                ownerId={calendarEvent?.owner.id ?? currentUserId}
                publicCalendarEnabled={publicCalendarEnabled}
              />
            )}

            {form.conflict && (
              <Alert
                severity="warning"
                sx={{ mt: 2 }}
                action={
                  <Button color="inherit" size="small" onClick={form.reloadAfterConflict}>
                    {t('components.calendarEventForm.conflict.reload')}
                  </Button>
                }
              >
                {t('components.calendarEventForm.conflict.message')}
              </Alert>
            )}

            {form.apiError && (
              <Alert severity="error" onClose={form.dismissApiError} sx={{ mt: 2 }}>
                {form.apiError}
              </Alert>
            )}
          </DialogContent>

          <CalendarEventDialogActions
            readOnly={readOnly}
            calendarEvent={calendarEvent}
            hasOccurrence={!!occurrence}
            saving={form.saving}
            onDelete={form.deleteEvent}
            onDeleteOccurrence={form.deleteOccurrence}
            onCancel={form.cancel}
            onEdit={form.startEdit}
          />
        </form>
      </Dialog>
    </>
  );
}
