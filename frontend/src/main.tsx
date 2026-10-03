import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './i18n';
import { PageConfigProvider } from './components/PageConfigContext';
import { SnackbarProvider } from './components/SnackbarContext';
import { ThemeModeProvider } from './components/ThemeModeContext';
import ErrorBoundary from './components/ErrorBoundary';

if ('serviceWorker' in navigator) {
  const hadController = !!navigator.serviceWorker.controller;
  let reloadingAfterUpdate = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (!hadController || reloadingAfterUpdate) return;
    reloadingAfterUpdate = true;
    window.location.reload();
  });
}

const CHUNK_RELOAD_KEY = 'chunk-reload-at';
window.addEventListener('vite:preloadError', (event) => {
  try {
    const lastReload = Number(sessionStorage.getItem(CHUNK_RELOAD_KEY) ?? 0);
    if (Date.now() - lastReload < 60_000) return;
    sessionStorage.setItem(CHUNK_RELOAD_KEY, String(Date.now()));
  } catch {
    return;
  }
  event.preventDefault();
  window.location.reload();
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <ErrorBoundary fullScreen>
    <ThemeModeProvider>
      <SnackbarProvider>
        <PageConfigProvider>
          <App />
        </PageConfigProvider>
      </SnackbarProvider>
    </ThemeModeProvider>
  </ErrorBoundary>,
);
