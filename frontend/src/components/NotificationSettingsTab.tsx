import React, { useEffect, useState } from 'react';
import {
  Paper,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { Role, UserSetting, UserSettingType } from '../types';
import { useSnackbar } from '../hooks/useSnackbar';

export function NotificationSettingsTab({ role }: { role?: Role | string }) {
  const { t } = useTranslation();
  const setSnackbar = useSnackbar();
  const [receiptNotificationEnabled, setReceiptNotificationEnabled] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (role !== Role.FINANCIAL_MANAGER) return;

    const loadSettings = async () => {
      try {
        const settings: UserSetting[] = await api('/users/account/settings');
        const setting = settings.find((s) => s.type === UserSettingType.RECEIPT_NOTIFICATION);
        setReceiptNotificationEnabled(setting?.enabled ?? true);
      } catch {
        setSnackbar({
          status: 'error',
          message: t('pages.userManagement.notificationSettings.loadFailed'),
        });
      }
    };
    loadSettings().catch();
  }, [role, setSnackbar, t]);

  const handleToggle = async (checked: boolean) => {
    setSaving(true);
    setReceiptNotificationEnabled(checked);
    try {
      await api(`/users/account/settings/${UserSettingType.RECEIPT_NOTIFICATION}`, {
        method: 'PUT',
        body: JSON.stringify({ enabled: checked }),
      });
      setSnackbar({
        status: 'success',
        message: t('pages.userManagement.notificationSettings.saveSuccess'),
      });
    } catch {
      setReceiptNotificationEnabled(!checked);
      setSnackbar({
        status: 'error',
        message: t('pages.userManagement.notificationSettings.saveFailed'),
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
      <Table>
        <TableHead sx={{ bgcolor: 'action.hover' }}>
          <TableRow>
            <TableCell colSpan={2} sx={{ fontWeight: 700 }}>
              {t('pages.userManagement.notificationSettings.title')}
            </TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {role === Role.FINANCIAL_MANAGER ? (
            <TableRow hover>
              <TableCell>
                {t('pages.userManagement.notificationSettings.receiptNotification')}
              </TableCell>
              <TableCell align="right">
                <Switch
                  checked={receiptNotificationEnabled}
                  disabled={saving}
                  onChange={(e) => handleToggle(e.target.checked)}
                  slotProps={{
                    input: {
                      role: 'switch',
                      'aria-label': t(
                        'pages.userManagement.notificationSettings.receiptNotification',
                      ),
                    },
                  }}
                />
              </TableCell>
            </TableRow>
          ) : (
            <TableRow>
              <TableCell colSpan={2} sx={{ color: 'text.secondary' }}>
                {t('pages.userManagement.notificationSettings.noSettingsAvailable')}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
