import { useCallback, useEffect, useState } from 'react';
import {
  Avatar,
  Box,
  Button,
  CircularProgress,
  Container,
  Paper,
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
import { LanguageToggle } from '../components/LanguageToggle';
import { PoweredBy } from '../components/PoweredBy';
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

function FloatingLanguageToggle({ isMobile }: { isMobile: boolean }) {
  return (
    <Box
      sx={{
        position: 'fixed',
        top: 16,
        right: 16,
        zIndex: (theme) => theme.zIndex.modal + 1,
      }}
    >
      <LanguageToggle isMobile={isMobile} floating />
    </Box>
  );
}

export default function PublicEnrollmentPage({ embedded = false }: { embedded?: boolean }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { config, loading: configLoading } = usePageConfig();
  const [memberCategories, setMemberCategories] = useState<MemberCategory[]>([]);
  const [contributionPlans, setContributionPlans] = useState<ContributionPlans[]>([]);
  const [legalText, setLegalText] = useState('');
  const [status, setStatus] = useState<'loading' | 'ready' | 'closed' | 'error'>('loading');
  const [formKey, setFormKey] = useState(0);

  const loadFormData = useCallback(async () => {
    try {
      const data = await api('/self-enrollment/form-data');
      if (!data || typeof data !== 'object') {
        throw new Error('Form data unavailable');
      }
      if (data.enabled === true) {
        setMemberCategories(data.memberCategories ?? []);
        setContributionPlans(data.contributionPlans ?? []);
        setLegalText(data.legalText ?? '');
        setStatus('ready');
      } else {
        setStatus('closed');
      }
    } catch {
      setStatus('error');
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadFormData();
  }, [loadFormData]);

  const handleRetry = () => {
    setStatus('loading');
    loadFormData();
  };

  useEffect(() => {
    if (!embedded || window.parent === window) return;
    document.body.style.backgroundColor = 'transparent';
    const report = () =>
      window.parent.postMessage(
        {
          type: 'uv-enroll-height',
          height: Math.ceil(document.body.getBoundingClientRect().height),
        },
        '*',
      );
    const observer = new ResizeObserver(report);
    observer.observe(document.body);
    report();
    return () => observer.disconnect();
  }, [embedded]);

  const handleClosed = () => {
    setFormKey((key) => key + 1);
    loadFormData();
  };

  const unavailable = (
    <>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1, textAlign: 'center' }}>
        {status === 'error'
          ? t('pages.publicEnrollment.loadError')
          : t('pages.publicEnrollment.closedMessage')}
      </Typography>
      {status === 'error' && (
        <Button onClick={handleRetry} sx={{ mt: 1, textTransform: 'none' }}>
          {t('pages.publicEnrollment.retry')}
        </Button>
      )}
    </>
  );

  const background =
    theme.palette.mode === 'dark'
      ? 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)'
      : 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)';

  if (embedded) {
    return (
      <Box sx={{ p: 1 }}>
        {status === 'loading' ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress />
          </Box>
        ) : status !== 'ready' ? (
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', py: 2 }}>
            {unavailable}
          </Box>
        ) : (
          <MemberForm
            key={formKey}
            view={false}
            mode="public"
            member={emptyPublicMember()}
            contributionPlans={contributionPlans}
            memberCategories={memberCategories}
            legalText={legalText}
            embedded
            onClose={handleClosed}
          />
        )}
      </Box>
    );
  }

  if (configLoading || status === 'loading') {
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

  if (status !== 'ready') {
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
        <FloatingLanguageToggle isMobile={isMobile} />
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
            {unavailable}
          </Paper>
          <PoweredBy sx={{ mt: 2 }} />
        </Container>
      </Box>
    );
  }

  return (
    <Box sx={{ position: 'relative', minHeight: '100vh', background }}>
      <FloatingLanguageToggle isMobile={isMobile} />
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

      <MemberForm
        key={formKey}
        view={false}
        mode="public"
        member={emptyPublicMember()}
        contributionPlans={contributionPlans}
        memberCategories={memberCategories}
        legalText={legalText}
        onClose={handleClosed}
      />
    </Box>
  );
}
