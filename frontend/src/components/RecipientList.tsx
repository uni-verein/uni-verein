import React, { FC, useState } from 'react';
import {
  Avatar,
  Box,
  Checkbox,
  Divider,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import PeopleAltIcon from '@mui/icons-material/PeopleAlt';
import { RecipientListProps } from '../types';
import { useTranslation } from 'react-i18next';
import { NIL as NIL_UUID } from 'uuid';
import ResponsiveTablePagination from './ResponsiveTablePagination';

const stringToColor = (str: string): string => {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = str.charCodeAt(i) + ((hash << 5) - hash);
  }

  return `hsl(${hash % 360}, 55%, 45%)`;
};

const RecipientList: FC<RecipientListProps> = ({
  recipients,
  selectedEmails,
  onChange,
  filter,
  onFilter,
  totalCount,
  memberCategories,
}) => {
  const [page, setPage] = useState(0);
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

  const toggleOne = (email: string) => {
    if (filter.categoryId !== NIL_UUID) {
      onFilter({ ...filter, categoryId: NIL_UUID });
      onChange((prev: string[]) => [email]);
    } else {
      onChange((prev: string[]) =>
        prev.includes(email) ? prev.filter((e) => e !== email) : [...prev, email],
      );
    }
  };

  return (
    <Box
      sx={{
        borderRadius: 3,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        padding: 1,
      }}
    >
      <Box
        sx={{
          px: 3,
          py: 2,
          bgcolor: 'primary.main',
          color: 'primary.contrastText',
        }}
      >
        <Stack
          direction="row"
          alignItems="center"
          justifyContent="space-between"
          flexWrap="wrap"
          gap={1}
        >
          <Stack direction="row" alignItems="center" spacing={1}>
            <PeopleAltIcon />
            <Typography>{t('pages.mail.recipientPage.recipientList')}</Typography>
          </Stack>

          <Stack direction="row" alignItems="center" spacing={1.5}>
            <TextField
              label={t('pages.mail.recipientPage.memberCategoryLabel')}
              variant="outlined"
              value={filter.categoryId ?? NIL_UUID}
              onChange={(e) => {
                if (e.target.value === NIL_UUID) {
                  onChange([]);
                }

                onFilter({
                  ...filter,
                  categoryId: e.target.value,
                });
              }}
              select
              size="small"
              SelectProps={{
                displayEmpty: true,
              }}
              InputLabelProps={{
                shrink: true,
                sx: { color: 'rgba(255,255,255,0.7)' },
              }}
              sx={{
                minWidth: 180,
                '& .MuiOutlinedInput-root': {
                  color: 'inherit',
                  '& fieldset': {
                    borderColor: 'rgba(255,255,255,0.7)',
                  },
                  '&:hover fieldset': {
                    borderColor: '#fff',
                  },
                  '&.Mui-focused fieldset': {
                    borderColor: '#fff',
                  },
                },
                '& .MuiSelect-icon': {
                  color: 'inherit',
                },
                '& .MuiInputLabel-root': {
                  color: 'rgba(255,255,255,0.7)',
                  '&.Mui-focused': {
                    color: '#fff',
                  },
                },
              }}
            >
              <MenuItem value={NIL_UUID}>{t('pages.mail.memberCategory.custom')}</MenuItem>

              {memberCategories.map((e) => {
                return (
                  <MenuItem value={e.id.toString()}>
                    {t('pages.mail.memberCategory.' + e.category)}
                  </MenuItem>
                );
              })}
            </TextField>
          </Stack>
        </Stack>
      </Box>

      <Divider />

      <TableContainer sx={{ overflowY: 'auto', overflowX: 'hidden', flexGrow: 1 }}>
        <Table size="small" stickyHeader sx={{ tableLayout: 'fixed' }}>
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: 42, px: isMobile ? 0.5 : 2 }} />
              <TableCell sx={{ width: isMobile ? 40 : 48, px: isMobile ? 0.5 : 2 }} />
              {isMobile ? (
                <TableCell>{t('pages.mail.recipientPage.member')}</TableCell>
              ) : (
                <>
                  <TableCell sx={{ width: '35%' }}>Name</TableCell>
                  <TableCell>E-Mail</TableCell>
                </>
              )}
            </TableRow>
          </TableHead>

          <TableBody>
            {recipients.length === 0 ? (
              <TableRow>
                <TableCell colSpan={isMobile ? 3 : 4}>
                  <Box sx={{ p: 4, textAlign: 'center' }}>
                    <Typography color="text.secondary">
                      {t('pages.mail.recipientPage.noRecipientFound')}
                    </Typography>
                  </Box>
                </TableCell>
              </TableRow>
            ) : (
              recipients.map((r) => {
                const isSelected = selectedEmails.includes(r.email);
                const initials = r.firstName.charAt(0).toUpperCase();

                return (
                  <TableRow
                    key={r.email}
                    hover
                    selected={isSelected}
                    onClick={() => toggleOne(r.email)}
                    sx={{
                      cursor: 'pointer',
                      '&.Mui-selected': {
                        bgcolor: 'primary.50',
                        '&:hover': { bgcolor: 'primary.100' },
                      },
                    }}
                  >
                    <TableCell padding="checkbox" sx={{ px: isMobile ? 0.5 : 2 }}>
                      <Checkbox
                        checked={isSelected}
                        color="primary"
                        onClick={(e) => e.stopPropagation()}
                        onChange={() => toggleOne(r.email)}
                      />
                    </TableCell>
                    <TableCell sx={{ px: isMobile ? 0.5 : 2, pr: 0 }}>
                      <Avatar
                        sx={{
                          bgcolor: stringToColor(`${r.firstName} ${r.lastName}`),
                          width: isMobile ? 30 : 38,
                          height: isMobile ? 30 : 38,
                          fontSize: isMobile ? 13 : 16,
                          fontWeight: 700,
                        }}
                      >
                        {initials}
                      </Avatar>
                    </TableCell>
                    {(() => {
                      const nameLabel = (
                        <Typography
                          variant="body2"
                          fontWeight={isSelected ? 600 : 400}
                          color="text.primary"
                          noWrap
                        >
                          {r.firstName} {r.lastName}
                        </Typography>
                      );
                      const emailLabel = (
                        <Typography
                          variant={isMobile ? 'caption' : 'body2'}
                          color="text.secondary"
                          noWrap
                          component="div"
                        >
                          {r.email}
                        </Typography>
                      );

                      return isMobile ? (
                        <TableCell sx={{ minWidth: 0 }}>
                          {nameLabel}
                          {emailLabel}
                        </TableCell>
                      ) : (
                        <>
                          <TableCell sx={{ width: '35%' }}>{nameLabel}</TableCell>
                          <TableCell>{emailLabel}</TableCell>
                        </>
                      );
                    })()}
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>

        <ResponsiveTablePagination
          component="div"
          count={totalCount}
          rowsPerPage={filter.limit}
          page={page}
          onPageChange={(_, newPage) => {
            setPage(newPage);
            onFilter({ ...filter, offset: newPage * filter.limit });
          }}
          onRowsPerPageChange={(e) => {
            onFilter({ ...filter, limit: parseInt(e.target.value, 10), offset: 0 });
            setPage(0);
          }}
          labelRowsPerPage={t('pages.mail.recipientPage.rowsPerPage')}
        />
      </TableContainer>
    </Box>
  );
};

export default RecipientList;
