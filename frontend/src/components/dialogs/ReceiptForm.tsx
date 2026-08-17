import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  IconButton,
  MenuItem,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import CloseIcon from '@mui/icons-material/Close';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import PhotoCameraIcon from '@mui/icons-material/PhotoCamera';
import DeleteIcon from '@mui/icons-material/Delete';
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import 'dayjs/locale/de';
import { api, apiFile } from '../../api';
import { Receipt, ReceiptCategory, ReceiptPaymentMethod, Role } from '../../types';
import { useTranslation } from 'react-i18next';
import { compressImageFile } from '../../utils/imageProcessing';
import { isPdfFile, renderPdfFirstPageToBlob } from '../../utils/pdfProcessing';
import { runReceiptOcr } from '../../utils/receiptOcr';
import { useIsPwaInstalled } from '../../hooks/useIsPwaInstalled';

interface PreviewFile {
  url: string;
  name: string;
  isPdf: boolean;
  originalUrl?: string;
}

interface PendingFile {
  blob: Blob;
  name: string;
  isPdf: boolean;
  previewBlob: Blob;
}

export default function ReceiptForm({
  mode,
  receipt,
  categories,
  role,
  onClose,
  onSaved,
}: {
  mode: 'create' | 'view' | 'edit';
  receipt: Receipt | null;
  categories: ReceiptCategory[];
  role: Role | string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const isPwaInstalled = useIsPwaInstalled();
  const isPrivileged = role === Role.ADMIN || role === Role.FINANCIAL_MANAGER;
  const readOnly = mode === 'view' || (mode === 'edit' && !!receipt?.paid);

  const [amount, setAmount] = useState<string>(receipt ? receipt.amount.toString() : '');
  const [amountTouched, setAmountTouched] = useState(!!receipt);
  const [receiptDate, setReceiptDate] = useState<Date | null>(
    receipt ? new Date(receipt.receiptDate) : new Date(),
  );
  const [receiptDateTouched, setReceiptDateTouched] = useState(!!receipt);
  const [categoryId, setCategoryId] = useState<string>(receipt?.categoryId?.toString() ?? '');
  const [vendor, setVendor] = useState(receipt?.vendor ?? '');
  const [description, setDescription] = useState(receipt?.description ?? '');
  const [paymentMethod, setPaymentMethod] = useState<string>(receipt?.paymentMethod ?? '');
  const [files, setFiles] = useState<PendingFile[]>([]);
  const [newPreviews, setNewPreviews] = useState<PreviewFile[]>([]);
  const [existingPreviews, setExistingPreviews] = useState<PreviewFile[]>([]);
  const [errors, setErrors] = useState<{ amount?: string; receiptDate?: string }>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [processingFiles, setProcessingFiles] = useState(false);
  const [lightboxFile, setLightboxFile] = useState<PreviewFile | null>(null);

  useEffect(() => {
    const urls = files.map((f) => ({
      url: URL.createObjectURL(f.previewBlob),
      name: f.name,
      isPdf: f.isPdf,
      originalUrl: f.isPdf ? URL.createObjectURL(f.blob) : undefined,
    }));
    setNewPreviews(urls);

    return () =>
      urls.forEach((u) => {
        URL.revokeObjectURL(u.url);
        if (u.originalUrl) URL.revokeObjectURL(u.originalUrl);
      });
  }, [files]);

  useEffect(() => {
    if (mode === 'create' || !receipt) return;
    const objectUrls: string[] = [];

    const loadFiles = async () => {
      const loaded: PreviewFile[] = [];
      for (const file of receipt.files) {
        const res = await apiFile(`/receipts/${receipt.id}/files/${file.id}`);
        if (res.ok) {
          const blob = await res.blob();
          const isPdf = file.contentType === 'application/pdf';
          const previewBlob = isPdf ? await renderPdfFirstPageToBlob(blob) : blob;
          const url = URL.createObjectURL(previewBlob);
          const originalUrl = isPdf ? URL.createObjectURL(blob) : undefined;
          objectUrls.push(url);

          if (originalUrl) objectUrls.push(originalUrl);
          loaded.push({ url, name: `${file.position + 1}`, isPdf, originalUrl });
        }
      }
      setExistingPreviews(loaded);
    };

    loadFiles();
    return () => objectUrls.forEach((u) => URL.revokeObjectURL(u));
  }, [mode, receipt]);

  const validate = () => {
    const newErrors: { amount?: string; receiptDate?: string } = {};
    const numericAmount = parseFloat(amount.replace(',', '.'));

    if (!amount || isNaN(numericAmount) || numericAmount <= 0) {
      newErrors.amount = t('components.receiptForm.validation.amountInvalid');
    }

    if (!receiptDate) {
      newErrors.receiptDate = t('components.receiptForm.validation.receiptDateRequired');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleFilesSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(e.target.files ?? []);
    e.target.value = '';
    if (selected.length === 0) return;

    setProcessingFiles(true);
    try {
      for (const file of selected) {
        const pdf = isPdfFile(file);
        const uploadBlob: Blob = pdf ? file : await compressImageFile(file);
        const previewBlob = pdf ? await renderPdfFirstPageToBlob(uploadBlob) : uploadBlob;
        setFiles((prev) => [
          ...prev,
          { blob: uploadBlob, name: file.name, isPdf: pdf, previewBlob },
        ]);

        if (!amountTouched || !receiptDateTouched) {
          const ocrResult = await runReceiptOcr(previewBlob);
          if (!amountTouched && ocrResult.amount !== undefined) {
            setAmount(ocrResult.amount.toFixed(2).replace('.', ','));
          }
          if (!receiptDateTouched && ocrResult.date !== undefined) {
            setReceiptDate(ocrResult.date);
          }
        }
      }
    } finally {
      setProcessingFiles(false);
    }
  };

  const removeFile = (index: number) => {
    setFiles((prev) => prev.filter((_, i) => i !== index));
  };

  const save = async (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!validate()) return;

    setSaving(true);
    setApiError(null);
    try {
      if (mode === 'edit' && receipt) {
        await api(`/receipts/${receipt.id}`, {
          method: 'PATCH',
          body: JSON.stringify({
            amount: parseFloat(amount.replace(',', '.')),
            receiptDate: (receiptDate as Date).toISOString(),
            categoryId: categoryId || null,
            vendor: vendor || null,
            description: description || null,
            ...(isPrivileged ? { paymentMethod: paymentMethod || null } : {}),
          }),
        });
        onSaved();
        return;
      }

      const formData = new FormData();
      formData.append('amount', amount.replace(',', '.'));
      formData.append('receiptDate', (receiptDate as Date).toISOString());

      if (categoryId) formData.append('categoryId', categoryId.toString());
      if (vendor) formData.append('vendor', vendor);
      if (description) formData.append('description', description);
      if (isPrivileged && paymentMethod) formData.append('paymentMethod', paymentMethod);
      files.forEach((file) => formData.append('files', file.blob, file.name));

      const res = await apiFile('/receipts', { method: 'POST', body: formData });
      if (!res.ok) {
        setApiError(t('components.receiptForm.alerts.saveFailed'));
      } else {
        onSaved();
      }
    } catch {
      setApiError(t('components.receiptForm.alerts.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Dialog
        open={true}
        onClose={onClose}
        fullWidth={!isMobile}
        fullScreen={isMobile}
        maxWidth="sm"
        slotProps={{ paper: { sx: { borderRadius: isMobile ? 0 : 3, p: 1 } } }}
      >
        <form onSubmit={save}>
          <DialogTitle>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              {mode === 'view' && t('components.receiptForm.title.view')}
              {mode === 'edit' && t('components.receiptForm.title.edit')}
              {mode === 'create' && t('components.receiptForm.title.new')}
            </Typography>
          </DialogTitle>

          <Divider sx={{ my: 1 }} />

          <DialogContent>
            <Grid container spacing={2}>
              {mode !== 'create' && receipt && (
                <Grid size={12}>
                  <TextField
                    fullWidth
                    disabled
                    label={t('components.receiptForm.fields.submittedBy')}
                    value={receipt.userName ?? t('pages.receipts.deletedUser')}
                  />
                </Grid>
              )}
              <Grid size={6}>
                <TextField
                  fullWidth
                  disabled={readOnly}
                  label={t('components.receiptForm.fields.amount')}
                  value={amount}
                  onChange={(e) => {
                    setAmount(e.target.value);
                    setAmountTouched(true);
                    setErrors({ ...errors, amount: undefined });
                  }}
                  required
                  error={!!errors.amount}
                  helperText={errors.amount}
                  slotProps={{ input: { endAdornment: '€' } }}
                />
              </Grid>
              <Grid size={6}>
                <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                  <DatePicker
                    label={t('components.receiptForm.fields.receiptDate')}
                    format="DD.MM.YYYY"
                    value={receiptDate ? dayjs(receiptDate) : null}
                    onChange={(newValue) => {
                      setReceiptDate(newValue ? newValue.toDate() : null);
                      setReceiptDateTouched(true);
                      setErrors({ ...errors, receiptDate: undefined });
                    }}
                    disabled={readOnly}
                    slotProps={{
                      textField: {
                        fullWidth: true,
                        required: true,
                        error: !!errors.receiptDate,
                        helperText: errors.receiptDate,
                      },
                    }}
                  />
                </LocalizationProvider>
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  disabled={readOnly}
                  select
                  label={t('components.receiptForm.fields.category')}
                  value={categoryId}
                  onChange={(e) => setCategoryId(e.target.value)}
                >
                  <MenuItem value="">{t('components.receiptForm.fields.noCategory')}</MenuItem>
                  {categories.map((c) => (
                    <MenuItem key={c.id.toString()} value={c.id.toString()}>
                      {c.name}
                    </MenuItem>
                  ))}
                </TextField>
              </Grid>
              {(isPrivileged || mode === 'view') && (
                <Grid size={6}>
                  <TextField
                    fullWidth
                    disabled={readOnly || !isPrivileged}
                    select
                    label={t('components.receiptForm.fields.paymentMethod')}
                    value={paymentMethod}
                    onChange={(e) => setPaymentMethod(e.target.value)}
                  >
                    <MenuItem value="">
                      {t('components.receiptForm.fields.noPaymentMethod')}
                    </MenuItem>
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
                </Grid>
              )}
              <Grid size={6}>
                <TextField
                  fullWidth
                  disabled={readOnly}
                  label={t('components.receiptForm.fields.vendor')}
                  value={vendor}
                  onChange={(e) => setVendor(e.target.value)}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  disabled={readOnly}
                  label={t('components.receiptForm.fields.description')}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </Grid>
            </Grid>

            <Divider sx={{ my: 2 }} />

            <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
              {t('components.receiptForm.fields.files')}
            </Typography>

            {mode === 'create' && (
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, mb: 2 }}>
                <Button variant="outlined" component="label" startIcon={<UploadFileIcon />}>
                  {t('components.receiptForm.buttons.addFiles')}
                  <input
                    type="file"
                    hidden
                    multiple
                    accept="image/*,application/pdf"
                    onChange={handleFilesSelected}
                  />
                </Button>
                {isPwaInstalled && (
                  <Button variant="outlined" component="label" startIcon={<PhotoCameraIcon />}>
                    {t('components.receiptForm.buttons.useCamera')}
                    <input
                      type="file"
                      hidden
                      accept="image/*"
                      capture="environment"
                      onChange={handleFilesSelected}
                    />
                  </Button>
                )}
                {processingFiles && (
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <CircularProgress size={20} />
                    <Typography variant="body2" color="text.secondary">
                      {t('components.receiptForm.fields.scanning')}
                    </Typography>
                  </Box>
                )}
              </Box>
            )}

            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              {(mode === 'create' ? newPreviews : existingPreviews).map((preview, index) => (
                <Box
                  key={preview.url}
                  onClick={() => setLightboxFile(preview)}
                  sx={{
                    position: 'relative',
                    width: 96,
                    height: 96,
                    borderRadius: 1,
                    overflow: 'hidden',
                    border: '1px solid',
                    borderColor: 'divider',
                    cursor: 'pointer',
                  }}
                >
                  <Box
                    component="img"
                    src={preview.url}
                    alt={preview.name}
                    sx={{ width: '100%', height: '100%', objectFit: 'cover' }}
                  />
                  {preview.isPdf && (
                    <PictureAsPdfIcon
                      fontSize="small"
                      sx={{
                        position: 'absolute',
                        top: 2,
                        left: 2,
                        color: 'common.white',
                        filter: 'drop-shadow(0 0 2px rgba(0,0,0,0.7))',
                      }}
                    />
                  )}
                  {mode === 'create' && (
                    <Chip
                      size="small"
                      icon={<DeleteIcon fontSize="small" />}
                      onClick={(e) => {
                        e.stopPropagation();
                        removeFile(index);
                      }}
                      sx={{ position: 'absolute', bottom: 2, right: 2 }}
                      label={t('components.receiptForm.buttons.remove')}
                    />
                  )}
                </Box>
              ))}
              {(mode === 'create' ? newPreviews : existingPreviews).length === 0 && (
                <Typography variant="body2" color="text.secondary">
                  {t('components.receiptForm.fields.noFiles')}
                </Typography>
              )}
            </Box>

            {apiError && (
              <Alert severity="error" onClose={() => setApiError(null)} sx={{ mt: 2 }}>
                {apiError}
              </Alert>
            )}
          </DialogContent>

          <DialogActions sx={{ p: 3, gap: 1 }}>
            <Button onClick={onClose} color="inherit" startIcon={<CloseIcon />}>
              {readOnly
                ? t('components.receiptForm.buttons.close')
                : t('components.receiptForm.buttons.cancel')}
            </Button>
            {!readOnly && (
              <Button
                type="submit"
                variant="contained"
                color="primary"
                startIcon={<SaveIcon />}
                disabled={saving}
              >
                {t('components.receiptForm.buttons.save')}
              </Button>
            )}
          </DialogActions>
        </form>
      </Dialog>

      <Dialog
        open={!!lightboxFile}
        onClose={() => setLightboxFile(null)}
        maxWidth="lg"
        fullScreen={isMobile}
      >
        <DialogTitle sx={{ display: 'flex', justifyContent: 'flex-end', p: 1 }}>
          <IconButton
            onClick={() => setLightboxFile(null)}
            aria-label={t('components.receiptForm.buttons.close')}
          >
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent sx={{ pt: 0 }}>
          <Box
            component="img"
            src={lightboxFile?.url}
            alt={lightboxFile?.name}
            sx={{
              maxWidth: '100%',
              maxHeight: '85vh',
              objectFit: 'contain',
              display: 'block',
              mx: 'auto',
            }}
          />
          {lightboxFile?.isPdf && lightboxFile.originalUrl && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
              <Button
                variant="outlined"
                startIcon={<PictureAsPdfIcon />}
                href={lightboxFile.originalUrl}
                target="_blank"
                rel="noopener"
              >
                {t('components.receiptForm.buttons.openPdf')}
              </Button>
            </Box>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
