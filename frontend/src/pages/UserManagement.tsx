import React, { useState } from 'react';
import { Box, Paper, Tab, Tabs, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { UserManagementProps } from '../types';
import { ProfileSettingsTab } from '../components/ProfileSettingsTab';
import { NotificationSettingsTab } from '../components/NotificationSettingsTab';

export default function UserManagement({ userId, accountView, role }: UserManagementProps) {
  const [activeTab, setActiveTab] = useState<'profile' | 'notifications'>('profile');
  const { t } = useTranslation();

  return (
    <Box>
      <Box
        sx={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 2, mb: 3 }}
      >
        <Typography variant="h5" fontWeight={700}>
          {t('pages.userManagement.title')}
        </Typography>
      </Box>

      {accountView && (
        <Paper variant="outlined" sx={{ mb: 3, borderRadius: 2 }}>
          <Tabs
            value={activeTab}
            onChange={(_, newValue) => setActiveTab(newValue)}
            sx={{ borderBottom: '1px solid', borderColor: 'divider' }}
          >
            <Tab value="profile" label={t('pages.userManagement.tabs.profile')} />
            <Tab value="notifications" label={t('pages.userManagement.tabs.notifications')} />
          </Tabs>
        </Paper>
      )}

      {(!accountView || activeTab === 'profile') && (
        <ProfileSettingsTab userId={userId} accountView={accountView} />
      )}

      {accountView && activeTab === 'notifications' && <NotificationSettingsTab role={role} />}
    </Box>
  );
}
