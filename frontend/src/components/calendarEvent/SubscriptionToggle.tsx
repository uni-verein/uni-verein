import { IconButton, Tooltip } from '@mui/material';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import NotificationsOffIcon from '@mui/icons-material/NotificationsOff';
import { useTranslation } from 'react-i18next';

export function SubscriptionToggle({
  subscribed,
  onToggle,
}: {
  subscribed: boolean;
  onToggle: () => void;
}) {
  const { t } = useTranslation();

  return (
    <Tooltip
      title={t(
        subscribed
          ? 'components.calendarEventForm.subscription.unsubscribe'
          : 'components.calendarEventForm.subscription.subscribe',
      )}
      arrow
    >
      <IconButton
        onClick={onToggle}
        color={subscribed ? 'primary' : 'default'}
        aria-pressed={subscribed}
        aria-label={t('components.calendarEventForm.subscription.label')}
      >
        {subscribed ? <NotificationsActiveIcon /> : <NotificationsOffIcon />}
      </IconButton>
    </Tooltip>
  );
}
