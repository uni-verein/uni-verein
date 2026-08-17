import React, { useEffect, useState } from 'react';
import {
  Box,
  CircularProgress,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Typography,
} from '@mui/material';
import { BarChart } from '@mui/x-charts/BarChart';
import type { BarItem, BarLabelContext } from '@mui/x-charts/BarChart';
import { PieChart } from '@mui/x-charts/PieChart';
import { useDrawingArea, useXScale, useYScale } from '@mui/x-charts/hooks';
import { api } from '../api';
import { useTranslation } from 'react-i18next';

interface ReceiptCategoryTotal {
  categoryId: string | null;
  categoryName: string | null;
  total: number;
}

interface ReceiptAnalyticsData {
  year: number;
  byYear: { year: number; total: number; byCategory: ReceiptCategoryTotal[] }[];
  byMonth: { month: number; total: number; byCategory: ReceiptCategoryTotal[] }[];
  byCategory: ReceiptCategoryTotal[];
}

const GOLDEN_ANGLE = 137.508;

function categoryColorForIndex(index: number): string {
  const hue = (index * GOLDEN_ANGLE) % 360;
  const saturation = 62 + (index % 3) * 8;
  const lightness = 50 + (index % 2) * 8;
  return `hsl(${hue.toFixed(1)}, ${saturation}%, ${lightness}%)`;
}

const NO_CATEGORY_KEY = 'none';

function categoryKey(categoryId: string | null): string {
  return categoryId ?? NO_CATEGORY_KEY;
}

function formatCurrency(value: number): string {
  return value.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' });
}

