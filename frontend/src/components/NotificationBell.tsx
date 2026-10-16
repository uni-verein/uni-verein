import React, { useCallback, useEffect, useState } from 'react';
import {
  Badge,
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Popover,
  Tooltip,
  Typography,
} from '@mui/material';
import NotificationsIcon from '@mui/icons-material/Notifications';
import DeleteIcon from '@mui/icons-material/Delete';
import EventIcon from '@mui/icons-material/Event';
import EditCalendarIcon from '@mui/icons-material/EditCalendar';
import EventBusyIcon from '@mui/icons-material/EventBusy';
import HowToRegIcon from '@mui/icons-material/HowToReg';
import SystemUpdateIcon from '@mui/icons-material/SystemUpdate';
import AlarmIcon from '@mui/icons-material/Alarm';
import { useTranslation } from 'react-i18next';
import { api } from '../api';
import { AppNotification, NotificationType } from '../types';

const POLL_INTERVAL_MS = 60_000;

export interface FirmwareUpdateInfo {
  newFirmwareAvailable: boolean;
  currentVersion?: string;
  latestVersion?: string;
}

const icons: Record<NotificationType, React.ReactNode> = {
  [NotificationType.CALENDAR_INVITATION]: <EventIcon color="primary" />,
  [NotificationType.CALENDAR_EVENT_CHANGED]: <EditCalendarIcon color="info" />,
  [NotificationType.CALENDAR_EVENT_DELETED]: <EventBusyIcon color="error" />,
  [NotificationType.CALENDAR_RSVP]: <HowToRegIcon color="success" />,
  [NotificationType.CALENDAR_REMINDER]: <AlarmIcon color="warning" />,
};

