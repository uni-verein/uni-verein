import { Box, Typography } from '@mui/material';
import { PieChart } from '@mui/x-charts/PieChart';
import { useDrawingArea } from '@mui/x-charts/hooks';
import { ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatEuro } from '../income/incomeUtils';
import { categoryLabel, FinanceCategorySummary } from './financeUtils';

function PieCenterLabel({ children }: { children: ReactNode }) {
  const { width, height, left, top } = useDrawingArea();
  return (
    <text
      x={left + width / 2}
      y={top + height / 2}
      textAnchor="middle"
      dominantBaseline="middle"
      style={{ fontSize: 15, fontWeight: 600, fill: 'currentColor' }}
    >
      {children}
    </text>
  );
}

const COMPACT_WIDTH = 440;

function useContainerWidth() {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState<number | null>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return { ref, width };
}

export function FinanceCategoryDonut({ categories }: { categories: FinanceCategorySummary[] }) {
  const { t } = useTranslation();
  const { ref, width } = useContainerWidth();
  const isCompact = width !== null && width < COMPACT_WIDTH;

  if (categories.length === 0) {
    return (
      <Box
        ref={ref}
        sx={{ height: 320, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
      >
        <Typography variant="body2" color="text.secondary">
          {t('pages.financeAnalytics.noData')}
        </Typography>
      </Box>
    );
  }

  const total = categories.reduce((sum, c) => sum + c.total, 0);

  const chart = (
    <PieChart
      height={isCompact ? 240 : 320}
      hideLegend={isCompact}
      series={[
        {
          data: categories.map((c) => ({
            id: c.key,
            value: c.total,
            label: categoryLabel(c, t),
            color: c.color,
          })),
          innerRadius: isCompact ? 55 : 40,
          valueFormatter: (item) => formatEuro(item.value),
          arcLabel: isCompact ? undefined : (item) => formatEuro(item.value),
          arcLabelMinAngle: 15,
        },
      ]}
    >
      <PieCenterLabel>{formatEuro(total)}</PieCenterLabel>
    </PieChart>
  );

  return (
    <Box ref={ref}>
      {chart}
      {isCompact && (
        <Box
          component="ul"
          sx={{ listStyle: 'none', m: 0, mt: 1, p: 0, display: 'grid', gap: 0.75 }}
        >
          {categories.map((c) => (
            <Box
              component="li"
              key={c.key}
              sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}
            >
              <Box
                sx={{ width: 12, height: 12, borderRadius: '50%', bgcolor: c.color, flexShrink: 0 }}
              />
              <Typography variant="body2" noWrap sx={{ flex: 1, minWidth: 0 }}>
                {categoryLabel(c, t)}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {formatEuro(c.total)}
              </Typography>
            </Box>
          ))}
        </Box>
      )}
    </Box>
  );
}