// Renders the period total in the donut hole of the category pie chart.
function PieCenterLabel({ children }: { children: React.ReactNode }) {
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

// Hides the label on stacked bar segments too thin to legibly hold a currency amount, instead
// of letting overlapping text pile up for many small categories.
function stackedBarLabel(item: BarItem, context: BarLabelContext): string | null {
  if (!item.value) return null;
  if (context.bar.height < 16) return null;
  return formatCurrency(item.value);
}

// Draws each year's grand total (the sum of its stacked category segments) just above the
// corresponding bar, instead of a single total for the whole chart.
function YearTotalLabels({ years, totals }: { years: string[]; totals: number[] }) {
  const xScale = useXScale<'band'>();
  const yScale = useYScale<'linear'>();
  if (!('bandwidth' in xScale)) return null;

  return (
    <>
      {years.map((yearLabel, i) => {
        const x = (xScale(yearLabel) ?? 0) + xScale.bandwidth() / 2;
        const y = yScale(totals[i]) - 8;
        return (
          <text
            key={yearLabel}
            x={x}
            y={y}
            textAnchor="middle"
            style={{ fontSize: 13, fontWeight: 600, fill: 'currentColor' }}
          >
            {formatCurrency(totals[i])}
          </text>
        );
      })}
    </>
  );
}

export default function ReceiptAnalytics() {
  const { t } = useTranslation();
  const [data, setData] = useState<ReceiptAnalyticsData | null>(null);
  const [year, setYear] = useState<number>(new Date().getFullYear());
  const [loading, setLoading] = useState(false);

  const load = async (selectedYear: number) => {
    setLoading(true);
    try {
      const result = await api(`/receipts/analytics?year=${selectedYear}`);
      setData(result);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load(year);
  }, [year]);

  const monthLabels = [
    t('pages.receiptAnalytics.months.jan'),
    t('pages.receiptAnalytics.months.feb'),
    t('pages.receiptAnalytics.months.mar'),
    t('pages.receiptAnalytics.months.apr'),
    t('pages.receiptAnalytics.months.may'),
    t('pages.receiptAnalytics.months.jun'),
    t('pages.receiptAnalytics.months.jul'),
    t('pages.receiptAnalytics.months.aug'),
    t('pages.receiptAnalytics.months.sep'),
    t('pages.receiptAnalytics.months.oct'),
    t('pages.receiptAnalytics.months.nov'),
    t('pages.receiptAnalytics.months.dec'),
  ];

  const noCategoryLabel = t('pages.receiptAnalytics.noCategory');

  const categoryNames = new Map<string, string>();
  const collectCategories = (items: ReceiptCategoryTotal[] | undefined) => {
    items?.forEach((c) => {
      const key = categoryKey(c.categoryId);
      if (!categoryNames.has(key)) categoryNames.set(key, c.categoryName ?? noCategoryLabel);
    });
  };
  collectCategories(data?.byCategory);
  data?.byYear.forEach((y) => collectCategories(y.byCategory));

  const categoryOrder = Array.from(categoryNames.keys()).sort((a, b) => {
    if (a === NO_CATEGORY_KEY) return 1;
    if (b === NO_CATEGORY_KEY) return -1;
    return categoryNames.get(a)!.localeCompare(categoryNames.get(b)!);
  });
  const categoryColors = new Map<string, string>(
    categoryOrder.map((key, i) => [key, categoryColorForIndex(i)]),
  );

  // One stacked series per category, valued per month, restricted to categories actually
  // present in the selected year.
  const monthlyCategoryKeys = (data?.byCategory ?? [])
    .map((c) => categoryKey(c.categoryId))
    .sort((a, b) => categoryOrder.indexOf(a) - categoryOrder.indexOf(b));
  const monthlyCategorySeries = monthlyCategoryKeys.map((key) => ({
    data: Array.from({ length: 12 }, (_, i) => {
      const month = i + 1;
      const monthEntry = data?.byMonth.find((m) => m.month === month);
      return monthEntry?.byCategory.find((c) => categoryKey(c.categoryId) === key)?.total ?? 0;
    }),
    label: categoryNames.get(key) ?? noCategoryLabel,
    color: categoryColors.get(key),
    stack: 'total',
  }));

  // One stacked series per category (across all years this time), valued per year.
  const yearlyCategorySeries = categoryOrder.map((key) => ({
    data: (data?.byYear ?? []).map(
      (y) => y.byCategory.find((c) => categoryKey(c.categoryId) === key)?.total ?? 0,
    ),
    label: categoryNames.get(key) ?? noCategoryLabel,
    color: categoryColors.get(key),
    stack: 'total',
    barLabel: stackedBarLabel,
  }));

  // Total for the currently selected year, shown next to the month/category card titles.
  const selectedYearTotal = (data?.byCategory ?? []).reduce((sum, c) => sum + c.total, 0);

  const availableYears = data?.byYear.map((y) => y.year) ?? [];
  if (!availableYears.includes(year)) availableYears.push(year);
  availableYears.sort((a, b) => b - a);

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>
          {t('pages.receiptAnalytics.title')}
        </Typography>
        <FormControl sx={{ minWidth: 120 }}>
          <InputLabel id="receipt-analytics-year-label">
            {t('pages.receiptAnalytics.year')}
          </InputLabel>
          <Select
            labelId="receipt-analytics-year-label"
            value={year}
            label={t('pages.receiptAnalytics.year')}
            onChange={(e) => setYear(Number(e.target.value))}
          >
            {availableYears.map((y) => (
              <MenuItem key={y} value={y}>
                {y}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {loading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {!loading && data && (
        <Grid container spacing={3} alignItems="stretch">
          <Grid size={{ xs: 12, md: 7 }}>
            <Paper variant="outlined" sx={{ p: 2, borderRadius: 2, height: '100%' }}>
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'baseline',
                  mb: 1,
                }}
              >
                <Typography variant="subtitle1" fontWeight={600}>
                  {t('pages.receiptAnalytics.byMonth', { year })}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t('pages.receiptAnalytics.total')}: {formatCurrency(selectedYearTotal)}
                </Typography>
              </Box>
              <BarChart
                height={320}
                xAxis={[{ scaleType: 'band', data: monthLabels }]}
                series={monthlyCategorySeries}
                localeText={{ noData: t('pages.receiptAnalytics.noData') }}
              />
            </Paper>
          </Grid>
          <Grid size={{ xs: 12, md: 5 }}>
            <Paper variant="outlined" sx={{ p: 2, borderRadius: 2, height: '100%' }}>
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'baseline',
                  mb: 1,
                }}
              >
                <Typography variant="subtitle1" fontWeight={600}>
                  {t('pages.receiptAnalytics.byCategory', { year })}
                </Typography>
                {data.byCategory.length > 0 && (
                  <Typography variant="body2" color="text.secondary">
                    {t('pages.receiptAnalytics.total')}: {formatCurrency(selectedYearTotal)}
                  </Typography>
                )}
              </Box>
              {data.byCategory.length === 0 ? (
                <Box
                  sx={{
                    height: 320,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <Typography variant="body2" color="text.secondary">
                    {t('pages.receiptAnalytics.noData')}
                  </Typography>
                </Box>
              ) : (
                <PieChart
                  height={320}
                  series={[
                    {
                      data: data.byCategory.map((c) => ({
                        id: categoryKey(c.categoryId),
                        value: c.total,
                        label: c.categoryName ?? noCategoryLabel,
                        color: categoryColors.get(categoryKey(c.categoryId)),
                      })),
                      innerRadius: 40,
                      arcLabel: (item) => formatCurrency(item.value),
                      arcLabelMinAngle: 15,
                    },
                  ]}
                >
                  <PieCenterLabel>{formatCurrency(selectedYearTotal)}</PieCenterLabel>
                </PieChart>
              )}
            </Paper>
          </Grid>
          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
              <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
                {t('pages.receiptAnalytics.byYear')}
              </Typography>
              <BarChart
                height={280}
                xAxis={[{ scaleType: 'band', data: data.byYear.map((y) => y.year.toString()) }]}
                series={yearlyCategorySeries}
                margin={{ top: 30 }}
                localeText={{ noData: t('pages.receiptAnalytics.noData') }}
              >
                <YearTotalLabels
                  years={data.byYear.map((y) => y.year.toString())}
                  totals={data.byYear.map((y) => y.total)}
                />
              </BarChart>
            </Paper>
          </Grid>
        </Grid>
      )}
    </Box>
  );
}
