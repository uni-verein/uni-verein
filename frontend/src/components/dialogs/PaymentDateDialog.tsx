import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material';
import EuroIcon from '@mui/icons-material/Euro';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs, { Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';

interface Props {
  open: boolean;
  onClose: (paymentDate: string | null) => void;
}

export function PaymentDateDialog({ open, onClose }: Props) {
  const { t } = useTranslation();
  const [paymentDate, setPaymentDate] = useState<Dayjs | null>(dayjs());

  const valid = paymentDate !== null && paymentDate.isValid();

  return (
    <Dialog
      open={open}
      onClose={() => onClose(null)}
      fullWidth
      maxWidth="xs"
      slotProps={{ transition: { onEnter: () => setPaymentDate(dayjs()) } }}
    >
      <DialogTitle>{t('components.paymentDateDialog.title')}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ mb: 2 }}>
          {t('components.paymentDateDialog.message')}
        </DialogContentText>
        <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
          <DatePicker
            label={t('components.paymentDateDialog.label')}
            format="DD.MM.YYYY"
            value={paymentDate}
            onChange={(v) => setPaymentDate(v)}
            slotProps={{ textField: { fullWidth: true, required: true } }}
          />
        </LocalizationProvider>
      </DialogContent>
      <DialogActions sx={{ p: 3, gap: 1 }}>
        <Button onClick={() => onClose(null)} color="inherit">
          {t('components.paymentDateDialog.cancel')}
        </Button>
        <Button
          variant="contained"
          color="success"
          startIcon={<EuroIcon />}
          disabled={!valid}
          onClick={() => paymentDate && onClose(paymentDate.format('YYYY-MM-DD'))}
        >
          {t('components.paymentDateDialog.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
