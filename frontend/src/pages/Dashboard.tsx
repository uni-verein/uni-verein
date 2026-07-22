import React, { useEffect, useState } from 'react';
import {
  Box,
  Drawer,
  AppBar,
  Toolbar,
  Typography,
  Divider,
  Button,
  CssBaseline,
  Container,
  Paper,
  Tooltip,
  IconButton,
  useMediaQuery,
  useTheme,
  Badge,
  Popover,
  BottomNavigation,
  BottomNavigationAction,
} from '@mui/material';
import PeopleIcon from '@mui/icons-material/People';
import NotificationsIcon from '@mui/icons-material/Notifications';
import EmailIcon from '@mui/icons-material/Email';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import EuroIcon from '@mui/icons-material/Euro';
import LogoutIcon from '@mui/icons-material/Logout';
import MenuIcon from '@mui/icons-material/Menu';

import Members from './Members';
import Mail from './Mail';
import Sepa from './Sepa';
import Contributions from './Contributions';
import UserManagement from './UserManagement';
import EmailConfig from './EmailConfig';
import ContributionPlanConfig from './ContributionPlanConfig';
import LinkConfig from './LinkConfig';
import { api } from '../api';
import Backup from './Backup';
import Audit from './Audit';
import { Role } from '../types';
import CreditorConfig from './CreditorConfig';
import GeneralConfig from './GeneralConfig';
import { useTranslation } from 'react-i18next';
import { UUIDTypes } from 'uuid';
import MemberCategoryConfig from './MemberCategoryConfig';
import { SidebarContent } from '../components/SidebarContent';

const drawerWidthExpanded = 280;
const drawerWidthCollapsed = 64;
const bottomNavHeight = 56;

