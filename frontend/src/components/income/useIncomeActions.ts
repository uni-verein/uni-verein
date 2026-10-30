import { useCallback, useRef, useState } from 'react';
import { ButtonProps } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { api, apiFile } from '../../api';
import { IncomeRow } from '../../types';
import { useConfirm } from '../../hooks/useConfirm';
import { useSnackbar } from '../../hooks/useSnackbar';
import { incomeBasePath } from './incomeUtils';

export function useIncomeActions(reload: () => Promise<void>) {
  const { t } = useTranslation();
  const setIncomeChange = useSnackbar();
  const { open, confirm, handleClose } = useConfirm();
  const [confirmDialog, setConfirmDialog] = useState<{
    message: string;
    buttonText: string;
    confirmColor: ButtonProps['color'];
  }>({ message: '', buttonText: '', confirmColor: 'error' });

  const [paymentDateOpen, setPaymentDateOpen] = useState(false);
  const paymentDateResolveRef = useRef<((value: string | null) => void) | null>(null);

  const askPaymentDate = useCallback((): Promise<string | null> => {
    return new Promise((resolve) => {
      paymentDateResolveRef.current = resolve;
      setPaymentDateOpen(true);
    });
  }, []);

  const closePaymentDate = useCallback((value: string | null) => {
    setPaymentDateOpen(false);
    paymentDateResolveRef.current?.(value);
  }, []);

  const markAsPaid = async (row: IncomeRow, paid: boolean) => {
    let paymentDate: string | null = null;
    if (paid) {
      paymentDate = await askPaymentDate();
      if (paymentDate === null) return;
    }

    try {
      await api(`${incomeBasePath(row)}/${row.id}`, {
        method: 'POST',
        body: JSON.stringify({ paid, paymentDate }),
      });
    } catch {
      setIncomeChange({
        status: 'error',
        message: t('pages.income.responseMessages.paymentError'),
      });
    }
    await reload();
  };

  const remove = async (row: IncomeRow) => {
    setConfirmDialog({
      message: t('pages.income.confirmDialog.deleteQuestion'),
      buttonText: t('pages.income.confirmDialog.delete'),
      confirmColor: 'error',
    });
    if (!(await confirm())) return;
    try {
      await api(`${incomeBasePath(row)}/${row.id}`, { method: 'DELETE' });
      setIncomeChange({ status: 'success', message: t('pages.income.responseMessages.deleted') });
    } catch {
      setIncomeChange({ status: 'error', message: t('pages.income.responseMessages.deleteError') });
    }
    await reload();
  };

  const restore = async (row: IncomeRow) => {
    setConfirmDialog({
      message: t('pages.income.confirmDialog.restoreQuestion'),
      buttonText: t('pages.income.confirmDialog.restore'),
      confirmColor: 'success',
    });
    if (!(await confirm())) return;
    try {
      await api(`${incomeBasePath(row)}/${row.id}/restore`, { method: 'POST' });
      setIncomeChange({ status: 'success', message: t('pages.income.responseMessages.restored') });
    } catch {
      setIncomeChange({
        status: 'error',
        message: t('pages.income.responseMessages.restoreError'),
      });
    }
    await reload();
  };

  const issueAcknowledgement = async (row: IncomeRow) => {
    try {
      const response = await apiFile('/acknowledgements', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ source: row.source, id: row.id }),
      });
      if (!response.ok) throw new Error('API Error');

      const filename =
        /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(
          response.headers.get('content-disposition') ?? '',
        )?.[1] ?? 'Quittung.pdf';
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement('a');
      link.href = url;
      link.download = decodeURIComponent(filename);
      link.click();
      URL.revokeObjectURL(url);

      setIncomeChange({
        status: 'success',
        message: t('pages.income.responseMessages.acknowledgementCreated'),
      });
      await reload();
    } catch {
      setIncomeChange({
        status: 'error',
        message: t('pages.income.responseMessages.acknowledgementError'),
      });
    }
  };

  return {
    markAsPaid,
    remove,
    restore,
    issueAcknowledgement,
    confirmDialogProps: { open, onClose: handleClose, ...confirmDialog },
    paymentDateDialogProps: { open: paymentDateOpen, onClose: closePaymentDate },
  };
}
