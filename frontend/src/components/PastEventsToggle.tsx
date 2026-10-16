import { Box, Button } from '@mui/material';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { useTranslation } from 'react-i18next';

export function PastEventsToggle({
  showPast,
  count,
  onToggle,
}: {
  showPast: boolean;
  count: number;
  onToggle: () => void;
}) {
  const { t } = useTranslation();
  if (count === 0) return null;

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', mb: 1 }}>
      <Button
        size="small"
        onClick={onToggle}
        startIcon={showPast ? <ExpandLessIcon /> : <ExpandMoreIcon />}
      >
        {t(showPast ? 'pages.calendar.hidePast' : 'pages.calendar.showPast', { count })}
      </Button>
    </Box>
  );
}
