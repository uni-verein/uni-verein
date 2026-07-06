import { Box, Divider, Paper, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import PersonIcon from '@mui/icons-material/Person';

export function DemoDialog() {
  const { t } = useTranslation();

  return (
    <Paper
      elevation={0}
      sx={{
        mt: 3,
        p: 2.5,
        borderRadius: 2,
        backgroundColor: 'warning.lighter',
        border: '1px solid',
        borderColor: 'warning.main',
        background: 'linear-gradient(135deg, #fff8e1 0%, #fff3cd 100%)',
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <WarningAmberIcon sx={{ color: 'warning.dark', fontSize: 22 }} />
        <Typography variant="h6" sx={{ color: 'warning.dark', fontWeight: 700 }}>
          {t('pages.login.demo.header')}
        </Typography>
      </Box>

      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
        {t('pages.login.demo.caption')}
      </Typography>

      <Divider sx={{ mb: 1.5, borderColor: 'warning.light' }} />

      <Typography
        variant="caption"
        sx={{
          color: 'text.secondary',
          textTransform: 'uppercase',
          fontWeight: 600,
          letterSpacing: 0.5,
        }}
      >
        {t('pages.login.demo.login')}
      </Typography>

      <Box sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 0.75 }}>
        {[
          { label: t('pages.login.demo.admin') },
          { label: t('pages.login.demo.user') },
          { label: t('pages.login.demo.finance') },
        ].map((item, index) => (
          <Box
            key={index}
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              px: 1.5,
              py: 0.75,
              borderRadius: 1,
              backgroundColor: 'rgba(255,255,255,0.7)',
              border: '1px solid',
              borderColor: 'rgba(237,108,2,0.2)',
            }}
          >
            <PersonIcon sx={{ fontSize: 16, color: 'warning.dark' }} />
            <Typography variant="body2" sx={{ color: 'text.primary', fontWeight: 500 }}>
              {item.label}
            </Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
