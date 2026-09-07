import { useCallback, useEffect, useState } from 'react';
import {
  Avatar,
  Box,
  Button,
  CircularProgress,
  Container,
  IconButton,
  Paper,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import HowToRegIcon from '@mui/icons-material/HowToReg';
import { useTranslation } from 'react-i18next';
import { NIL as NIL_UUID } from 'uuid';
import { api } from '../api';
import MemberForm from '../components/dialogs/MemberForm';
import { usePageConfig } from '../hooks/usePageConfig';
import {
  BulkMail,
  ContributionPlans,
  Gender,
  Member,
  MemberCategory,
  TaskWithinTheClub,
} from '../types';

function emptyPublicMember(): Member {
  return {
    id: NIL_UUID,
    memberNumber: 0,
    gender: Gender.MALE,
    firstName: '',
    middleName: '',
    lastName: '',
    birthday: null,
    street: '',
    postalCode: '',
    city: '',
    countryCode: null,
    email: '',
    phone: '',
    bulkMail: BulkMail.ALLOWED,
    startOfStudies: null,
    endOfStudies: null,
    academicDegree: null,
    courseOfStudy: '',
    taskWithinTheClub: TaskWithinTheClub.MEMBER,
    memberCategoryId: null,
    iban: '',
    bic: '',
    sepaConsent: null,
    entryDate: new Date(),
    exitDate: null,
    contributionPlanId: null,
    deletedAt: null,
  };
}

function LanguageToggle({ isMobile }: { isMobile: boolean }) {
  const { t, i18n } = useTranslation();
  const label = t('pages.dashboard.switchLanguage');
  const toggle = () => i18n.changeLanguage(i18n.resolvedLanguage === 'de' ? 'en' : 'de');

  return (
    <Box
      sx={{
        position: 'fixed',
        top: 16,
        right: 16,
        zIndex: (theme) => theme.zIndex.modal + 1,
      }}
    >
      {isMobile ? (
        <Tooltip title={label} arrow>
          <IconButton
            color="inherit"
            onClick={toggle}
            aria-label={label}
            sx={{ bgcolor: 'background.paper', boxShadow: 2 }}
          >
            <Typography sx={{ fontSize: '1.2rem', lineHeight: 1 }}>
              {i18n.resolvedLanguage === 'de' ? '🇬🇧' : '🇩🇪'}
            </Typography>
          </IconButton>
        </Tooltip>
      ) : (
        <Button
          onClick={toggle}
          color="inherit"
          sx={{
            bgcolor: 'background.paper',
            boxShadow: 2,
            '&:hover': { bgcolor: 'background.paper' },
          }}
        >
          {i18n.resolvedLanguage === 'de' ? '🇬🇧 English' : '🇩🇪 Deutsch'}
        </Button>
      )}
    </Box>
  );
}

export default function PublicEnrollmentPage() {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { config, loading: configLoading, reloadConfig, serverReachable } = usePageConfig();
  const [memberCategories, setMemberCategories] = useState<MemberCategory[]>([]);
  const [contributionPlans, setContributionPlans] = useState<ContributionPlans[]>([]);
  const [formDataLoading, setFormDataLoading] = useState(false);
  const [formKey, setFormKey] = useState(0);

  const loadFormData = useCallback(async () => {
    setFormDataLoading(true);
    try {
      const data = await api('/self-enrollment/form-data');
      setMemberCategories(data?.memberCategories ?? []);
      setContributionPlans(data?.contributionPlans ?? []);
    } catch {
      setMemberCategories([]);
      setContributionPlans([]);
    } finally {
      setFormDataLoading(false);
    }
  }, []);

  useEffect(() => {
    if (config.selfEnrollmentEnabled) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      loadFormData();
    }
  }, [config.selfEnrollmentEnabled, loadFormData]);

  const handleClosed = () => {
    setFormKey((key) => key + 1);
    reloadConfig().catch(() => {});
    if (config.selfEnrollmentEnabled) {
      loadFormData();
    }
  };

  const background =
    theme.palette.mode === 'dark'
      ? 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)'
      : 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)';

  if (configLoading) {
    return (
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background,
        }}
      >
        <CircularProgress />
      </Box>
    );
  }

  if (!config.selfEnrollmentEnabled) {
    return (
      <Box
        sx={{
          position: 'relative',
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background,
        }}
      >
        <LanguageToggle isMobile={isMobile} />
        <Container maxWidth="xs">
          <Paper
            elevation={6}
            sx={{
              p: 4,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              borderRadius: 3,
            }}
          >
            <Avatar
              src={config.logo}
              sx={{ m: 1, bgcolor: 'transparent', color: 'text.secondary', width: 72, height: 72 }}
            >
              <HowToRegIcon fontSize="large" />
            </Avatar>
            <Typography
              component="h1"
              variant="h5"
              sx={{ fontWeight: 700, mt: 1, textAlign: 'center' }}
            >
              {t('pages.publicEnrollment.closedTitle')}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1, textAlign: 'center' }}>
              {serverReachable
                ? t('pages.publicEnrollment.closedMessage')
                : t('pages.publicEnrollment.loadError')}
            </Typography>
          </Paper>
        </Container>
      </Box>
    );
  }

  return (
    <Box sx={{ position: 'relative', minHeight: '100vh', background }}>
      <LanguageToggle isMobile={isMobile} />
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', pt: 4, px: 2 }}>
        <Avatar
          src={config.logo}
          sx={{ bgcolor: 'transparent', color: 'text.secondary', width: 64, height: 64 }}
        >
          <HowToRegIcon fontSize="large" />
        </Avatar>
        <Typography
          component="h1"
          variant="h5"
          sx={{ fontWeight: 700, mt: 1, textAlign: 'center' }}
        >
          {config.pageName}
        </Typography>
      </Box>

      {formDataLoading && memberCategories.length === 0 ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
          <CircularProgress />
        </Box>
      ) : (
        <MemberForm
          key={formKey}
          view={false}
          mode="public"
          member={emptyPublicMember()}
          contributionPlans={contributionPlans}
          memberCategories={memberCategories}
          onClose={handleClosed}
        />
      )}
    </Box>
  );
}
