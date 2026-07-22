import { useState } from 'react';
import { Box, Card, CardContent, Chip, Collapse, IconButton, Typography } from '@mui/material';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp';
import { useTranslation } from 'react-i18next';
import { RedactedText } from './RedactedText';

const actionColors: Record<string, 'success' | 'error' | 'warning' | 'info'> = {
  CREATE: 'success',
  DELETE: 'error',
  UPDATE: 'warning',
  READ: 'info',
};

export function AuditLogCard({ l }: { l: any }) {
  const [open, setOpen] = useState(false);
  const { t } = useTranslation();

  const rawData = typeof l.data === 'object' ? JSON.stringify(l.data, null, 2) : l.data;

  return (
    <Card
      variant="outlined"
      onClick={() => setOpen((prev) => !prev)}
      sx={{ mb: 1.5, borderRadius: 2, cursor: 'pointer' }}
    >
      <CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <Box>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {l.userName}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {new Date(l.timestamp).toLocaleString()}
            </Typography>
          </Box>
          <Chip
            label={l.action}
            color={actionColors[l.action] ?? 'default'}
            size="small"
            variant="outlined"
          />
        </Box>

        <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 1 }}>
          <Typography variant="caption" color="text.secondary">
            {t('pages.audit.tableHeaders.entity')}
          </Typography>
          <Typography variant="body2">{l.entity}</Typography>
        </Box>

        <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 0.5 }}>
          <IconButton
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              setOpen((prev) => !prev);
            }}
          >
            {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </Box>

        <Collapse in={open} timeout="auto" unmountOnExit>
          <Box
            sx={{
              p: 1.5,
              backgroundColor: 'action.hover',
              borderRadius: 1,
              borderLeft: '3px solid',
              borderColor: 'primary.main',
            }}
          >
            <Typography variant="caption" color="text.secondary" fontWeight="bold">
              {t('pages.audit.dataLabel')}
            </Typography>
            <Typography
              component="pre"
              variant="body2"
              sx={{
                mt: 0.5,
                fontFamily: 'monospace',
                fontSize: '0.8rem',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-all',
                margin: 0,
              }}
            >
              <RedactedText text={rawData} />
            </Typography>
          </Box>
        </Collapse>
      </CardContent>
    </Card>
  );
}
