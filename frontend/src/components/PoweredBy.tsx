import { Link, SxProps, Theme, Typography } from '@mui/material';

export function PoweredBy({ sx }: { sx?: SxProps<Theme> }) {
  return (
    <Typography
      variant="caption"
      color="text.secondary"
      component="p"
      sx={[{ textAlign: 'center', m: 0 }, ...(Array.isArray(sx) ? sx : [sx])]}
    >
      Powered by{' '}
      <Link
        href="https://uni-verein.de"
        target="_blank"
        rel="noopener noreferrer"
        underline="hover"
        color="inherit"
        sx={{ fontWeight: 600 }}
      >
        Uni-Verein
      </Link>
    </Typography>
  );
}
