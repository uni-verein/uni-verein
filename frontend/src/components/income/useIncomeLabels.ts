import { useMemo } from 'react';
import { useTheme } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { UUIDTypes } from 'uuid';
import { IncomeCategory, IncomeKind, IncomeRow } from '../../types';

export function useIncomeLabels(categories: IncomeCategory[]) {
  const theme = useTheme();
  const { t } = useTranslation();

  const chipColors = useMemo(() => {
    const palette = [
      theme.palette.primary.main,
      theme.palette.secondary.main,
      theme.palette.success.main,
      theme.palette.warning.main,
      theme.palette.info.main,
      theme.palette.error.main,
    ];
    const keys: string[] = [IncomeKind.MEMBERSHIP, ...categories.map((c) => c.id.toString())];
    return (kind: IncomeKind, categoryId: UUIDTypes | null) => {
      const key = kind === IncomeKind.CATEGORY && categoryId ? categoryId.toString() : kind;
      const index = keys.indexOf(key);
      return palette[(index < 0 ? keys.length : index) % palette.length];
    };
  }, [theme, categories]);

  const kindLabel = (row: { kind: IncomeKind; categoryName: string | null }) =>
    row.kind === IncomeKind.CATEGORY
      ? (row.categoryName ?? '')
      : t(`pages.income.kind.${row.kind}`);

  const sourceLabel = (row: IncomeRow) =>
    [
      t(`pages.income.source.${row.source}`),
      row.memberNumber ? `#${row.memberNumber}` : null,
      row.membershipStatus ? t(`components.membershipStatusOptions.${row.membershipStatus}`) : null,
      row.acknowledgementNumber
        ? t('pages.income.acknowledgementNumber', { number: row.acknowledgementNumber })
        : null,
    ]
      .filter(Boolean)
      .join(' · ');

  return { chipColors, kindLabel, sourceLabel };
}

export type IncomeLabels = ReturnType<typeof useIncomeLabels>;
