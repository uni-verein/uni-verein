import { useState } from 'react';
import { Box, Chip, Collapse, IconButton, TableCell, TableRow, Typography } from '@mui/material';
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

export function AuditLogTableRow({ l, index }: { l: any; index: number }) {
  const [open, setOpen] = useState(false);
  const { t } = useTranslation();

  const rawData = typeof l.data === 'object' ? JSON.stringify(l.data, null, 2) : l.data;

  return (
    <>
      <TableRow
        onClick={() => setOpen((prev) => !prev)}
        sx={{
          backgroundColor: index % 2 === 0 ? 'inherit' : 'action.hover',
          '&:hover': { backgroundColor: 'action.selected' },
          cursor: 'pointer',
        }}
      >
        <TableCell padding="checkbox">
          <IconButton
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              setOpen((prev) => !prev);
            }}
          >
            {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
        <TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary' }}>
          {new Date(l.timestamp).toLocaleString()}
        </TableCell>
        <TableCell>{l.userName}</TableCell>
        <TableCell>
          <Chip
            label={l.action}
            color={actionColors[l.action] ?? 'default'}
            size="small"
            variant="outlined"
          />
        </TableCell>
        <TableCell>{l.entity}</TableCell>
        <TableCell
          sx={{
            maxWidth: 300,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            color: 'text.secondary',
            fontFamily: 'monospace',
            fontSize: '0.8rem',
          }}
        >
          <RedactedText text={rawData} />
        </TableCell>
      </TableRow>

      <TableRow>
        <TableCell colSpan={6} sx={{ py: 0, border: open ? undefined : 'none' }}>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <Box
              sx={{
                m: 1.5,
                p: 2,
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
        </TableCell>
      </TableRow>
    </>
  );
}
