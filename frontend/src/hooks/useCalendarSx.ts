import { useMemo } from 'react';
import { alpha, useMediaQuery, useTheme } from '@mui/material';

export function useCalendarSx() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });

  return useMemo(
    () => ({
      '--fc-border-color': theme.palette.divider,
      '--fc-page-bg-color': theme.palette.background.paper,
      '--fc-neutral-bg-color': theme.palette.action.hover,
      '--fc-list-event-hover-bg-color': theme.palette.action.hover,
      '--fc-today-bg-color': alpha(theme.palette.primary.main, 0.08),
      '--fc-button-bg-color': theme.palette.primary.main,
      '--fc-button-border-color': theme.palette.primary.main,
      '--fc-button-hover-bg-color': theme.palette.primary.dark,
      '--fc-button-hover-border-color': theme.palette.primary.dark,
      '--fc-button-active-bg-color': theme.palette.primary.dark,
      '--fc-button-active-border-color': theme.palette.primary.dark,
      '--fc-button-text-color': theme.palette.primary.contrastText,
      '& .fc': { fontFamily: theme.typography.fontFamily },
      '& .fc .fc-toolbar-title': { fontSize: isMobile ? '1.1rem' : '1.4rem', fontWeight: 700 },
      '& .fc .fc-button': { textTransform: 'none', borderRadius: `${theme.shape.borderRadius}px` },
      '& .fc .fc-list-day-cushion': { bgcolor: 'action.hover' },
      '& .fc a': { color: 'inherit' },
      '& .fc-event': { cursor: 'pointer' },
    }),
    [theme, isMobile],
  );
}
