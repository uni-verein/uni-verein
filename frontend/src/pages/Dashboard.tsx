import React, { useEffect, useState } from 'react';
import {
  Box,
  Drawer,
  AppBar,
  Toolbar,
  List,
  Typography,
  Divider,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Avatar,
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
} from '@mui/material';
import PeopleIcon from '@mui/icons-material/People';
import NotificationsIcon from '@mui/icons-material/Notifications';
import EmailIcon from '@mui/icons-material/Email';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import EuroIcon from '@mui/icons-material/Euro';
import LogoutIcon from '@mui/icons-material/Logout';
import SettingsIcon from '@mui/icons-material/Settings';
import PeopleAltIcon from '@mui/icons-material/PeopleAlt';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import FolderIcon from '@mui/icons-material/Folder';
import Collapse from '@mui/material/Collapse';
import SaveIcon from '@mui/icons-material/Save';
import ArticleIcon from '@mui/icons-material/Article';
import WebIcon from '@mui/icons-material/Web';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import GroupIcon from '@mui/icons-material/Group';

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
import { DynamicIcon } from '../components/muiIcons';
import MemberCategoryConfig from './MemberCategoryConfig';

const drawerWidthExpanded = 280;
const drawerWidthCollapsed = 64;

export default function Dashboard({
  onLogout,
  pageName,
}: {
  onLogout?: () => void;
  pageName: string;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

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

  const selectedStyle = {
    borderRadius: 2,
    '&.Mui-selected': {
      bgcolor: 'primary.light',
      color: 'white',
      '& .MuiListItemIcon-root': { color: 'white' },
      '& .MuiListItemText-primary': { color: 'white' },
      '&:hover': { bgcolor: 'primary.light' },
    },
  };

  const DrawerToggleButton = () => (
    <Box
      sx={{
        display: 'flex',
        justifyContent: collapsed ? 'center' : 'flex-end',
        px: collapsed ? 0 : 1,
        py: 0.5,
        width: '100%',
      }}
    >
      <Tooltip title={collapsed ? 'Menü ausklappen' : 'Menü einklappen'} placement="right" arrow>
        <IconButton
          onClick={() => setCollapsed(!collapsed)}
          size="small"
          sx={{
            border: '1px solid',
            borderColor: 'divider',
            bgcolor: 'background.paper',
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          {collapsed ? <ChevronRightIcon fontSize="small" /> : <ChevronLeftIcon fontSize="small" />}
        </IconButton>
      </Tooltip>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex' }}>
      <CssBaseline />
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          width: `calc(100% - ${drawerWidth}px)`,
          ml: `${drawerWidth}px`,
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
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            {''}
          </Typography>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
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
            <Button onClick={() => i18n.changeLanguage(i18n.language === 'de' ? 'en' : 'de')}>
              {i18n.language === 'de' ? '🇬🇧 English' : '🇩🇪 Deutsch'}
            </Button>
            <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
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
          </Box>
        </Toolbar>
      </AppBar>

      <Drawer
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: drawerWidth,
            boxSizing: 'border-box',
            borderRight: '1px solid',
            borderColor: 'divider',
            overflowX: 'hidden',
            transition: theme.transitions.create('width', {
              easing: theme.transitions.easing.sharp,
              duration: theme.transitions.duration.enteringScreen,
            }),
          },
        }}
        variant="permanent"
        anchor="left"
      >
        <Box
          sx={{
            p: collapsed ? 1 : 3,
            display: 'flex',
            alignItems: 'center',
            justifyContent: collapsed ? 'center' : 'flex-start',
            minHeight: 64,
          }}
        >
          {collapsed ? (
            <Tooltip
              title={pageName !== '' ? pageName : t('pages.dashboard.clubManagement')}
              placement="right"
              arrow
            >
              <Box
                component="span"
                sx={{
                  bgcolor: 'primary.main',
                  color: 'white',
                  px: 1,
                  borderRadius: 1,
                  fontWeight: 800,
                  fontSize: '1.1rem',
                }}
              >
                {(pageName !== '' ? pageName : t('pages.dashboard.clubManagement')).substring(0, 1)}
              </Box>
            </Tooltip>
          ) : (
            <Typography
              variant="h6"
              sx={{
                fontWeight: 800,
                color: 'primary.main',
                display: 'flex',
                alignItems: 'center',
                gap: 1,
                whiteSpace: 'nowrap',
              }}
            >
              <Box
                component="span"
                sx={{ bgcolor: 'primary.main', color: 'white', px: 1, borderRadius: 1 }}
              >
                {(pageName !== '' ? pageName : t('pages.dashboard.clubManagement')).substring(0, 1)}
              </Box>
              {pageName !== '' ? pageName : t('pages.dashboard.clubManagement')}
            </Typography>
          )}

          <DrawerToggleButton />
        </Box>

        <Divider sx={{ mx: collapsed ? 0 : 2, mb: 1 }} />

        <List sx={{ px: collapsed ? 0.5 : 2 }}>
          {navItems.map((item) =>
            item.roles.includes(user.role as Role) && viewPage(item.id) ? (
              <ListItem key={item.id} disablePadding sx={{ mb: 0.5 }}>
                <Tooltip title={collapsed ? item.label : ''} placement="right" arrow>
                  <ListItemButton
                    onClick={() => setPage(item.id)}
                    selected={page === item.id}
                    sx={{
                      justifyContent: collapsed ? 'center' : 'flex-start',
                      px: collapsed ? 1 : 2,
                      ...selectedStyle,
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        minWidth: collapsed ? 'unset' : 40,
                        justifyContent: 'center',
                      }}
                    >
                      {item.icon}
                    </ListItemIcon>
                    {!collapsed && (
                      <ListItemText
                        primary={item.label}
                        primaryTypographyProps={{ fontSize: '0.9rem', fontWeight: 500 }}
                      />
                    )}
                  </ListItemButton>
                </Tooltip>
              </ListItem>
            ) : null,
          )}

          {sideBarSettings.links &&
            sideBarSettings.links.map(
              (linkEntry: { id: UUIDTypes; link: string; name: string; icon: string }) => {
                return (
                  <ListItem key={linkEntry.name} disablePadding sx={{ mb: 0.5 }}>
                    <Tooltip
                      title={collapsed ? t('pages.dashboard.pageNames.files') : ''}
                      placement="right"
                      arrow
                    >
                      <ListItemButton
                        component="a"
                        href={
                          linkEntry.link.startsWith('http')
                            ? linkEntry.link
                            : `https://${linkEntry.link}`
                        }
                        target="_blank"
                        rel="noopener noreferrer"
                        sx={{
                          borderRadius: 2,
                          justifyContent: collapsed ? 'center' : 'flex-start',
                          px: collapsed ? 1 : 2,
                        }}
                      >
                        <ListItemIcon
                          sx={{
                            minWidth: collapsed ? 'unset' : 40,
                            justifyContent: 'center',
                          }}
                        >
                          <DynamicIcon name={linkEntry.icon} fontSize="small" color="action" />
                        </ListItemIcon>
                        {!collapsed && (
                          <ListItemText
                            primary={linkEntry.name}
                            primaryTypographyProps={{ fontSize: '0.9rem', fontWeight: 500 }}
                          />
                        )}
                      </ListItemButton>
                    </Tooltip>
                  </ListItem>
                );
              },
            )}

          {user.role === Role.ADMIN && (
            <>
              <ListItem key="audit" disablePadding sx={{ mb: 0.5 }}>
                <Tooltip
                  title={collapsed ? t('pages.dashboard.pageNames.audit') : ''}
                  placement="right"
                  arrow
                >
                  <ListItemButton
                    onClick={() => setPage('audit')}
                    selected={page === 'audit'}
                    sx={{
                      justifyContent: collapsed ? 'center' : 'flex-start',
                      px: collapsed ? 1 : 2,
                      ...selectedStyle,
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        minWidth: collapsed ? 'unset' : 40,
                        justifyContent: 'center',
                      }}
                    >
                      <ArticleIcon />
                    </ListItemIcon>
                    {!collapsed && (
                      <ListItemText
                        primary={t('pages.dashboard.pageNames.audit')}
                        primaryTypographyProps={{ fontSize: '0.9rem', fontWeight: 500 }}
                      />
                    )}
                  </ListItemButton>
                </Tooltip>
              </ListItem>

              <ListItem key="backup" disablePadding sx={{ mb: 0.5 }}>
                <Tooltip
                  title={collapsed ? t('pages.dashboard.pageNames.backup') : ''}
                  placement="right"
                  arrow
                >
                  <ListItemButton
                    onClick={() => setPage('backup')}
                    selected={page === 'backup'}
                    sx={{
                      justifyContent: collapsed ? 'center' : 'flex-start',
                      px: collapsed ? 1 : 2,
                      ...selectedStyle,
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        minWidth: collapsed ? 'unset' : 40,
                        justifyContent: 'center',
                      }}
                    >
                      <SaveIcon />
                    </ListItemIcon>
                    {!collapsed && (
                      <ListItemText
                        primary={t('pages.dashboard.pageNames.backup')}
                        primaryTypographyProps={{ fontSize: '0.9rem', fontWeight: 500 }}
                      />
                    )}
                  </ListItemButton>
                </Tooltip>
              </ListItem>
            </>
          )}

          {user.role === Role.ADMIN && (
            <>
              <ListItem disablePadding>
                <Tooltip
                  title={collapsed ? t('pages.dashboard.settings.name') : ''}
                  placement="right"
                  arrow
                >
                  <ListItemButton
                    onClick={handleSettingsClick}
                    sx={{
                      borderRadius: 2,
                      mt: 1,
                      justifyContent: collapsed ? 'center' : 'flex-start',
                      px: collapsed ? 1 : 2,
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        minWidth: collapsed ? 'unset' : 40,
                        justifyContent: 'center',
                      }}
                    >
                      <SettingsIcon />
                    </ListItemIcon>
                    {!collapsed && (
                      <>
                        <ListItemText primary={t('pages.dashboard.settings.name')} />
                        {openSettings ? <ExpandLess /> : <ExpandMore />}
                      </>
                    )}
                  </ListItemButton>
                </Tooltip>
              </ListItem>

              <Collapse in={openSettings && !collapsed} timeout="auto" unmountOnExit>
                <List component="div" disablePadding sx={{ pl: 3 }}>
                  {[
                    {
                      id: 'users',
                      icon: <PeopleAltIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.userManagement'),
                    },
                    {
                      id: 'email-config',
                      icon: <EmailIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.emailConfig'),
                    },
                    {
                      id: 'link-config',
                      icon: <FolderIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.linkConfig'),
                    },
                    {
                      id: 'contribution-plan-config',
                      icon: <EuroIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.contributionPlanConfig'),
                    },
                    {
                      id: 'member-category-config',
                      icon: <GroupIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.memberCategoryConfig'),
                    },
                    {
                      id: 'creditor-config',
                      icon: <AccountBalanceIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.creditorConfig'),
                    },
                    {
                      id: 'general-config',
                      icon: <WebIcon fontSize="small" />,
                      label: t('pages.dashboard.settings.generalConfig'),
                    },
                  ].map((item) => (
                    <ListItemButton
                      key={item.id}
                      onClick={() => setPage(item.id)}
                      selected={page === item.id}
                      sx={{ borderRadius: 2, mt: 0.5 }}
                    >
                      <ListItemIcon sx={{ minWidth: 35 }}>{item.icon}</ListItemIcon>
                      <ListItemText primary={item.label} />
                    </ListItemButton>
                  ))}
                </List>
              </Collapse>
            </>
          )}
        </List>

        <Box
          sx={{ mt: 'auto', p: collapsed ? 1 : 2, borderTop: '1px solid', borderColor: 'divider' }}
        >
          <Tooltip
            title={
              collapsed
                ? `${user.name} (${user.role})`
                : t('pages.dashboard.settings.profileSettings')
            }
            placement="right"
            arrow
          >
            <Box
              onClick={() => setPage('user')}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: collapsed ? 0 : 2,
                px: 1,
                py: 0.75,
                borderRadius: 2,
                cursor: 'pointer',
                justifyContent: collapsed ? 'center' : 'flex-start',
                transition: 'background-color 0.2s',
                '&:hover': { bgcolor: 'action.hover' },
              }}
            >
              <Avatar
                sx={{
                  bgcolor: 'secondary.main',
                  width: 32,
                  height: 32,
                }}
              >
                {user.name.charAt(0)}
              </Avatar>
              {!collapsed && (
                <>
                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                      {user.name}
                    </Typography>
                    <Typography variant="caption" color="text.secondary" noWrap>
                      {user.role}
                    </Typography>
                  </Box>
                  <MoreVertIcon
                    sx={{ fontSize: 18, color: 'text.secondary', flexShrink: 0, opacity: 0.6 }}
                  />
                </>
              )}
            </Box>
          </Tooltip>
        </Box>
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          bgcolor: 'grey.50',
          p: 3,
          minHeight: '100vh',
          transition: theme.transitions.create('margin', {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
        }}
      >
        <Toolbar />
        <Container maxWidth="xl">
          <Paper
            elevation={0}
            sx={{
              p: 4,
              borderRadius: 3,
              border: '1px solid',
              borderColor: 'divider',
              minHeight: '70vh',
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
    </Box>
  );
}
