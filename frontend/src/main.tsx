import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './i18n';
import { PageConfigProvider } from './components/PageConfigContext';
import { SnackbarProvider } from './components/SnackbarContext';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <SnackbarProvider>
    <PageConfigProvider>
      <App />
    </PageConfigProvider>
  </SnackbarProvider>,
);
