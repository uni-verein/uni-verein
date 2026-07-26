import React, { useState, useMemo, useDeferredValue, useEffect, useRef } from 'react';
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
  useMediaQuery,
  useTheme,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import type { SvgIconComponent } from '@mui/icons-material';
import { DynamicIcon, loadAllIcons } from '../muiIcons';
import { useTranslation } from 'react-i18next';

// Rendering all ~8600 icons at once means mounting that many MUI IconButton/Tooltip
// instances, which blocks the main thread for over a second on every open. Capping the
// rendered tiles keeps the dialog responsive; searching narrows the list further.
const MAX_RENDERED_ICONS = 250;

interface IconPickerDialogProps {
  open: boolean;
  selectedIcon: string;
  onSelect: (iconName: string) => void;
  onClose: () => void;
}

export function IconPickerDialog({ open, selectedIcon, onSelect, onClose }: IconPickerDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [iconNames, setIconNames] = useState<string[]>([]);
  const [icons, setIcons] = useState<Record<string, SvgIconComponent>>({});
  const [isLoading, setIsLoading] = useState(true);
  const abortRef = useRef(false);

  const deferredSearch = useDeferredValue(search);

  useEffect(() => {
    if (!open) return;

    abortRef.current = false;
    setIsLoading(true);

    const load = async () => {
      try {
        const MuiIcons = await loadAllIcons();
        const names = Object.keys(MuiIcons).filter(
          (key) => !key.endsWith('Outlined') && key !== 'default',
        );

        if (!abortRef.current) {
          setIcons(MuiIcons);
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
  const visible = filtered.slice(0, MAX_RENDERED_ICONS);

  const handleClose = () => {
    setSearch('');
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      fullWidth={!isMobile}
      fullScreen={isMobile}
      maxWidth="md"
    >
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
            {filtered.length > MAX_RENDERED_ICONS &&
              `: ${t('components.iconPickerDialog.refineSearch')}`}
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
            visible.map((name) => {
              const isSelected = name === selectedIcon;
              const Icon = icons[name];
              if (!Icon) return null;
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
                    <Icon fontSize="small" />
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
