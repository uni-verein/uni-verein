import React, { ReactNode } from 'react';
import { Box, Card, CardContent, Typography } from '@mui/material';

export interface MobileListCardRow {
  label: string;
  value: ReactNode;
}

export function MobileListCard({
  primary,
  secondaryRows,
  actions,
  onClick,
}: {
  primary: ReactNode;
  secondaryRows?: MobileListCardRow[];
  actions?: ReactNode;
  onClick?: () => void;
}) {
  return (
    <Card
      variant="outlined"
      onClick={onClick}
      sx={{
        mb: 1.5,
        borderRadius: 2,
        cursor: onClick ? 'pointer' : 'default',
      }}
    >
      <CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          {primary}
        </Box>

        {secondaryRows && secondaryRows.length > 0 && (
          <Box sx={{ mt: 1 }}>
            {secondaryRows.map((row, index) => (
              <Box
                key={index}
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  gap: 2,
                  py: 0.5,
                  '&:not(:last-child)': { borderBottom: '1px dashed', borderColor: 'divider' },
                }}
              >
                <Typography variant="caption" color="text.secondary">
                  {row.label}
                </Typography>
                <Typography variant="body2" sx={{ textAlign: 'right' }}>
                  {row.value}
                </Typography>
              </Box>
            ))}
          </Box>
        )}

        {actions && (
          <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 1, gap: 0.5 }}>{actions}</Box>
        )}
      </CardContent>
    </Card>
  );
}
