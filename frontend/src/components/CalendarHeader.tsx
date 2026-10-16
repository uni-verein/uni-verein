import React from 'react';
import { Box, Typography } from '@mui/material';

export function CalendarHeader({ title, actions }: { title: string; actions: React.ReactNode }) {
  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: 2,
        mb: 2,
      }}
    >
      <Typography variant="h5" fontWeight={700}>
        {title}
      </Typography>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>{actions}</Box>
    </Box>
  );
}
