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
  const [selfEnrollmentNotificationEnabled, setSelfEnrollmentNotificationEnabled] = useState(false);
  const [savingReceipt, setSavingReceipt] = useState(false);
  const [savingSelfEnrollment, setSavingSelfEnrollment] = useState(false);
  // Calendar mails are opt-out, so a missing setting means enabled.
  const [calendarSettings, setCalendarSettings] = useState<Record<string, boolean>>({
    [UserSettingType.CALENDAR_INVITE_EMAIL]: true,
    [UserSettingType.CALENDAR_CHANGE_EMAIL]: true,
    [UserSettingType.CALENDAR_REMINDER_EMAIL]: true,
  });
  const [savingCalendar, setSavingCalendar] = useState<string | null>(null);

  useEffect(() => {
    const loadSettings = async () => {
      try {
        const settings: UserSetting[] = await api('/users/account/settings');
        if (role === Role.FINANCIAL_MANAGER) {
          const receiptSetting = settings.find(
            (s) => s.type === UserSettingType.RECEIPT_NOTIFICATION,
          );
          setReceiptNotificationEnabled(receiptSetting?.enabled ?? true);
        }
        const selfEnrollmentSetting = settings.find(
          (s) => s.type === UserSettingType.SELF_ENROLLMENT_NOTIFICATION,
        );
        setSelfEnrollmentNotificationEnabled(selfEnrollmentSetting?.enabled ?? false);
        setCalendarSettings((current) =>
          Object.fromEntries(
            Object.keys(current).map((type) => [
              type,
              settings.find((s) => s.type === type)?.enabled ?? true,
            ]),
          ),
        );
      } catch {
        setSnackbar({
          status: 'error',
          message: t('pages.userManagement.notificationSettings.loadFailed'),
        });
      }
    };
    loadSettings().catch();
  }, [role, setSnackbar, t]);

  const handleReceiptToggle = async (checked: boolean) => {
    setSavingReceipt(true);
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
      setSavingReceipt(false);
    }
  };

  const handleSelfEnrollmentToggle = async (checked: boolean) => {
    setSavingSelfEnrollment(true);
    setSelfEnrollmentNotificationEnabled(checked);
    try {
      await api(`/users/account/settings/${UserSettingType.SELF_ENROLLMENT_NOTIFICATION}`, {
        method: 'PUT',
        body: JSON.stringify({ enabled: checked }),
      });
      setSnackbar({
        status: 'success',
        message: t('pages.userManagement.notificationSettings.saveSuccess'),
      });
    } catch {
      setSelfEnrollmentNotificationEnabled(!checked);
      setSnackbar({
        status: 'error',
        message: t('pages.userManagement.notificationSettings.saveFailed'),
      });
    } finally {
      setSavingSelfEnrollment(false);
    }
  };

  const handleCalendarToggle = async (type: UserSettingType, checked: boolean) => {
    setSavingCalendar(type);
    setCalendarSettings((current) => ({ ...current, [type]: checked }));
    try {
      await api(`/users/account/settings/${type}`, {
        method: 'PUT',
        body: JSON.stringify({ enabled: checked }),
      });
      setSnackbar({
        status: 'success',
        message: t('pages.userManagement.notificationSettings.saveSuccess'),
      });
    } catch {
      setCalendarSettings((current) => ({ ...current, [type]: !checked }));
      setSnackbar({
        status: 'error',
        message: t('pages.userManagement.notificationSettings.saveFailed'),
      });
    } finally {
      setSavingCalendar(null);
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
          {role === Role.FINANCIAL_MANAGER && (
            <TableRow hover>
              <TableCell>
                {t('pages.userManagement.notificationSettings.receiptNotification')}
              </TableCell>
              <TableCell align="right">
                <Switch
                  checked={receiptNotificationEnabled}
                  disabled={savingReceipt}
                  onChange={(e) => handleReceiptToggle(e.target.checked)}
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
          )}
          <TableRow hover>
            <TableCell>
              {t('pages.userManagement.notificationSettings.selfEnrollmentNotification')}
            </TableCell>
            <TableCell align="right">
              <Switch
                checked={selfEnrollmentNotificationEnabled}
                disabled={savingSelfEnrollment}
                onChange={(e) => handleSelfEnrollmentToggle(e.target.checked)}
                slotProps={{
                  input: {
                    role: 'switch',
                    'aria-label': t(
                      'pages.userManagement.notificationSettings.selfEnrollmentNotification',
                    ),
                  },
                }}
              />
            </TableCell>
          </TableRow>
          {[
            {
              type: UserSettingType.CALENDAR_INVITE_EMAIL,
              label: t('pages.userManagement.notificationSettings.calendarInviteEmail'),
            },
            {
              type: UserSettingType.CALENDAR_CHANGE_EMAIL,
              label: t('pages.userManagement.notificationSettings.calendarChangeEmail'),
            },
            {
              type: UserSettingType.CALENDAR_REMINDER_EMAIL,
              label: t('pages.userManagement.notificationSettings.calendarReminderEmail'),
            },
          ].map(({ type, label }) => (
            <TableRow hover key={type}>
              <TableCell>{label}</TableCell>
              <TableCell align="right">
                <Switch
                  checked={calendarSettings[type] ?? true}
                  disabled={savingCalendar === type}
                  onChange={(e) => handleCalendarToggle(type, e.target.checked)}
                  slotProps={{ input: { role: 'switch', 'aria-label': label } }}
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
