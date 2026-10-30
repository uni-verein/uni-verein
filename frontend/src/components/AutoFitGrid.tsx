import { ReactNode } from 'react';
import { Box, SxProps, Theme } from '@mui/material';

export function AutoFitGrid({
  children,
  minColumnWidth = 240,
  sx,
}: {
  children: ReactNode;
  minColumnWidth?: number;
  sx?: SxProps<Theme>;
}) {
  return (
    <Box
      sx={[
        {
          display: 'grid',
          gridTemplateColumns: `repeat(auto-fit, minmax(min(${minColumnWidth}px, 100%), 1fr))`,
          gap: 2,
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
    >
      {children}
    </Box>
  );
}
