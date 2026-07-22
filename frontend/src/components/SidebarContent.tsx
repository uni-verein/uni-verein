import React from 'react';
import {
  Avatar,
  Box,
  Collapse,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Tooltip,
  Typography,
} from '@mui/material';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import FolderIcon from '@mui/icons-material/Folder';
import SaveIcon from '@mui/icons-material/Save';
import ArticleIcon from '@mui/icons-material/Article';
import WebIcon from '@mui/icons-material/Web';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import GroupIcon from '@mui/icons-material/Group';
import EmailIcon from '@mui/icons-material/Email';
import EuroIcon from '@mui/icons-material/Euro';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import PeopleAltIcon from '@mui/icons-material/PeopleAlt';
import SettingsIcon from '@mui/icons-material/Settings';
import { useTranslation } from 'react-i18next';
import { UUIDTypes } from 'uuid';
import { Role } from '../types';
import { DynamicIcon } from './muiIcons';

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

function DrawerToggleButton({
  collapsedView,
  onToggleCollapse,
}: {
  collapsedView: boolean;
  onToggleCollapse: () => void;
}) {
  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: collapsedView ? 'center' : 'flex-end',
        px: collapsedView ? 0 : 1,
        py: 0.5,
        width: '100%',
      }}
    >
      <Tooltip
        title={collapsedView ? 'Menü ausklappen' : 'Menü einklappen'}
        placement="right"
        arrow
      >
        <IconButton
          onClick={onToggleCollapse}
          size="small"
          sx={{
            border: '1px solid',
            borderColor: 'divider',
            bgcolor: 'background.paper',
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          {collapsedView ? (
            <ChevronRightIcon fontSize="small" />
          ) : (
            <ChevronLeftIcon fontSize="small" />
          )}
        </IconButton>
      </Tooltip>
    </Box>
  );
}

