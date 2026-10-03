import React from 'react';
import { Box, Button, Stack, Typography } from '@mui/material';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { useTranslation } from 'react-i18next';

type ErrorBoundaryProps = {
  children: React.ReactNode;
  resetKey?: unknown;
  fullScreen?: boolean;
};

type ErrorBoundaryState = {
  error: Error | null;
  resetKey: unknown;
};

export default class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null, resetKey: this.props.resetKey };

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return { error };
  }

  static getDerivedStateFromProps(
    props: ErrorBoundaryProps,
    state: ErrorBoundaryState,
  ): Partial<ErrorBoundaryState> | null {
    if (props.resetKey !== state.resetKey) {
      return { error: null, resetKey: props.resetKey };
    }
    return null;
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error('Unhandled render error:', error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <ErrorFallback
          fullScreen={this.props.fullScreen}
          onRetry={() => this.setState({ error: null })}
        />
      );
    }
    return this.props.children;
  }
}

function ErrorFallback({ fullScreen, onRetry }: { fullScreen?: boolean; onRetry: () => void }) {
  const { t } = useTranslation();

  return (
    <Box
      role="alert"
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        p: 4,
        minHeight: fullScreen ? '100vh' : 240,
      }}
    >
      <Stack spacing={2} alignItems="center" sx={{ maxWidth: 420 }}>
        <ErrorOutlineIcon color="error" sx={{ fontSize: 48 }} />
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {t('components.errorBoundary.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('components.errorBoundary.description')}
        </Typography>
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1}
          sx={{ width: { xs: '100%', sm: 'auto' } }}
        >
          {!fullScreen && (
            <Button variant="outlined" onClick={onRetry}>
              {t('components.errorBoundary.retry')}
            </Button>
          )}
          <Button variant="contained" onClick={() => window.location.reload()}>
            {t('components.errorBoundary.reload')}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
}
