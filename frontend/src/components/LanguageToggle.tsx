import { Button, IconButton, SxProps, Theme, Tooltip, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

export function LanguageToggle({
  isMobile,
  floating = false,
}: {
  isMobile: boolean;
  floating?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const label = t('pages.dashboard.switchLanguage');
  const toggle = () => i18n.changeLanguage(i18n.resolvedLanguage === 'de' ? 'en' : 'de');
  const floatingSx: SxProps<Theme> | undefined = floating
    ? { bgcolor: 'background.paper', boxShadow: 2, '&:hover': { bgcolor: 'background.paper' } }
    : undefined;

  if (isMobile) {
    return (
      <Tooltip title={label} arrow>
        <IconButton color="inherit" onClick={toggle} aria-label={label} sx={floatingSx}>
          <Typography sx={{ fontSize: '1.2rem', lineHeight: 1 }}>
            {i18n.resolvedLanguage === 'de' ? '🇩🇪' : '🇬🇧'}
          </Typography>
        </IconButton>
      </Tooltip>
    );
  }

  return (
    <Button onClick={toggle} color="inherit" sx={floatingSx}>
      {i18n.resolvedLanguage === 'de' ? '🇩🇪 Deutsch' : '🇬🇧 English'}
    </Button>
  );
}
