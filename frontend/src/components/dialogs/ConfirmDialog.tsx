import {
  Button,
  ButtonProps,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  TextField,
} from '@mui/material';
import { useTranslation } from 'react-i18next';

interface Props {
  open: boolean;
  title?: string;
  message: string;
  buttonText?: string;
  onClose: (confirmed: boolean) => void;
  confirmColor?: ButtonProps['color'];
  textFieldLabel?: string;
  textFieldValue?: string;
  onTextFieldChange?: (value: string) => void;
}

export function ConfirmDialog({
  open,
  title,
  message,
  buttonText,
  onClose,
  confirmColor = 'error',
  textFieldLabel,
  textFieldValue,
  onTextFieldChange,
}: Props) {
  const { t } = useTranslation();

  return (
    <Dialog open={open} onClose={() => onClose(false)} fullWidth maxWidth="xs">
      <DialogTitle>{title ?? t('components.confirmDialog.confirm')}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
        {onTextFieldChange && (
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={2}
            label={textFieldLabel}
            value={textFieldValue ?? ''}
            onChange={(e) => onTextFieldChange(e.target.value)}
            sx={{ mt: 2 }}
          />
        )}
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
