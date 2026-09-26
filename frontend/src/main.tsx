import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './i18n';
import { PageConfigProvider } from './components/PageConfigContext';
import { SnackbarProvider } from './components/SnackbarContext';
import { ThemeModeProvider } from './components/ThemeModeContext';

if ('serviceWorker' in navigator) {
  const hadController = !!navigator.serviceWorker.controller;
  let reloadingAfterUpdate = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (!hadController || reloadingAfterUpdate) return;
    reloadingAfterUpdate = true;
    window.location.reload();
  });
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <ThemeModeProvider>
    <SnackbarProvider>
      <PageConfigProvider>
        <App />
      </PageConfigProvider>
    </SnackbarProvider>
  </ThemeModeProvider>,
);
