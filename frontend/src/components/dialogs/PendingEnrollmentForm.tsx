import React, { useEffect, useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Grid,
  Typography,
  Divider,
  Alert,
  MenuItem,
  Button,
  CircularProgress,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import CloseIcon from '@mui/icons-material/Close';
import { api } from '../../api';
import {
  AcademicDegree,
  BulkMail,
  ContributionPlans,
  Gender,
  MemberCategory,
  MemberErrors,
  PendingSelfEnrollmentDetail,
} from '../../types';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import 'dayjs/locale/de';
import { formatIBAN, ACADEMIC_DEGREE_LABELS, validateIBAN, validateBIC } from '../../utils';
import { NIL as NIL_UUID } from 'uuid';
import * as countries from 'i18n-iso-countries';
import deLocale from 'i18n-iso-countries/langs/de.json';
import { useTranslation } from 'react-i18next';
import { useSnackbar } from '../../hooks/useSnackbar';
countries.registerLocale(deLocale);

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function ValidateRequiredStringLength(value: string, name: string, maxLength: number) {
  if (!value.trim()) {
    return `${name} darf nicht leer sein.`;
  } else if (value.length > maxLength) {
    return `${name} darf maximal ${maxLength} Zeichen lang sein.`;
  }
}

const countryOptions = Object.entries(countries.getNames('de', { select: 'official' }))
  .map(([code, name]) => ({
    value: code,
    label: name,
  }))
  .sort((a, b) => a.label.localeCompare(b.label));

export default function PendingEnrollmentForm({
  id,
  contributionPlans,
  memberCategories,
  onMemberCreated,
  onClose,
}: {
  id: string;
  contributionPlans: ContributionPlans[];
  memberCategories: MemberCategory[];
  onMemberCreated: () => void;
  onClose: () => void;
}) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const { t } = useTranslation();
  const setSnackbar = useSnackbar();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [detail, setDetail] = useState<PendingSelfEnrollmentDetail | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [errors, setErrors] = useState<MemberErrors>({});

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await api(`/pending-self-enrollments/${id}`);
        if (!cancelled) setDetail(response as PendingSelfEnrollmentDetail);
      } catch {
        if (!cancelled) setLoadError(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id]);

  const validate = () => {
    if (!detail) return false;
    const newErrors: MemberErrors = {};

    if (!detail.gender.trim()) {
      newErrors.gender = t('components.memberForm.validation.genderRequired');
    }

    let value = ValidateRequiredStringLength(
      detail.firstName,
      t('components.memberForm.fields.firstName'),
      100,
    );
    if (value !== undefined) {
      newErrors.firstName = value;
    }

    if (detail.middleName.length > 100) {
      newErrors.middleName = t('components.memberForm.validation.middleNameMaxLength');
    }

    value = ValidateRequiredStringLength(
      detail.lastName,
      t('components.memberForm.fields.lastName'),
      100,
    );
    if (value !== undefined) {
      newErrors.lastName = value;
    }

    if (!detail.birthday) {
      newErrors.birthday = t('components.memberForm.validation.birthdayRequired');
    } else if (new Date(detail.birthday) > new Date()) {
      newErrors.birthday = t('components.memberForm.validation.birthdayPast');
    }

    value = ValidateRequiredStringLength(
      detail.street,
      t('components.memberForm.fields.street'),
      100,
    );
    if (value !== undefined) {
      newErrors.street = value;
    }

    value = ValidateRequiredStringLength(
      detail.postalCode,
      t('components.memberForm.fields.postalCode'),
      10,
    );
    if (value !== undefined) {
      newErrors.postalCode = value;
    }

    value = ValidateRequiredStringLength(detail.city, t('components.memberForm.fields.city'), 100);
    if (value !== undefined) {
      newErrors.city = value;
    }

    if (!detail.countryCode) {
      newErrors.countryCode = t('components.memberForm.validation.countryCodeRequired');
    }

    value = ValidateRequiredStringLength(detail.email, t('components.memberForm.fields.email'), 50);
    if (value !== undefined) {
      newErrors.email = value;
    } else if (!EMAIL_REGEX.test(detail.email)) {
      newErrors.email = t('components.memberForm.validation.emailInvalid');
    }

    if (!detail.startOfStudies) {
      newErrors.startOfStudies = t('components.memberForm.validation.startOfStudiesRequired');
    } else if (new Date(detail.startOfStudies) > new Date()) {
      newErrors.startOfStudies = t('components.memberForm.validation.startOfStudiesInvalid');
    }

    if (
      detail.endOfStudies !== null &&
      new Date(detail.endOfStudies) < new Date(detail.startOfStudies ?? new Date())
    ) {
      newErrors.endOfStudies = t('components.memberForm.validation.endOfStudiesInvalid');
    }

    if (detail.courseOfStudy.length > 100) {
      newErrors.courseOfStudy = t('components.memberForm.validation.courseOfStudyMaxLength');
    }

    value = ValidateRequiredStringLength(
      detail.motivation,
      t('components.memberForm.fields.motivation'),
      1000,
    );
    if (value !== undefined) {
      newErrors.motivation = value;
    }

    if (!detail.memberCategoryId) {
      newErrors.memberCategoryId = t('components.memberForm.validation.memberCategoryRequired');
    }

    if (!detail.iban.trim()) {
      newErrors.iban = t('components.memberForm.validation.ibanRequired');
    } else if (!validateIBAN(detail.iban)) {
      newErrors.iban = t('components.memberForm.validation.ibanError');
    }

    if (!detail.bic.trim()) {
      newErrors.bic = t('components.memberForm.validation.bicRequired');
    } else if (!validateBIC(detail.bic)) {
      newErrors.bic = t('components.memberForm.validation.bicError');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const save = async (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!detail || !validate()) return;

    try {
      const patchResponse = await api(`/pending-self-enrollments/${id}`, {
        method: 'PATCH',
        body: JSON.stringify(detail),
      });
      if (patchResponse === 409) {
        setApiError(t('components.memberForm.alerts.duplicateIbanOrEmail'));
        return;
      }

      const approveResponse = await api(`/pending-self-enrollments/${id}/approve`, {
        method: 'POST',
      });
      if (approveResponse === 409) {
        setApiError(t('components.pendingEnrollments.responseMessages.approveConflict'));
        return;
      }

      setSnackbar({
        status: 'success',
        message: t('components.pendingEnrollments.responseMessages.approved'),
      });
      onMemberCreated();
      onClose();
    } catch {
      setApiError(t('components.memberForm.alerts.saveFailed'));
    }
  };

  const handleChange =
    (field: keyof PendingSelfEnrollmentDetail) => (e: React.ChangeEvent<HTMLInputElement>) => {
      setDetail((prev) => (prev ? { ...prev, [field]: e.target.value } : prev));
      setErrors({ ...errors, [field]: undefined });
    };

  const handleManualChange = (name: string, value: Date | null) => {
    setDetail((prev) => (prev ? { ...prev, [name]: value } : prev));
    setErrors({ ...errors, [name]: undefined });
  };

  const handleIbanChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value.replace(/\s+/g, '').toUpperCase();
    setDetail((prev) => (prev ? { ...prev, iban: raw } : prev));

    if (validateIBAN(raw)) {
      setErrors({ ...errors, iban: undefined });
    } else {
      setErrors({ ...errors, iban: t('components.memberForm.validation.ibanInvalid') });
    }
  };

  const handleBicChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value.replace(/\s+/g, '').toUpperCase();
    setDetail((prev) => (prev ? { ...prev, bic: raw } : prev));

    if (validateBIC(raw)) {
      setErrors({ ...errors, bic: undefined });
    } else {
      setErrors({ ...errors, bic: t('components.memberForm.validation.bicInvalid') });
    }
  };

  const allId = memberCategories.find((x) => x.category === 'ALL')?.id.toString() ?? '';

  return (
    <Dialog
      open={true}
      onClose={onClose}
      fullWidth={!isMobile}
      fullScreen={isMobile}
      maxWidth="sm"
      slotProps={{
        paper: { sx: { borderRadius: isMobile ? 0 : 3, p: 1 } },
      }}
    >
      {loading ? (
        <DialogContent sx={{ textAlign: 'center', py: 8 }}>
          <CircularProgress />
        </DialogContent>
      ) : loadError || !detail ? (
        <>
          <DialogContent sx={{ textAlign: 'center', py: 6 }}>
            <Alert severity="error">{t('components.pendingEnrollmentForm.loadError')}</Alert>
          </DialogContent>
          <DialogActions sx={{ p: 3, justifyContent: 'center' }}>
            <Button onClick={onClose} variant="contained" sx={{ textTransform: 'none' }}>
              {t('components.memberForm.buttons.close')}
            </Button>
          </DialogActions>
        </>
      ) : (
        <form onSubmit={save} noValidate>
          <DialogTitle>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              {t('components.pendingEnrollmentForm.title')}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t('components.pendingEnrollmentForm.subtitle')}
            </Typography>
          </DialogTitle>

          <Divider sx={{ my: 1 }} />

          <DialogContent>
            <Grid container spacing={2}>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.gender')}
                  variant="outlined"
                  value={detail.gender}
                  onChange={handleChange('gender')}
                  select
                  required
                  error={!!errors.gender}
                  helperText={errors.gender}
                >
                  <MenuItem value={Gender.MALE}>
                    {t('components.memberForm.fields.genderOptions.male')}
                  </MenuItem>
                  <MenuItem value={Gender.FEMALE}>
                    {t('components.memberForm.fields.genderOptions.female')}
                  </MenuItem>
                  <MenuItem value={Gender.DIVERSE}>
                    {t('components.memberForm.fields.genderOptions.diverse')}
                  </MenuItem>
                </TextField>
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.firstName')}
                  variant="outlined"
                  value={detail.firstName}
                  onChange={handleChange('firstName')}
                  required
                  error={!!errors.firstName}
                  helperText={errors.firstName ?? `${detail.firstName.length}/100`}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.middleName')}
                  variant="outlined"
                  value={detail.middleName}
                  onChange={handleChange('middleName')}
                  error={!!errors.middleName}
                  helperText={errors.middleName ?? `${detail.middleName.length}/100`}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.lastName')}
                  variant="outlined"
                  value={detail.lastName}
                  onChange={handleChange('lastName')}
                  required
                  error={!!errors.lastName}
                  helperText={errors.lastName ?? `${detail.lastName.length}/100`}
                />
              </Grid>
              <Grid size={6}>
                <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                  <DatePicker
                    label={t('components.memberForm.fields.birthday')}
                    views={['day', 'month', 'year']}
                    format="DD.MM.YYYY"
                    value={detail.birthday ? dayjs(detail.birthday) : null}
                    onChange={(newValue) => {
                      const dateValue = newValue ? newValue.toDate() : null;
                      handleManualChange('birthday', dateValue);
                    }}
                    slotProps={{
                      textField: {
                        fullWidth: true,
                        variant: 'outlined',
                        required: true,
                        error: !!errors.birthday,
                        helperText: errors.birthday,
                      },
                    }}
                  />
                </LocalizationProvider>
              </Grid>
            </Grid>
            <Divider sx={{ my: 1 }} />
            <Grid container spacing={2}>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.street')}
                  variant="outlined"
                  value={detail.street}
                  onChange={handleChange('street')}
                  required
                  error={!!errors.street}
                  helperText={errors.street ?? `${detail.street.length}/100`}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.postalCode')}
                  variant="outlined"
                  value={detail.postalCode}
                  onChange={handleChange('postalCode')}
                  required
                  error={!!errors.postalCode}
                  helperText={errors.postalCode ?? `${detail.postalCode.length}/10`}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.city')}
                  variant="outlined"
                  value={detail.city}
                  onChange={handleChange('city')}
                  required
                  error={!!errors.city}
                  helperText={errors.city ?? `${detail.city.length}/100`}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  label={t('components.memberForm.fields.countryCode')}
                  fullWidth
                  required
                  value={detail.countryCode ?? ''}
                  onChange={handleChange('countryCode')}
                  select
                  error={!!errors.countryCode}
                  helperText={errors.countryCode}
                  slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
                >
                  {countryOptions.map(({ value, label }) => (
                    <MenuItem key={value} value={value}>
                      {label} ({value})
                    </MenuItem>
                  ))}
                </TextField>
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.email')}
                  type="email"
                  variant="outlined"
                  value={detail.email}
                  onChange={handleChange('email')}
                  required
                  error={!!errors.email}
                  helperText={errors.email ?? `${detail.email.length}/50`}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.phone')}
                  variant="outlined"
                  value={detail.phone}
                  onChange={handleChange('phone')}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.bulkMail')}
                  variant="outlined"
                  value={detail.bulkMail ?? BulkMail.ALLOWED}
                  onChange={handleChange('bulkMail')}
                  select
                >
                  <MenuItem value={BulkMail.ALLOWED}>
                    {t('components.memberForm.fields.bulkMailOptions.allowed')}
                  </MenuItem>
                  <MenuItem value={BulkMail.NOT_ALLOWED}>
                    {t('components.memberForm.fields.bulkMailOptions.notAllowed')}
                  </MenuItem>
                </TextField>
              </Grid>
            </Grid>
            <Divider sx={{ my: 1 }} />
            <Grid container spacing={2}>
              <Grid size={6}>
                <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                  <DatePicker
                    label={t('components.memberForm.fields.startOfStudies')}
                    views={['month', 'year']}
                    value={detail.startOfStudies ? dayjs(detail.startOfStudies) : null}
                    onChange={(newValue) => {
                      const dateValue = newValue ? newValue.startOf('month').toDate() : null;
                      handleManualChange('startOfStudies', dateValue);
                    }}
                    slotProps={{
                      textField: {
                        fullWidth: true,
                        variant: 'outlined',
                        required: true,
                        error: !!errors.startOfStudies,
                        helperText: errors.startOfStudies,
                      },
                    }}
                  />
                </LocalizationProvider>
              </Grid>
              <Grid size={6}>
                <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                  <DatePicker
                    label={t('components.memberForm.fields.endOfStudies')}
                    views={['month', 'year']}
                    value={detail.endOfStudies ? dayjs(detail.endOfStudies) : null}
                    onChange={(newValue) => {
                      const dateValue = newValue ? newValue.startOf('month').toDate() : null;
                      handleManualChange('endOfStudies', dateValue);
                    }}
                    slotProps={{
                      textField: {
                        fullWidth: true,
                        variant: 'outlined',
                        error: !!errors.endOfStudies,
                        helperText: errors.endOfStudies,
                      },
                    }}
                  />
                </LocalizationProvider>
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.academicDegree')}
                  variant="outlined"
                  value={detail.academicDegree ?? ''}
                  onChange={(event) =>
                    setDetail((prev) =>
                      prev
                        ? {
                            ...prev,
                            academicDegree:
                              event.target.value === ''
                                ? null
                                : (event.target.value as AcademicDegree),
                          }
                        : prev,
                    )
                  }
                  select
                  slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
                >
                  <MenuItem value="">{t('components.memberForm.fields.noAcademicDegree')}</MenuItem>
                  {Object.entries(ACADEMIC_DEGREE_LABELS).map(([value, label]) => (
                    <MenuItem key={value} value={value}>
                      {label}
                    </MenuItem>
                  ))}
                </TextField>
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.courseOfStudy')}
                  variant="outlined"
                  value={detail.courseOfStudy}
                  onChange={handleChange('courseOfStudy')}
                  error={!!errors.courseOfStudy}
                  helperText={errors.courseOfStudy ?? `${detail.courseOfStudy.length}/100`}
                />
              </Grid>
              <Grid size={12}>
                <TextField
                  fullWidth
                  multiline
                  rows={4}
                  label={t('components.memberForm.fields.motivation')}
                  variant="outlined"
                  value={detail.motivation}
                  onChange={handleChange('motivation')}
                  required
                  error={!!errors.motivation}
                  helperText={errors.motivation ?? `${detail.motivation.length}/1000`}
                />
              </Grid>
              <Grid size={12}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.memberCategory')}
                  variant="outlined"
                  value={
                    !detail.memberCategoryId || detail.memberCategoryId === ''
                      ? allId
                      : detail.memberCategoryId
                  }
                  onChange={handleChange('memberCategoryId')}
                  select
                  required
                  error={!!errors.memberCategoryId}
                  helperText={errors.memberCategoryId}
                >
                  {memberCategories.map((e) => {
                    if (e.category === 'ALL') return null;

                    const translationKey = `components.memberForm.fields.memberCategoryOptions.${e.category}`;
                    const label = t(translationKey).startsWith(translationKey)
                      ? e.name
                      : t(translationKey);

                    return (
                      <MenuItem key={e.id.toString()} value={e.id.toString()}>
                        {label}
                      </MenuItem>
                    );
                  })}
                </TextField>
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.iban')}
                  variant="outlined"
                  placeholder="DE00 0000 0000 0000 0000 00"
                  value={formatIBAN(detail.iban ?? '')}
                  onChange={handleIbanChange}
                  error={errors.iban !== undefined}
                  helperText={errors.iban}
                />
              </Grid>
              <Grid size={6}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.bic')}
                  variant="outlined"
                  placeholder="DEUTDEXXX"
                  value={detail.bic}
                  onChange={handleBicChange}
                  error={errors.bic !== undefined}
                  helperText={errors.bic}
                />
              </Grid>
              <Grid size={12}>
                <TextField
                  fullWidth
                  label={t('components.memberForm.fields.contributionPlan')}
                  variant="outlined"
                  value={detail.contributionPlanId === null ? NIL_UUID : detail.contributionPlanId}
                  onChange={(event) =>
                    event.target.value !== NIL_UUID
                      ? setDetail((prev) =>
                          prev ? { ...prev, contributionPlanId: event.target.value } : prev,
                        )
                      : null
                  }
                  select
                  error={!!errors.contributionPlanId}
                  helperText={errors.contributionPlanId}
                >
                  <MenuItem value={NIL_UUID}>
                    {t('components.memberForm.fields.noContribution')}
                  </MenuItem>
                  {contributionPlans.map((x) => (
                    <MenuItem key={x.id.toString()} value={x.id.toString()}>
                      {x.name}
                    </MenuItem>
                  ))}
                </TextField>
              </Grid>
            </Grid>

            {Object.keys(errors).length !== 0 &&
              Object.values(errors).some((v) => v !== undefined) && (
                <Alert severity="error" onClose={() => setErrors({})} sx={{ mb: 2, mt: 2 }}>
                  {t('components.memberForm.alerts.invalidInputs')}
                </Alert>
              )}

            {apiError && (
              <Alert severity="error" onClose={() => setApiError(null)} sx={{ mb: 2 }}>
                {apiError}
              </Alert>
            )}
          </DialogContent>

          <DialogActions sx={{ p: 3, gap: 1 }}>
            <Button
              onClick={onClose}
              color="inherit"
              startIcon={<CloseIcon />}
              sx={{ textTransform: 'none' }}
            >
              {t('components.memberForm.buttons.cancel')}
            </Button>
            <Button
              type="submit"
              variant="contained"
              color="primary"
              startIcon={<SaveIcon />}
              sx={{ textTransform: 'none', borderRadius: 2, px: 3 }}
            >
              {t('components.pendingEnrollmentForm.buttons.saveAndCreate')}
            </Button>
          </DialogActions>
        </form>
      )}
    </Dialog>
  );
}