export function NotificationBell({
  firmwareUpdate,
  onOpenEvent,
}: {
  firmwareUpdate: FirmwareUpdateInfo | null;
  onOpenEvent: (eventId: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [unread, setUnread] = useState(0);
  const [items, setItems] = useState<AppNotification[]>([]);
  const [loading, setLoading] = useState(false);

  const loadUnreadCount = useCallback(async () => {
    try {
      const data = await api('/notifications/unread-count');
      if (data) setUnread(data.unread);
    } catch {
      // best-effort: badge just keeps its last value
    }
  }, []);

  const loadNotifications = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api('/notifications?limit=20');
      if (data) {
        setItems(data.items);
        setUnread(data.unread);
      }
    } catch {
      // best-effort: list stays as it is
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadUnreadCount();
    const interval = window.setInterval(loadUnreadCount, POLL_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, [loadUnreadCount]);

  const open = (e: React.MouseEvent<HTMLElement>) => {
    setAnchor(e.currentTarget);
    loadNotifications();
  };

  const markRead = async (n: AppNotification) => {
    if (n.read) return;
    try {
      await api(`/notifications/${n.id}/read`, { method: 'POST' });
      setItems((list) => list.map((x) => (x.id === n.id ? { ...x, read: true } : x)));
      setUnread((u) => Math.max(0, u - 1));
    } catch {
      // ignore, the notification stays unread
    }
  };

  const markAllRead = async () => {
    try {
      await api('/notifications/read-all', { method: 'POST' });
      setItems((list) => list.map((x) => ({ ...x, read: true })));
      setUnread(0);
    } catch {
      // ignore
    }
  };

  const deleteNotification = async (n: AppNotification) => {
    try {
      await api(`/notifications/${n.id}`, { method: 'DELETE' });
      setItems((list) => list.filter((x) => x.id !== n.id));
      if (!n.read) setUnread((u) => Math.max(0, u - 1));
      loadNotifications();
    } catch {
      // ignore, the notification stays in the list
    }
  };

  const deleteRead = async () => {
    try {
      await api('/notifications/read', { method: 'DELETE' });
      setItems((list) => list.filter((x) => !x.read));
      loadNotifications();
    } catch {
      // ignore
    }
  };

  const locale = i18n.language.startsWith('de') ? 'de-DE' : 'en-GB';

  const formatReminderDate = (n: AppNotification) => {
    const allDay = n.details === 'allDay';
    return new Date(n.eventStart!).toLocaleString(locale, {
      weekday: 'short',
      day: '2-digit',
      month: '2-digit',
      ...(allDay ? { timeZone: 'UTC' } : { hour: '2-digit', minute: '2-digit' }),
    });
  };

  const describe = (n: AppNotification) => {
    const values = { actor: n.actorUsername, title: n.eventTitle };
    switch (n.type) {
      case NotificationType.CALENDAR_INVITATION:
        return t('components.notificationBell.invitation', values);
      case NotificationType.CALENDAR_EVENT_CHANGED: {
        const fields = n.details
          .split(',')
          .filter(Boolean)
          .map((f) => t(`components.notificationBell.fields.${f}`, { defaultValue: f }))
          .join(', ');
        return t('components.notificationBell.changed', { ...values, fields });
      }
      case NotificationType.CALENDAR_EVENT_DELETED:
        return t('components.notificationBell.deleted', values);
      case NotificationType.CALENDAR_RSVP:
        return t(`components.notificationBell.rsvp.${n.details}`, values);
      case NotificationType.CALENDAR_REMINDER:
        return t('components.notificationBell.reminder', {
          ...values,
          date: n.eventStart ? formatReminderDate(n) : '',
        });
      default:
        return n.eventTitle;
    }
  };

  const showFirmware = !!firmwareUpdate?.newFirmwareAvailable;

  return (
    <>
      <Tooltip title={t('pages.dashboard.notifications.label')} arrow>
        <IconButton
          onClick={open}
          color="inherit"
          aria-label={t('pages.dashboard.notifications.label')}
        >
          <Badge
            color="error"
            badgeContent={unread > 0 ? unread : undefined}
            max={99}
            variant={unread === 0 && showFirmware ? 'dot' : 'standard'}
            invisible={unread === 0 && !showFirmware}
          >
            <NotificationsIcon />
          </Badge>
        </IconButton>
      </Tooltip>
      <Popover
        open={Boolean(anchor)}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 380, maxWidth: 'calc(100vw - 32px)' } } }}
      >
        <Box
          sx={{
            px: 2,
            py: 1.5,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            {t('pages.dashboard.notifications.label')}
          </Typography>
          <Box sx={{ display: 'flex', gap: 0.5 }}>
            {unread > 0 && (
              <Button size="small" onClick={markAllRead}>
                {t('components.notificationBell.markAllRead')}
              </Button>
            )}
            {items.some((n) => n.read) && (
              <Button size="small" color="error" onClick={deleteRead}>
                {t('components.notificationBell.deleteRead')}
              </Button>
            )}
          </Box>
        </Box>
        <Divider />

        {showFirmware && (
          <Box sx={{ display: 'flex', gap: 1.5, px: 2, py: 1.5 }}>
            <SystemUpdateIcon color="warning" />
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                {t('pages.dashboard.notifications.firmwareUpdateTitle')}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t('pages.dashboard.notifications.firmwareUpdateText', {
                  version: firmwareUpdate?.latestVersion,
                  currentVersion: firmwareUpdate?.currentVersion,
                })}
              </Typography>
            </Box>
          </Box>
        )}

        {loading && items.length === 0 ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
            <CircularProgress size={24} />
          </Box>
        ) : items.length === 0 && !showFirmware ? (
          <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
            {t('pages.dashboard.notifications.noNotifications')}
          </Typography>
        ) : (
          <List dense disablePadding sx={{ maxHeight: 420, overflowY: 'auto' }}>
            {items.map((n) => (
              <ListItem
                key={n.id}
                disablePadding
                secondaryAction={
                  <Tooltip title={t('components.notificationBell.delete')} arrow>
                    <IconButton
                      edge="end"
                      size="small"
                      aria-label={t('components.notificationBell.delete')}
                      onClick={() => deleteNotification(n)}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                }
              >
                <ListItemButton
                  onClick={() => {
                    markRead(n);
                    if (n.eventId && n.type !== NotificationType.CALENDAR_EVENT_DELETED) {
                      setAnchor(null);
                      onOpenEvent(n.eventId);
                    }
                  }}
                  sx={{
                    alignItems: 'flex-start',
                    pr: 7,
                    bgcolor: n.read ? undefined : 'action.selected',
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 40, mt: 0.5 }}>{icons[n.type]}</ListItemIcon>
                  <ListItemText
                    primary={describe(n)}
                    secondary={new Date(n.createdAt).toLocaleString(locale, {
                      day: '2-digit',
                      month: '2-digit',
                      year: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                    slotProps={{
                      primary: {
                        sx: { fontWeight: n.read ? 400 : 600, overflowWrap: 'anywhere' },
                      },
                    }}
                  />
                </ListItemButton>
              </ListItem>
            ))}
          </List>
        )}
      </Popover>
    </>
  );
}