export function SidebarContent({
  collapsedView,
  showToggle,
  pageName,
  page,
  onPageChange,
  onToggleCollapse,
  navItems,
  sideBarSettings,
  user,
  openSettings,
  onSettingsClick,
}: {
  collapsedView: boolean;
  showToggle: boolean;
  pageName: string;
  page: string;
  onPageChange: (pageId: string) => void;
  onToggleCollapse: () => void;
  navItems: { id: string; label: string; icon: React.ReactNode; roles: Role[] }[];
  sideBarSettings: {
    showMail: boolean;
    showSepa: boolean;
    links: { id: UUIDTypes; link: string; name: string; icon: string }[];
  };
  user: { id: UUIDTypes | undefined; name: string; role: string | Role };
  openSettings: boolean;
  onSettingsClick: () => void;
}) {
  const { t } = useTranslation();

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

  return (
    <>
      <Box
        sx={{
          p: collapsedView ? 1 : 3,
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsedView ? 'center' : 'flex-start',
          minHeight: 64,
        }}
      >
        {collapsedView ? (
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

        {showToggle && (
          <DrawerToggleButton collapsedView={collapsedView} onToggleCollapse={onToggleCollapse} />
        )}
      </Box>

      <Divider sx={{ mx: collapsedView ? 0 : 2, mb: 1 }} />

      <List sx={{ px: collapsedView ? 0.5 : 2 }}>
        {navItems.map((item) =>
          item.roles.includes(user.role as Role) && viewPage(item.id) ? (
            <ListItem key={item.id} disablePadding sx={{ mb: 0.5 }}>
              <Tooltip title={collapsedView ? item.label : ''} placement="right" arrow>
                <ListItemButton
                  onClick={() => onPageChange(item.id)}
                  selected={page === item.id}
                  sx={{
                    justifyContent: collapsedView ? 'center' : 'flex-start',
                    px: collapsedView ? 1 : 2,
                    ...selectedStyle,
                  }}
                >
                  <ListItemIcon
                    sx={{
                      minWidth: collapsedView ? 'unset' : 40,
                      justifyContent: 'center',
                    }}
                  >
                    {item.icon}
                  </ListItemIcon>
                  {!collapsedView && (
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
          sideBarSettings.links.map((linkEntry) => {
            return (
              <ListItem key={linkEntry.name} disablePadding sx={{ mb: 0.5 }}>
                <Tooltip
                  title={collapsedView ? t('pages.dashboard.pageNames.files') : ''}
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
                      justifyContent: collapsedView ? 'center' : 'flex-start',
                      px: collapsedView ? 1 : 2,
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        minWidth: collapsedView ? 'unset' : 40,
                        justifyContent: 'center',
                      }}
                    >
                      <DynamicIcon name={linkEntry.icon} fontSize="small" color="action" />
                    </ListItemIcon>
                    {!collapsedView && (
                      <ListItemText
                        primary={linkEntry.name}
                        primaryTypographyProps={{ fontSize: '0.9rem', fontWeight: 500 }}
                      />
                    )}
                  </ListItemButton>
                </Tooltip>
              </ListItem>
            );
          })}

        {user.role === Role.ADMIN && (
          <>
            <ListItem key="audit" disablePadding sx={{ mb: 0.5 }}>
              <Tooltip
                title={collapsedView ? t('pages.dashboard.pageNames.audit') : ''}
                placement="right"
                arrow
              >
                <ListItemButton
                  onClick={() => onPageChange('audit')}
                  selected={page === 'audit'}
                  sx={{
                    justifyContent: collapsedView ? 'center' : 'flex-start',
                    px: collapsedView ? 1 : 2,
                    ...selectedStyle,
                  }}
                >
                  <ListItemIcon
                    sx={{
                      minWidth: collapsedView ? 'unset' : 40,
                      justifyContent: 'center',
                    }}
                  >
                    <ArticleIcon />
                  </ListItemIcon>
                  {!collapsedView && (
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
                title={collapsedView ? t('pages.dashboard.pageNames.backup') : ''}
                placement="right"
                arrow
              >
                <ListItemButton
                  onClick={() => onPageChange('backup')}
                  selected={page === 'backup'}
                  sx={{
                    justifyContent: collapsedView ? 'center' : 'flex-start',
                    px: collapsedView ? 1 : 2,
                    ...selectedStyle,
                  }}
                >
                  <ListItemIcon
                    sx={{
                      minWidth: collapsedView ? 'unset' : 40,
                      justifyContent: 'center',
                    }}
                  >
                    <SaveIcon />
                  </ListItemIcon>
                  {!collapsedView && (
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
                title={collapsedView ? t('pages.dashboard.settings.name') : ''}
                placement="right"
                arrow
              >
                <ListItemButton
                  onClick={onSettingsClick}
                  sx={{
                    borderRadius: 2,
                    mt: 1,
                    justifyContent: collapsedView ? 'center' : 'flex-start',
                    px: collapsedView ? 1 : 2,
                  }}
                >
                  <ListItemIcon
                    sx={{
                      minWidth: collapsedView ? 'unset' : 40,
                      justifyContent: 'center',
                    }}
                  >
                    <SettingsIcon />
                  </ListItemIcon>
                  {!collapsedView && (
                    <>
                      <ListItemText primary={t('pages.dashboard.settings.name')} />
                      {openSettings ? <ExpandLess /> : <ExpandMore />}
                    </>
                  )}
                </ListItemButton>
              </Tooltip>
            </ListItem>

            <Collapse in={openSettings && !collapsedView} timeout="auto" unmountOnExit>
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
                    onClick={() => onPageChange(item.id)}
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
        sx={{
          mt: 'auto',
          p: collapsedView ? 1 : 2,
          borderTop: '1px solid',
          borderColor: 'divider',
        }}
      >
        <Tooltip
          title={
            collapsedView
              ? `${user.name} (${user.role})`
              : t('pages.dashboard.settings.profileSettings')
          }
          placement="right"
          arrow
        >
          <Box
            onClick={() => onPageChange('user')}
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: collapsedView ? 0 : 2,
              px: 1,
              py: 0.75,
              borderRadius: 2,
              cursor: 'pointer',
              justifyContent: collapsedView ? 'center' : 'flex-start',
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
            {!collapsedView && (
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
    </>
  );
}
