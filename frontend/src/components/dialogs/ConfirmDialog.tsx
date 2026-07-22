import {
  Button,
  ButtonProps,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material';
import { useTranslation } from 'react-i18next';

interface Props {
  open: boolean;
  title?: string;
  message: string;
  buttonText?: string;
  onClose: (confirmed: boolean) => void;
  confirmColor?: ButtonProps['color'];
}

export function ConfirmDialog({
  open,
  title,
  message,
  buttonText,
  onClose,
  confirmColor = 'error',
}: Props) {
  const { t } = useTranslation();

  return (
    <Dialog open={open} onClose={() => onClose(false)}>
      <DialogTitle>{title ?? t('components.confirmDialog.confirm')}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onClose(false)}>{t('components.confirmDialog.cancel')}</Button>
        <Button onClick={() => onClose(true)} color={confirmColor} variant="contained">
          {buttonText ?? t('components.confirmDialog.delete')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