export default function Dashboard({
  onLogout,
  pageName,
}: {
  onLogout?: () => void;
  pageName: string;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });

  const [page, setPage] = useState('members');
  const [user, setUser] = useState<{
    id: UUIDTypes | undefined;
    name: string;
    role: string | Role;
  }>({ id: undefined, name: '', role: '' });
  const [openSettings, setOpenSettings] = useState(false);
  const [sideBarSettings, setSideBarSettings] = useState({
    showMail: false,
    showSepa: false,
    links: [],
  });
  const [collapsed, setCollapsed] = useState(isMobile);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [firmwareUpdate, setFirmwareUpdate] = useState<{
    newFirmwareAvailable: boolean;
    currentVersion?: string;
    latestVersion?: string;
  } | null>(null);
  const [notificationsAnchor, setNotificationsAnchor] = useState<HTMLElement | null>(null);
  const { t, i18n } = useTranslation();

  const drawerWidth = collapsed ? drawerWidthCollapsed : drawerWidthExpanded;

  useEffect(() => {
    setCollapsed(isMobile);
  }, [isMobile]);

  useEffect(() => {
    if (collapsed) {
      setOpenSettings(false);
    }
  }, [collapsed]);

  useEffect(() => {
    if (isMobile) {
      setMobileNavOpen(false);
    }
  }, [page, isMobile]);

  const loadSettings = async () => {
    try {
      const settings = await api('/web-page-config/sidebar');
      if (settings) {
        setSideBarSettings(settings);
      }
    } catch (e) {}
  };

  const loadFirmwareUpdate = async () => {
    try {
      const result = await api('/notifications/firmware-update');
      if (result) {
        setFirmwareUpdate(result);
      }
    } catch (e) {}
  };

  useEffect(() => {
    const tokenString = localStorage.getItem('token');
    if (!tokenString) return;

    try {
      const payload = JSON.parse(atob(tokenString.split('.')[1]));
      setUser({
        name: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'],
        role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
        id: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
      });
    } catch (e) {
      console.error('Token parse error', e);
    }

    loadSettings();
  }, []);

  useEffect(() => {
    if (user.role === Role.ADMIN) {
      loadFirmwareUpdate();
    }
  }, [user.role]);

  const navItems = [
    {
      id: 'members',
      label: t('pages.dashboard.pageNames.members'),
      shortLabel: t('pages.dashboard.pageNames.membersShort'),
      icon: <PeopleIcon />,
      roles: [Role.USER, Role.ADMIN, Role.FINANCIAL_MANAGER],
    },
    {
      id: 'mail',
      label: t('pages.dashboard.pageNames.broadcastEmail'),
      icon: <EmailIcon />,
      roles: [Role.USER, Role.ADMIN, Role.FINANCIAL_MANAGER],
    },
    {
      id: 'sepa',
      label: t('pages.dashboard.pageNames.sepa'),
      icon: <AccountBalanceIcon />,
      roles: [Role.ADMIN, Role.FINANCIAL_MANAGER],
    },
    {
      id: 'contributions',
      label: t('pages.dashboard.pageNames.contributions'),
      icon: <EuroIcon />,
      roles: [Role.ADMIN, Role.USER, Role.FINANCIAL_MANAGER],
    },
  ];

  const handleSettingsClick = () => {
    if (collapsed) {
      setCollapsed(false);
      setOpenSettings(true);
    } else {
      setOpenSettings(!openSettings);
    }
  };

  const viewPage = (pageId: string) => {
    switch (pageId) {
      case 'mail':
        return sideBarSettings?.showMail ?? false;
      case 'sepa':
      case 'contributions':
        return sideBarSettings?.showSepa ?? false;
      default:
        return true;
    }
  };

  const bottomNavItems = navItems.filter(
    (item) => item.roles.includes(user.role as Role) && viewPage(item.id),
  );

  return (
    <Box sx={{ display: 'flex' }}>
      <CssBaseline />
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          width: isMobile ? '100%' : `calc(100% - ${drawerWidth}px)`,
          ml: isMobile ? 0 : `${drawerWidth}px`,
          bgcolor: 'white',
          color: 'text.primary',
          borderBottom: '1px solid',
          borderColor: 'divider',
          transition: theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
        }}
      >
        <Toolbar sx={{ justifyContent: 'space-between' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            {isMobile && (
              <IconButton
                edge="start"
                onClick={() => setMobileNavOpen(true)}
                aria-label={t('pages.dashboard.openMenu')}
              >
                <MenuIcon />
              </IconButton>
            )}
            <Typography variant="h6" sx={{ fontWeight: 600 }}>
              {''}
            </Typography>
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: isMobile ? 0.5 : 2 }}>
            {user.role === Role.ADMIN && (
              <>
                <Tooltip title={t('pages.dashboard.notifications.label')} arrow>
                  <IconButton
                    onClick={(e) => setNotificationsAnchor(e.currentTarget)}
                    color="inherit"
                  >
                    <Badge
                      color="error"
                      variant="dot"
                      invisible={!firmwareUpdate?.newFirmwareAvailable}
                    >
                      <NotificationsIcon />
                    </Badge>
                  </IconButton>
                </Tooltip>
                <Popover
                  open={Boolean(notificationsAnchor)}
                  anchorEl={notificationsAnchor}
                  onClose={() => setNotificationsAnchor(null)}
                  anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
                  transformOrigin={{ vertical: 'top', horizontal: 'right' }}
                >
                  <Box sx={{ p: 2, maxWidth: 320 }}>
                    {firmwareUpdate?.newFirmwareAvailable ? (
                      <>
                        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                          {t('pages.dashboard.notifications.firmwareUpdateTitle')}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {t('pages.dashboard.notifications.firmwareUpdateText', {
                            version: firmwareUpdate.latestVersion,
                            currentVersion: firmwareUpdate.currentVersion,
                          })}
                        </Typography>
                      </>
                    ) : (
                      <Typography variant="body2" color="text.secondary">
                        {t('pages.dashboard.notifications.noNotifications')}
                      </Typography>
                    )}
                  </Box>
                </Popover>
              </>
            )}

            {isMobile ? (
              <Tooltip title={t('pages.dashboard.switchLanguage')} arrow>
                <IconButton
                  color="inherit"
                  onClick={() => i18n.changeLanguage(i18n.resolvedLanguage === 'de' ? 'en' : 'de')}
                  aria-label={t('pages.dashboard.switchLanguage')}
                >
                  <Typography sx={{ fontSize: '1.2rem', lineHeight: 1 }}>
                    {i18n.resolvedLanguage === 'de' ? '🇬🇧' : '🇩🇪'}
                  </Typography>
                </IconButton>
              </Tooltip>
            ) : (
              <Button
                onClick={() => i18n.changeLanguage(i18n.resolvedLanguage === 'de' ? 'en' : 'de')}
              >
                {i18n.resolvedLanguage === 'de' ? '🇬🇧 English' : '🇩🇪 Deutsch'}
              </Button>
            )}

            {!isMobile && <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />}

            {isMobile ? (
              <Tooltip title={t('pages.dashboard.logout')} arrow>
                <IconButton
                  color="error"
                  onClick={onLogout}
                  aria-label={t('pages.dashboard.logout')}
                >
                  <LogoutIcon />
                </IconButton>
              </Tooltip>
            ) : (
              <Button
                variant="outlined"
                color="error"
                startIcon={<LogoutIcon />}
                onClick={onLogout}
                sx={{
                  borderRadius: 2,
                  textTransform: 'none',
                  '&:hover': { bgcolor: 'error.lighter' },
                }}
              >
                {t('pages.dashboard.logout')}
              </Button>
            )}
          </Box>
        </Toolbar>
      </AppBar>

      <Drawer
        variant={isMobile ? 'temporary' : 'permanent'}
        anchor="left"
        open={isMobile ? mobileNavOpen : true}
        onClose={() => setMobileNavOpen(false)}
        ModalProps={isMobile ? { keepMounted: true } : undefined}
        sx={{
          width: isMobile ? undefined : drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: isMobile ? drawerWidthExpanded : drawerWidth,
            boxSizing: 'border-box',
            borderRight: isMobile ? undefined : '1px solid',
            borderColor: 'divider',
            overflowX: 'hidden',
            transition: theme.transitions.create('width', {
              easing: theme.transitions.easing.sharp,
              duration: theme.transitions.duration.enteringScreen,
            }),
          },
        }}
      >
        <SidebarContent
          collapsedView={isMobile ? false : collapsed}
          showToggle={!isMobile}
          pageName={pageName}
          page={page}
          onPageChange={setPage}
          onToggleCollapse={() => setCollapsed(!collapsed)}
          navItems={navItems}
          sideBarSettings={sideBarSettings}
          user={user}
          openSettings={openSettings}
          onSettingsClick={handleSettingsClick}
        />
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          bgcolor: 'grey.50',
          p: { xs: 1.5, sm: 3 },
          pb: {
            xs: isMobile
              ? `calc(${bottomNavHeight}px + env(safe-area-inset-bottom, 0px) + 16px)`
              : 1.5,
            sm: 3,
          },
          minHeight: '100vh',
          transition: theme.transitions.create('margin', {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
        }}
      >
        <Toolbar />
        <Container maxWidth="xl" disableGutters={isMobile}>
          <Paper
            elevation={0}
            sx={{
              p: { xs: 1.5, sm: 4 },
              borderRadius: { xs: 2, sm: 3 },
              border: '1px solid',
              borderColor: 'divider',
              minHeight: { xs: 'auto', sm: '70vh' },
            }}
          >
            {page === 'members' && <Members role={user.role} />}
            {page === 'mail' && <Mail />}
            {page === 'sepa' && <Sepa />}
            {page === 'contributions' && <Contributions role={user.role} />}
            {page === 'user' && <UserManagement accountView={true} userId={user.id} />}
            {page === 'users' && <UserManagement accountView={false} userId={user.id} />}
            {page === 'email-config' && <EmailConfig />}
            {page === 'link-config' && <LinkConfig />}
            {page === 'contribution-plan-config' && <ContributionPlanConfig />}
            {page === 'audit' && <Audit />}
            {page === 'backup' && <Backup />}
            {page === 'creditor-config' && <CreditorConfig />}
            {page === 'general-config' && <GeneralConfig />}
            {page === 'member-category-config' && <MemberCategoryConfig />}
          </Paper>
        </Container>
      </Box>

      {isMobile && bottomNavItems.length > 0 && (
        <BottomNavigation
          value={page}
          onChange={(_, newValue) => setPage(newValue)}
          sx={{
            position: 'fixed',
            bottom: 0,
            left: 0,
            right: 0,
            height: `calc(${bottomNavHeight}px + env(safe-area-inset-bottom, 0px))`,
            paddingBottom: 'env(safe-area-inset-bottom, 0px)',
            boxSizing: 'border-box',
            zIndex: theme.zIndex.appBar,
            borderTop: '1px solid',
            borderColor: 'divider',
          }}
        >
          {bottomNavItems.map((item) => (
            <BottomNavigationAction
              key={item.id}
              label={item.shortLabel ?? item.label}
              value={item.id}
              icon={item.icon}
              sx={{ minWidth: 0, px: 0.5 }}
            />
          ))}
        </BottomNavigation>
      )}
    </Box>
  );
}
