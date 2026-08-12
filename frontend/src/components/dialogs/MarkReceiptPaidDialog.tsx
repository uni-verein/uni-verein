import { useState } from 'react';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  TextField,
} from '@mui/material';
import EuroIcon from '@mui/icons-material/Euro';
import { useTranslation } from 'react-i18next';
import { api } from '../../api';
import { Receipt, ReceiptPaymentMethod } from '../../types';

export function MarkReceiptPaidDialog({
  receipt,
  onClose,
  onSaved,
}: {
  receipt: Receipt;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useTranslation();
  const [paymentMethod, setPaymentMethod] = useState<string>(receipt.paymentMethod ?? '');
  const [saving, setSaving] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const methodAlreadySet = !!receipt.paymentMethod;

  const confirm = async () => {
    setSaving(true);
    setApiError(null);
    try {
      const result = await api(`/receipts/${receipt.id}/pay`, {
        method: 'POST',
        body: JSON.stringify({
          paymentMethod: methodAlreadySet ? null : paymentMethod || null,
        }),
      });
      if (result === 409) {
        setApiError(t('components.markReceiptPaidDialog.alreadyPaid'));
        return;
      }
      onSaved();
    } catch (e) {
      setApiError(t('components.markReceiptPaidDialog.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={true} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>{t('components.markReceiptPaidDialog.title')}</DialogTitle>
      <DialogContent>
        <TextField
          fullWidth
          select
          sx={{ mt: 1 }}
          disabled={methodAlreadySet}
          required
          label={t('components.receiptForm.fields.paymentMethod')}
          value={paymentMethod}
          onChange={(e) => setPaymentMethod(e.target.value)}
          helperText={
            methodAlreadySet ? undefined : t('components.markReceiptPaidDialog.paymentMethodRequired')
          }
        >
          <MenuItem value={ReceiptPaymentMethod.CASH}>
            {t('components.receiptForm.fields.paymentMethodOptions.cash')}
          </MenuItem>
          <MenuItem value={ReceiptPaymentMethod.BANK_TRANSFER}>
            {t('components.receiptForm.fields.paymentMethodOptions.bankTransfer')}
          </MenuItem>
          <MenuItem value={ReceiptPaymentMethod.CARD}>
            {t('components.receiptForm.fields.paymentMethodOptions.card')}
          </MenuItem>
          <MenuItem value={ReceiptPaymentMethod.SEPA_DIRECT_DEBIT}>
            {t('components.receiptForm.fields.paymentMethodOptions.sepaDirectDebit')}
          </MenuItem>
          <MenuItem value={ReceiptPaymentMethod.PAYPAL}>
            {t('components.receiptForm.fields.paymentMethodOptions.paypal')}
          </MenuItem>
          <MenuItem value={ReceiptPaymentMethod.OTHER}>
            {t('components.receiptForm.fields.paymentMethodOptions.other')}
          </MenuItem>
        </TextField>

        {apiError && (
          <Alert severity="error" onClose={() => setApiError(null)} sx={{ mt: 2 }}>
            {apiError}
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ p: 3, gap: 1 }}>
        <Button onClick={onClose} color="inherit">
          {t('components.markReceiptPaidDialog.cancel')}
        </Button>
        <Button
          variant="contained"
          color="success"
          startIcon={<EuroIcon />}
          disabled={saving || (!methodAlreadySet && !paymentMethod)}
          onClick={confirm}
        >
          {t('components.markReceiptPaidDialog.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
