import { FC, useEffect, useRef } from 'react';
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  LinearProgress,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import BarChartIcon from '@mui/icons-material/BarChart';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import RefreshIcon from '@mui/icons-material/Refresh';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import SendIcon from '@mui/icons-material/Send';
import { EmailState, SendProgressProps, StatCardProps } from '../types';
import { useTranslation } from 'react-i18next';

const IdleView: FC = () => {
  const { t } = useTranslation();

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 2,
        py: 10,
        color: 'text.secondary',
      }}
    >
      <BarChartIcon sx={{ fontSize: 64, color: 'action.disabled' }} />
      <Typography variant="h5" fontWeight={600} color="text.primary">
        {t('pages.mail.sendProgressPage.idle.title')}
      </Typography>
      <Typography variant="body1">{t('pages.mail.sendProgressPage.idle.subtitle')}</Typography>
    </Box>
  );
};

const StatCard: FC<StatCardProps> = ({ label, value, color, icon }) => (
  <Card
    variant="outlined"
    sx={{
      flex: 1,
      borderColor: `${color}.main`,
      borderWidth: 2,
      borderRadius: 2,
    }}
  >
    <CardContent>
      <Stack alignItems="center" spacing={0.5}>
        <Box sx={{ color: `${color}.main` }}>{icon}</Box>
        <Typography variant="h4" fontWeight={700} color={`${color}.main`}>
          {value}
        </Typography>
        <Typography variant="caption" color="text.secondary" fontWeight={500}>
          {label}
        </Typography>
      </Stack>
    </CardContent>
  </Card>
);

const SendProgress: FC<SendProgressProps> = ({
  sendState,
  progress,
  processed,
  total,
  logEntries,
  summary,
  onReset,
}) => {
  const { t } = useTranslation();
  const logEndRef = useRef<HTMLDivElement>(null);
  const isDone = sendState === EmailState.DONE;

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [logEntries]);

  if (sendState === EmailState.IDLE) {
    return (
      <Box
        sx={{
          borderRadius: 3,
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
          height: '100%',
          padding: 1,
        }}
      >
        <IdleView />
      </Box>
    );
  }

  return (
    <Box
      sx={{
        borderRadius: 3,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        padding: 1,
      }}
    >
      <Box
        sx={{
          px: 3,
          py: 2,
          bgcolor: isDone ? 'success.main' : 'primary.main',
          color: 'white',
          transition: 'background-color 0.4s ease',
        }}
      >
        <Stack direction="row" alignItems="center" justifyContent="space-between">
          <Stack direction="row" alignItems="center" spacing={1.5}>
            {isDone ? (
              <CheckCircleIcon />
            ) : (
              <HourglassEmptyIcon
                sx={{
                  animation: 'spin 2s linear infinite',
                  '@keyframes spin': {
                    from: { transform: 'rotate(0deg)' },
                    to: { transform: 'rotate(360deg)' },
                  },
                }}
              />
            )}
            <Typography variant="h6" fontWeight={700}>
              {isDone
                ? t('pages.mail.sendProgressPage.header.done')
                : t('pages.mail.sendProgressPage.header.sending')}
            </Typography>
          </Stack>

          <Chip
            label={`${processed} / ${total}`}
            size="small"
            icon={<SendIcon style={{ color: 'white' }} />}
            sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white', fontWeight: 700 }}
          />
        </Stack>
      </Box>

      <Box sx={{ px: 3, py: 3, display: 'flex', flexDirection: 'column', gap: 3 }}>
        <Box>
          <Stack direction="row" justifyContent="space-between" mb={0.5}>
            <Typography variant="body2" color="text.secondary">
              {t('pages.mail.sendProgressPage.progress.label')}
            </Typography>
            <Typography variant="body2" fontWeight={700} color="primary.main">
              {progress}%
            </Typography>
          </Stack>
          <LinearProgress
            variant="determinate"
            value={progress}
            color={isDone ? 'success' : 'primary'}
            sx={{ height: 10, borderRadius: 5 }}
          />
        </Box>
        {summary && (
          <>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <StatCard
                label={t('pages.mail.sendProgressPage.statCard.total')}
                value={summary.total}
                color="primary"
                icon={<SendIcon />}
              />
              <StatCard
                label={t('pages.mail.sendProgressPage.statCard.successful')}
                value={summary.successful}
                color="success"
                icon={<CheckCircleIcon />}
              />
              <StatCard
                label={t('pages.mail.sendProgressPage.statCard.failed')}
                value={summary.failed}
                color="error"
                icon={<ErrorIcon />}
              />
            </Stack>

            {summary.failed > 0 && (
              <Alert severity="error" variant="outlined" sx={{ borderRadius: 2 }}>
                <AlertTitle fontWeight={700}>
                  {t('pages.mail.sendProgressPage.failedAlert.title', { count: summary.failed })}
                </AlertTitle>
                <List dense disablePadding>
                  {summary.results
                    .filter((r) => !r.success)
                    .map((r, i) => (
                      <ListItem key={i} disablePadding sx={{ py: 0.25 }}>
                        <ListItemIcon sx={{ minWidth: 28 }}>
                          <ErrorIcon fontSize="small" color="error" />
                        </ListItemIcon>
                        <ListItemText
                          primary={
                            <Typography variant="body2" fontWeight={600}>
                              {r.email}
                            </Typography>
                          }
                          secondary={r.errorMessage}
                        />
                      </ListItem>
                    ))}
                </List>
              </Alert>
            )}
          </>
        )}

        <Divider />

        <Box>
          <Typography variant="subtitle1" fontWeight={700} mb={1}>
            {t('pages.mail.sendProgressPage.log.title')}
          </Typography>
          <Paper
            variant="outlined"
            sx={{
              maxHeight: 260,
              overflowY: 'auto',
              borderRadius: 2,
              bgcolor: 'grey.50',
              p: 1,
            }}
          >
            {logEntries.length === 0 && (
              <Typography variant="body2" color="text.disabled" sx={{ p: 1 }}>
                {t('pages.mail.sendProgressPage.log.waiting')}
              </Typography>
            )}

            <List dense disablePadding>
              {logEntries.map((entry, i) => (
                <ListItem
                  key={i}
                  disablePadding
                  sx={{
                    px: 1,
                    py: 0.25,
                    borderRadius: 1,
                    mb: 0.25,
                    bgcolor: entry.success ? 'success.50' : 'error.50',
                    border: '1px solid',
                    borderColor: entry.success ? 'success.200' : 'error.200',
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 28 }}>
                    {entry.success ? (
                      <CheckCircleIcon fontSize="small" color="success" />
                    ) : (
                      <ErrorIcon fontSize="small" color="error" />
                    )}
                  </ListItemIcon>
                  <ListItemText
                    primary={
                      <Typography variant="body2" fontWeight={600}>
                        {entry.email}
                      </Typography>
                    }
                    secondary={
                      !entry.success && entry.errorMessage ? (
                        <Typography variant="caption" color="error.main">
                          {entry.errorMessage}
                        </Typography>
                      ) : null
                    }
                  />
                </ListItem>
              ))}
            </List>

            <div ref={logEndRef} />
          </Paper>
        </Box>

        {isDone && (
          <Button
            variant="contained"
            color="primary"
            size="large"
            startIcon={<RefreshIcon />}
            onClick={onReset}
            sx={{ borderRadius: 2, alignSelf: 'center', px: 4 }}
          >
            {t('pages.mail.sendProgressPage.resetButton')}
          </Button>
        )}
      </Box>
    </Box>
  );
};

export default SendProgress;
