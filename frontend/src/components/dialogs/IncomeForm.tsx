import { useState } from 'react';
import {
  Alert,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs, { Dayjs } from 'dayjs';
import 'dayjs/locale/de';
import { useTranslation } from 'react-i18next';
import { api } from '../../api';
import { IncomeCategory, IncomeRow } from '../../types';
import { MAX_AMOUNT } from '../../utils/moneyLimits';

export default function IncomeForm({
  income,
  categories,
  onClose,
  onSaved,
}: {
  income: IncomeRow | null;
  categories: IncomeCategory[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const isEdit = income !== null;

  const [categoryId, setCategoryId] = useState<string>(
    income?.categoryId?.toString() ?? categories[0]?.id.toString() ?? '',
  );
  const [payer, setPayer] = useState(income?.payer ?? '');
  const [amount, setAmount] = useState(income ? income.amount.toString() : '');
  const [incomeDate, setIncomeDate] = useState<Dayjs | null>(
    income ? dayjs(income.dueDate.substring(0, 10)) : dayjs(),
  );
  const [description, setDescription] = useState(income?.description ?? '');
  const [paid, setPaid] = useState(true);
  const [paymentDate, setPaymentDate] = useState<Dayjs | null>(null);
  const [errors, setErrors] = useState<{ payer?: string; amount?: string; incomeDate?: string }>(
    {},
  );
  const [apiError, setApiError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const validate = () => {
    const next: typeof errors = {};
    if (!payer.trim()) next.payer = t('components.incomeForm.validation.payerRequired');
    const parsed = parseFloat(amount.replace(',', '.'));
    if (!(parsed > 0)) next.amount = t('components.incomeForm.validation.amountPositive');
    else if (parsed > MAX_AMOUNT)
      next.amount = t('components.incomeForm.validation.amountTooLarge');
    if (!incomeDate || !incomeDate.isValid())
      next.incomeDate = t('components.incomeForm.validation.dateRequired');
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSave = async () => {
    if (!validate() || !incomeDate) return;
    const effectivePaymentDate = paymentDate ?? incomeDate;
    if (!isEdit && paid && !effectivePaymentDate.isValid()) return;

    const body = {
      categoryId,
      payer: payer.trim(),
      description: description.trim() || null,
      amount: parseFloat(amount.replace(',', '.')),
      incomeDate: incomeDate.format('YYYY-MM-DD'),
    };

    setSaving(true);
    setApiError(null);
    try {
      if (isEdit) {
        await api(`/income/${income.id}`, { method: 'PUT', body: JSON.stringify(body) });
      } else {
        await api('/income', {
          method: 'POST',
          body: JSON.stringify({
            ...body,
            paid,
            paymentDate: paid ? effectivePaymentDate.format('YYYY-MM-DD') : undefined,
          }),
        });
      }
      onSaved();
    } catch {
      setApiError(t('components.incomeForm.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onClose={onClose} fullWidth={!isMobile} fullScreen={isMobile} maxWidth="sm">
      <DialogTitle>
        {t(isEdit ? 'components.incomeForm.titleEdit' : 'components.incomeForm.titleCreate')}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {apiError && <Alert severity="error">{apiError}</Alert>}
        {categories.length === 0 && (
          <Alert severity="info">{t('components.incomeForm.noCategories')}</Alert>
        )}
        <TextField
          select
          sx={{ mt: 1 }}
          fullWidth
          required
          disabled={categories.length === 0}
          label={t('components.incomeForm.fields.type')}
          value={categoryId}
          onChange={(e) => setCategoryId(e.target.value)}
        >
          {categories.map((c) => (
            <MenuItem key={c.id.toString()} value={c.id.toString()}>
              {c.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          fullWidth
          required
          label={t('components.incomeForm.fields.payer')}
          placeholder={t('components.incomeForm.fields.payerPlaceholder')}
          value={payer}
          error={errors.payer !== undefined}
          helperText={errors.payer}
          onChange={(e) => setPayer(e.target.value)}
        />
        <TextField
          fullWidth
          required
          type="number"
          label={t('components.incomeForm.fields.amount')}
          value={amount}
          error={errors.amount !== undefined}
          helperText={errors.amount}
          slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
          onChange={(e) => setAmount(e.target.value)}
        />
        <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
          <DatePicker
            label={t('components.incomeForm.fields.date')}
            format="DD.MM.YYYY"
            value={incomeDate}
            onChange={(v) => setIncomeDate(v)}
            slotProps={{
              textField: {
                fullWidth: true,
                required: true,
                error: errors.incomeDate !== undefined,
                helperText: errors.incomeDate,
              },
            }}
          />
        </LocalizationProvider>
        <TextField
          fullWidth
          multiline
          minRows={2}
          label={t('components.incomeForm.fields.description')}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
        {!isEdit && (
          <FormControlLabel
            control={<Checkbox checked={paid} onChange={(e) => setPaid(e.target.checked)} />}
            label={t('components.incomeForm.fields.alreadyReceived')}
          />
        )}
        {!isEdit && paid && (
          <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
            <DatePicker
              label={t('components.incomeForm.fields.paymentDate')}
              format="DD.MM.YYYY"
              value={paymentDate ?? incomeDate}
              onChange={(v) => setPaymentDate(v)}
              slotProps={{ textField: { fullWidth: true } }}
            />
          </LocalizationProvider>
        )}
      </DialogContent>
      <DialogActions sx={{ p: 3 }}>
        <Button onClick={onClose}>{t('components.incomeForm.cancel')}</Button>
        <Button variant="contained" onClick={handleSave} disabled={saving || !categoryId}>
          {t('components.incomeForm.save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
