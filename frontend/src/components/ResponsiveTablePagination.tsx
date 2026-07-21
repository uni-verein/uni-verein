import React, { FC } from 'react';
import { TablePagination, TablePaginationProps, useMediaQuery, useTheme } from '@mui/material';

const ResponsiveTablePagination: FC<TablePaginationProps> = ({ sx, ...props }) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });

  return (
    <TablePagination
      {...props}
      sx={[
        {
          overflow: 'hidden',
          '& .MuiTablePagination-toolbar': {
            flexWrap: isMobile ? 'wrap' : 'nowrap',
            justifyContent: isMobile ? 'center' : 'flex-end',
            rowGap: 0.5,
            minHeight: 'auto',
            py: isMobile ? 1 : 0,
            px: isMobile ? 1 : 2,
          },
          '& .MuiTablePagination-spacer': {
            display: isMobile ? 'none' : 'block',
          },
          '& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows': {
            margin: 0,
          },
        },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
    />
  );
};

export default ResponsiveTablePagination;
