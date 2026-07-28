import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './i18n';
import { PageConfigProvider } from './components/PageConfigContext';
import { SnackbarProvider } from './components/SnackbarContext';
import { ThemeModeProvider } from './components/ThemeModeContext';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <ThemeModeProvider>
    <SnackbarProvider>
      <PageConfigProvider>
        <App />
      </PageConfigProvider>
    </SnackbarProvider>
  </ThemeModeProvider>,
);
