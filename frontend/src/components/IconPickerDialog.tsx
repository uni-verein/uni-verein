import React, {
  useState,
  useMemo,
  useDeferredValue,
  useEffect,
  useRef,
} from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  InputAdornment,
  TextField,
  Tooltip,
  Typography,
  IconButton,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { DynamicIcon } from './muiIcons';
import { useTranslation } from 'react-i18next';

interface IconPickerDialogProps {
  open: boolean;
  selectedIcon: string;
  onSelect: (iconName: string) => void;
  onClose: () => void;
}

export function IconPickerDialog({ open, selectedIcon, onSelect, onClose }: IconPickerDialogProps) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [iconNames, setIconNames] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const abortRef = useRef(false);

  const deferredSearch = useDeferredValue(search);

  useEffect(() => {
    if (!open) return;

    abortRef.current = false;
    setIsLoading(true);

    const load = async () => {
      try {
        const { loadIconNames } = await import('./muiIcons');
        const names = await loadIconNames();

        if (!abortRef.current) {
          setIconNames(names);
        }
      } catch (error) {
        if (!abortRef.current) {
          console.error('Failed to load icons:', error);
        }
      } finally {
        if (!abortRef.current) {
          setIsLoading(false);
        }
      }
    };

    load();

    return () => {
      abortRef.current = true;
    };
  }, [open]);

  const filtered = useMemo(
    () => iconNames.filter((name) => name.toLowerCase().includes(deferredSearch.toLowerCase())),
    [iconNames, deferredSearch],
  );

  const handleClose = () => {
    setSearch('');
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="md">
      <DialogTitle>{t('components.iconPickerDialog.selectIcon')}</DialogTitle>

      <DialogContent>
        <TextField
          autoFocus
          fullWidth
          placeholder={t('components.iconPickerDialog.searchIcon')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          disabled={isLoading}
          sx={{ mb: 2, mt: 1 }}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon />
                </InputAdornment>
              ),
            },
          }}
        />

        {!isLoading && (
          <Typography variant="caption" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
            {filtered.length} {t('components.iconPickerDialog.foundIcons')}
          </Typography>
        )}
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: isLoading ? '1fr' : 'repeat(auto-fill, minmax(56px, 1fr))',
            gap: 0.5,
            maxHeight: 400,
            minHeight: 200,
            overflowY: 'auto',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 1,
            p: 1,
            ...(isLoading && {
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }),
          }}
        >
          {isLoading ? (
            <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2 }}>
              <CircularProgress size={40} />
              <Typography variant="body2" color="text.secondary">
                {t('components.iconPickerDialog.loadingIcons')}
              </Typography>
            </Box>
          ) : (
            filtered.map((name) => {
              const isSelected = name === selectedIcon;
              return (
                <Tooltip key={name} title={name} placement="top" arrow>
                  <IconButton
                    onClick={() => onSelect(name)}
                    size="small"
                    sx={{
                      borderRadius: 1,
                      border: '2px solid',
                      borderColor: isSelected ? 'primary.main' : 'transparent',
                      bgcolor: isSelected ? 'primary.50' : 'transparent',
                      color: isSelected ? 'primary.main' : 'action.active',
                      '&:hover': {
                        borderColor: 'primary.light',
                        bgcolor: 'primary.50',
                      },
                    }}
                  >
                    <DynamicIcon name={name} fontSize="small" />
                  </IconButton>
                </Tooltip>
              );
            })
          )}
        </Box>
      </DialogContent>

      <DialogActions sx={{ p: 2 }}>
        {selectedIcon && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mr: 'auto' }}>
            <DynamicIcon name={selectedIcon} color="primary" />
            <Typography variant="body2" color="primary">
              {selectedIcon}
            </Typography>
          </Box>
        )}
        <Button onClick={handleClose}>{t('components.iconPickerDialog.buttons.cancel')}</Button>
        <Button variant="contained" onClick={handleClose} disabled={!selectedIcon}>
          {t('components.iconPickerDialog.buttons.accept')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
