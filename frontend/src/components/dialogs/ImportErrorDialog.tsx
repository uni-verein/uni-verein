import React from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { useIndexedTranslation } from '../../hooks/useIndexedTranslation';

export function ImportErrorDialog({
  open,
  errors,
  onClose,
}: {
  open: boolean;
  errors: Array<{ translationKey: string; values: string[] }>;
  onClose: () => void;
}) {
  const { ti } = useIndexedTranslation();

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <ErrorOutlineIcon color="error" />
          <Typography variant="h6" fontWeight={700}>
            {ti('pages.backup.csvImport.importErrors.title')}
          </Typography>
        </Stack>
      </DialogTitle>

      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {ti('pages.backup.csvImport.importErrors.description')}
        </Typography>
        <List disablePadding>
          {errors.map((error, index) => (
            <React.Fragment key={index}>
              <ListItem alignItems="flex-start" sx={{ px: 0 }}>
                <ListItemIcon sx={{ minWidth: 32, mt: 0.5 }}>
                  <ErrorOutlineIcon color="error" fontSize="small" />
                </ListItemIcon>
                <ListItemText
                  primary={ti(`pages.backup.csvImport.${error.translationKey}`, error.values)}
                  primaryTypographyProps={{
                    variant: 'body2',
                  }}
                />
              </ListItem>
              {index < errors.length - 1 && <Divider component="li" />}
            </React.Fragment>
          ))}
        </List>
      </DialogContent>

      <DialogActions>
        <Button variant="contained" color="error" onClick={onClose}>
          {ti('pages.backup.csvImport.importErrors.closeButton')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
