import { Chip, useTheme } from '@mui/material';
import { CalendarCategory } from '../types';

export function CalendarCategoryChips({
  categories,
  selectedIds,
  onToggle,
}: {
  categories: CalendarCategory[];
  selectedIds: string[];
  onToggle: (id: string) => void;
}) {
  const theme = useTheme();

  return categories.map((c) => {
    const selected = selectedIds.includes(c.id);
    return (
      <Chip
        key={c.id}
        label={c.name}
        onClick={() => onToggle(c.id)}
        variant={selected ? 'filled' : 'outlined'}
        sx={{
          borderColor: c.color,
          ...(selected && {
            bgcolor: c.color,
            color: theme.palette.getContrastText(c.color),
            '&:hover': { bgcolor: c.color },
          }),
        }}
      />
    );
  });
}
