import { Alert, AlertColor, Snackbar } from '@mui/material';

interface CustomSnackbarProps {
  status: null | AlertColor;
  message: string;
  onClose: () => void;
}

export const CustomSnackbar = ({ status, message, onClose }: CustomSnackbarProps) => (
  <Snackbar
    open={status !== null}
    autoHideDuration={4000}
    onClose={(_, reason) => {
      if (reason === 'clickaway') return;
      onClose();
    }}
    anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
  >
    <Alert severity={(status as AlertColor) || 'info'} variant="filled" sx={{ width: '100%' }}>
      {message}
    </Alert>
  </Snackbar>
);
