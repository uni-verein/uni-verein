import React, { useState } from 'react';
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
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import CloseIcon from '@mui/icons-material/Close';
import { api } from '../api';
import {
  BulkMail,
  ContributionPlans,
  Gender,
  Member,
  MemberCategory,
  MemberErrors,
  SNACKBAR_INITIAL_STATE,
  SnackbarState,
} from '../types';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import 'dayjs/locale/de';
import customParseFormat from 'dayjs/plugin/customParseFormat';
dayjs.extend(customParseFormat);
import {
  formatIBAN,
  TASK_WITHIN_THE_CLUB_LABELS,
  ACADEMIC_DEGREE_LABELS,
  validateIBAN,
  validateBIC,
} from '../utils';
import { NIL as NIL_UUID } from 'uuid';
import * as countries from 'i18n-iso-countries';
import deLocale from 'i18n-iso-countries/langs/de.json';
import { useSnackbar } from './SnackbarContext';
import { CustomSnackbar } from './CustomSnackbar';
import { useTranslation } from 'react-i18next';
countries.registerLocale(deLocale);

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


export default function MemberForm({
  view,
  member,
  contributionPlans,
  memberCategories,
  onClose,
}: {
  view: boolean;
  member: Member;
  contributionPlans: ContributionPlans[];
  memberCategories: MemberCategory[];
  onClose: () => void;
}) {
  const [m, setM] = useState<Member>(member);
  const [apiError, setApiError] = useState<string | null>(null);
  const [errors, setErrors] = useState<MemberErrors>({});
  const [editOrUpdateMember, setEditOrUpdateMember] =
    useState<SnackbarState>(SNACKBAR_INITIAL_STATE);
  const setSuccessMember = useSnackbar();
  const { t } = useTranslation();

  const validate = () => {
    const newErrors: MemberErrors = {};

    if (!m.gender.trim()) {
      newErrors.gender = t('components.memberForm.validation.genderRequired');
    }

    let value = ValidateRequiredStringLength(
      m.firstName,
      t('components.memberForm.fields.firstName'),
      100,
    );
    if (value !== undefined) {
      newErrors.firstName = value;
    }

    if (m.middleName.length > 100) {
      newErrors.middleName = t('components.memberForm.validation.middleNameMaxLength');
    }

    value = ValidateRequiredStringLength(
      m.lastName,
      t('components.memberForm.fields.lastName'),
      100,
    );
    if (value !== undefined) {
      newErrors.lastName = value;
    }

    if (!m.birthday) {
      newErrors.birthday = t('components.memberForm.validation.birthdayRequired');
    } else if (new Date(m.birthday) > new Date()) {
      newErrors.birthday = t('components.memberForm.validation.birthdayPast');
    }

    value = ValidateRequiredStringLength(m.street, t('components.memberForm.fields.street'), 100);
    if (value !== undefined) {
      newErrors.street = value;
    }

    value = ValidateRequiredStringLength(
      m.postalCode,
      t('components.memberForm.fields.postalCode'),
      10,
    );
    if (value !== undefined) {
      newErrors.postalCode = value;
    }

    value = ValidateRequiredStringLength(m.city, t('components.memberForm.fields.city'), 100);
    if (value !== undefined) {
      newErrors.city = value;
    }

    value = ValidateRequiredStringLength(m.email, t('components.memberForm.fields.email'), 50);
    if (value !== undefined) {
      newErrors.email = value;
    }

    if (!m.startOfStudies) {
      newErrors.startOfStudies = t('components.memberForm.validation.startOfStudiesRequired');
    } else if (new Date(m.startOfStudies) > new Date()) {
      newErrors.startOfStudies = t('components.memberForm.validation.startOfStudiesInvalid');
    }

    if (
      m.endOfStudies !== null &&
      new Date(m.endOfStudies) < new Date(m.startOfStudies ?? new Date())
    ) {
      newErrors.endOfStudies = t('components.memberForm.validation.endOfStudiesInvalid');
    }

    if (m.courseOfStudy.length > 100) {
      newErrors.courseOfStudy = t('components.memberForm.validation.courseOfStudyMaxLength');
    }

    if (!m.memberCategoryId) {
      newErrors.memberCategoryId = t('components.memberForm.validation.memberCategoryRequired');
    }

    if (!m.entryDate) {
      newErrors.entryDate = t('components.memberForm.validation.entryDateRequired');
    } else if (new Date(m.entryDate) > new Date()) {
      newErrors.entryDate = t('components.memberForm.validation.entryDateInvalid');
    }

    if (m.exitDate !== null && new Date(m.exitDate) < new Date(m.entryDate ?? new Date())) {
      newErrors.exitDate = t('components.memberForm.validation.exitDateInvalid');
    }

    if (m.iban && !validateIBAN(m.iban)) {
      newErrors.iban = t('components.memberForm.validation.ibanError');
    }

    if (m.bic && !validateBIC(m.bic)) {
      newErrors.bic = t('components.memberForm.validation.bicError');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const save = async (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!validate()) return;

    try {
      if (m.id === NIL_UUID) {
        let response = await api('/members', { method: 'POST', body: JSON.stringify(m) });
        console.log(response);
        if (response === 409) {
          setApiError(t('components.memberForm.alerts.duplicateIbanOrEmail'));
          setEditOrUpdateMember({
            status: 'error',
            message: t('components.memberForm.alerts.duplicateIbanOrEmailShort'),
          });
        } else {
          setSuccessMember({
            status: 'success',
            message: t('components.memberForm.snackbar.createSuccess'),
          });
          onClose();
        }
      } else {
        let response = await api(`/members/${m.id}`, { method: 'PATCH', body: JSON.stringify(m) });
        if (response === 409) {
          setApiError(t('components.memberForm.alerts.duplicateIbanOrEmail'));
          setEditOrUpdateMember({
            status: 'error',
            message: t('components.memberForm.alerts.duplicateIbanOrEmailShort'),
          });
        } else {
          setSuccessMember({
            status: 'success',
            message: t('components.memberForm.snackbar.updateSuccess'),
          });
          onClose();
        }
      }
    } catch (e) {
      setApiError(t('components.memberForm.alerts.saveFailed'));
      setEditOrUpdateMember({
        status: 'error',
        message: t('components.memberForm.alerts.saveFailed'),
      });
    }
  };

  const handleChange = (field: keyof Member) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setM({ ...m, [field]: e.target.value });
    setErrors({ ...errors, [field]: undefined });
  };

  const handleManualChange = (name: string, value: Date | null) => {
    setM((prev) => ({
      ...prev,
      [name]: value,
    }));
    setErrors({ ...errors, [name]: undefined });
  };

  const handleIbanChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value.replace(/\s+/g, '').toUpperCase();

    setM({
      ...m,
      iban: raw,
    });

    if (validateIBAN(raw)) {
      setErrors({ ...errors, iban: undefined });
    } else {
      setErrors({ ...errors, iban: t('components.memberForm.validation.ibanInvalid') });
    }
  };

  const handleBicChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value.replace(/\s+/g, '').toUpperCase();

    setM({
      ...m,
      bic: raw,
    });

    if (validateBIC(raw)) {
      setErrors({ ...errors, bic: undefined });
    } else {
      setErrors({ ...errors, bic: t('components.memberForm.validation.bicInvalid') });
    }
  };

  const formattedIBAN = formatIBAN(m.iban ?? '');
  const allId = memberCategories.find((x) => x.category === 'ALL')?.id.toString() ?? '';

  return (
    <Dialog
      open={true}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      slotProps={{
        paper: { sx: { borderRadius: 3, p: 1 } },
      }}
    >
      <form onSubmit={save}>
        <DialogTitle>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            {m.id !== NIL_UUID
              ? t(`components.memberForm.title.${(view ? 'view' : 'edit')}`)
              : t('components.memberForm.title.new')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {!view && t('components.memberForm.subtitle')}
          </Typography>
        </DialogTitle>

        <Divider sx={{ my: 1 }} />

        <DialogContent>
          <Grid container spacing={2}>
            {m.id !== NIL_UUID && (
              <Grid size={6}>
                <TextField
                  fullWidth
                  disabled
                  label={t('components.memberForm.fields.memberNumber')}
                  variant="outlined"
                  value={m.memberNumber}
                />
              </Grid>
            )}
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.gender')}
                variant="outlined"
                value={m.gender}
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
                disabled={view}
                label={t('components.memberForm.fields.firstName')}
                variant="outlined"
                value={m.firstName}
                onChange={handleChange('firstName')}
                required
                error={!!errors.firstName}
                helperText={errors.firstName ?? `${m.firstName.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.middleName')}
                variant="outlined"
                value={m.middleName}
                onChange={handleChange('middleName')}
                error={!!errors.middleName}
                helperText={errors.middleName ?? `${m.middleName.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.lastName')}
                variant="outlined"
                value={m.lastName}
                onChange={handleChange('lastName')}
                required
                error={!!errors.lastName}
                helperText={errors.lastName ?? `${m.lastName.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                <DatePicker
                  label={t('components.memberForm.fields.birthday')}
                  views={['day', 'month', 'year']}
                  format="DD.MM.YYYY"
                  value={m.birthday ? dayjs(m.birthday) : null}
                  onChange={(newValue) => {
                    const dateValue = newValue ? newValue.toDate() : null;
                    handleManualChange('birthday', dateValue);
                  }}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      variant: 'outlined',
                      required: true,
                      disabled: view,
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
                disabled={view}
                label={t('components.memberForm.fields.street')}
                variant="outlined"
                value={m.street}
                onChange={handleChange('street')}
                required
                error={!!errors.street}
                helperText={errors.street ?? `${m.street.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.postalCode')}
                variant="outlined"
                value={m.postalCode}
                onChange={handleChange('postalCode')}
                required
                error={!!errors.postalCode}
                helperText={errors.postalCode ?? `${m.postalCode.length}/10`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.city')}
                variant="outlined"
                value={m.city}
                onChange={handleChange('city')}
                required
                error={!!errors.city}
                helperText={errors.city ?? `${m.city.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                  label={t('components.memberForm.fields.countryCode')}
                  fullWidth
                  required
                  value={m.countryCode}
                  onChange={handleChange('countryCode')}
                  select
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
                disabled={view}
                label={t('components.memberForm.fields.email')}
                type="email"
                variant="outlined"
                value={m.email}
                onChange={handleChange('email')}
                required
                error={!!errors.email}
                helperText={errors.email ?? `${m.email.length}/50`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.phone')}
                variant="outlined"
                value={m.phone}
                onChange={handleChange('phone')}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.bulkMail')}
                variant="outlined"
                value={m.bulkMail ?? BulkMail.ALLOWED}
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
                  value={m.startOfStudies ? dayjs(m.startOfStudies) : null}
                  onChange={(newValue) => {
                    const dateValue = newValue ? newValue.startOf('month').toDate() : null;
                    handleManualChange('startOfStudies', dateValue);
                  }}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      variant: 'outlined',
                      required: true,
                      disabled: view,
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
                  value={m.endOfStudies ? dayjs(m.endOfStudies) : null}
                  onChange={(newValue) => {
                    const dateValue = newValue ? newValue.startOf('month').toDate() : null;
                    handleManualChange('endOfStudies', dateValue);
                  }}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      variant: 'outlined',
                      disabled: view,
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
                disabled={view}
                label={t('components.memberForm.fields.academicDegree')}
                variant="outlined"
                value={m.academicDegree}
                onChange={handleChange('academicDegree')}
                select
              >
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
                disabled={view}
                label={t('components.memberForm.fields.courseOfStudy')}
                variant="outlined"
                value={m.courseOfStudy}
                onChange={handleChange('courseOfStudy')}
                error={!!errors.courseOfStudy}
                helperText={errors.courseOfStudy ?? `${m.courseOfStudy.length}/100`}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.taskWithinTheClub')}
                variant="outlined"
                value={m.taskWithinTheClub || ''}
                onChange={handleChange('taskWithinTheClub')}
                select
                required
              >
                {Object.entries(TASK_WITHIN_THE_CLUB_LABELS).map(([value, label]) => (
                  <MenuItem key={value} value={value}>
                    {t(`components.taskWithinTheClubOptions.${label}`)}
                  </MenuItem>
                ))}
              </TextField>
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.memberCategory')}
                variant="outlined"
                value={
                  !m.memberCategoryId || m.memberCategoryId === '' ? allId : m.memberCategoryId
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
              <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                <DatePicker
                  label={t('components.memberForm.fields.entryDate')}
                  views={['day', 'month', 'year']}
                  format="DD.MM.YYYY"
                  value={m.entryDate ? dayjs(m.entryDate) : null}
                  onChange={(newValue) => {
                    const dateValue = newValue ? newValue.toDate() : null;
                    handleManualChange('entryDate', dateValue);
                  }}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      variant: 'outlined',
                      required: true,
                      disabled: view,
                    },
                  }}
                />
              </LocalizationProvider>
            </Grid>
            <Grid size={6}>
              <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                <DatePicker
                  label={t('components.memberForm.fields.exitDate')}
                  views={['day', 'month', 'year']}
                  format="DD.MM.YYYY"
                  value={m.exitDate ? dayjs(m.exitDate) : null}
                  onChange={(newValue) => {
                    const dateValue = newValue ? newValue.toDate() : null;
                    handleManualChange('exitDate', dateValue);
                  }}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      variant: 'outlined',
                      disabled: view,
                      error: !!errors.exitDate,
                      helperText: errors.exitDate,
                    },
                  }}
                />
              </LocalizationProvider>
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.iban')}
                variant="outlined"
                placeholder="DE00 0000 0000 0000 0000 00"
                value={formattedIBAN}
                onChange={handleIbanChange}
                error={errors.iban !== undefined}
                helperText={errors.iban}
              />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.bic')}
                variant="outlined"
                placeholder="DEUTDEXXX"
                value={m.bic}
                onChange={handleBicChange}
                error={errors.bic !== undefined}
                helperText={errors.bic}
              />
            </Grid>
            <Grid size={6}>
              <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="de">
                <DatePicker
                  label={t('components.memberForm.fields.sepaConsent')}
                  views={['day', 'month', 'year']}
                  format="DD.MM.YYYY"
                  value={m.sepaConsent ? dayjs(m.sepaConsent) : null}
                  onChange={(newValue) => {
                    const dateValue = newValue ? newValue.toDate() : null;
                    handleManualChange('sepaConsent', dateValue);
                  }}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      variant: 'outlined',
                      disabled: view,
                      error: !!errors.sepaConsent,
                      helperText: errors.sepaConsent,
                    },
                  }}
                />
              </LocalizationProvider>
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                disabled={view}
                label={t('components.memberForm.fields.contributionPlan')}
                variant="outlined"
                value={m.contributionPlanId === null ? NIL_UUID : m.contributionPlanId}
                onChange={(event) =>
                  event.target.value !== NIL_UUID
                    ? setM({
                        ...m,
                        ['contributionPlanId']: event.target.value,
                      })
                    : null
                }
                select
                error={!!errors.contributionPlanId}
                helperText={errors.contributionPlanId}
              >
                <MenuItem value={NIL_UUID}>
                  {t('components.memberForm.fields.noContribution')}
                </MenuItem>
                {contributionPlans.map((x) => {
                  return <MenuItem value={x.id.toString()}>{x.name}</MenuItem>;
                })}
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
            {view
              ? t('components.memberForm.buttons.close')
              : t('components.memberForm.buttons.cancel')}
          </Button>
          {!view ? (
            <Button
              type="submit"
              variant="contained"
              color="primary"
              startIcon={<SaveIcon />}
              sx={{
                textTransform: 'none',
                borderRadius: 2,
                px: 3,
              }}
            >
              {t('components.memberForm.buttons.save')}
            </Button>
          ) : null}
        </DialogActions>
      </form>

      <CustomSnackbar
        status={editOrUpdateMember.status}
        message={editOrUpdateMember.message}
        onClose={() => setEditOrUpdateMember({ status: null, message: '' })}
      />
    </Dialog>
  );
}
