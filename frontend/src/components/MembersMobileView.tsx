import { Box, Chip, CircularProgress, IconButton, Tooltip, Typography } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import VisibilityIcon from '@mui/icons-material/Visibility';
import RestoreFromTrashIcon from '@mui/icons-material/RestoreFromTrash';
import { useTranslation } from 'react-i18next';
import { ContributionPlans, Member, MemberCategory, TaskWithinTheClub } from '../types';
import { TASK_WITHIN_THE_CLUB_LABELS } from '../utils';
import { MobileListCard } from './MobileListCard';
import { UUIDTypes } from 'uuid';
import ResponsiveTablePagination from './ResponsiveTablePagination';

export function MembersMobileView({
  members,
  contributionPlans,
  memberCategories,
  loading,
  totalCount,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  onView,
  onEdit,
  onDelete,
  onRestore,
}: {
  members: Member[];
  contributionPlans: ContributionPlans[];
  memberCategories: MemberCategory[];
  loading: boolean;
  totalCount: number;
  page: number;
  rowsPerPage: number;
  onPageChange: (page: number) => void;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  onView: (member: Member) => void;
  onEdit: (member: Member) => void;
  onDelete: (id: UUIDTypes) => void;
  onRestore: (id: UUIDTypes) => void;
}) {
  const { t } = useTranslation();

  const getTaskLabel = (task: string): string => {
    return TASK_WITHIN_THE_CLUB_LABELS[task as TaskWithinTheClub] || 'unknownTask';
  };

  const getCategoryTranslation = (category: MemberCategory | undefined) => {
    if (category === undefined) return '';

    const translationKey = `pages.members.filter.${category.category}`;
    return t(translationKey).startsWith(translationKey) ? category.name : t(translationKey);
  };

  return (
    <Box sx={{ position: 'relative' }}>
      {loading && (
        <Box
          sx={{
            position: 'absolute',
            top: 16,
            left: '50%',
            transform: 'translateX(-50%)',
            zIndex: 1,
          }}
        >
          <CircularProgress size={28} />
        </Box>
      )}
      {members.length === 0 ? (
        <Typography align="center" color="text.secondary" sx={{ py: 3 }}>
          {t('pages.members.table.noMembers')}
        </Typography>
      ) : (
        members.map((m) => (
          <MobileListCard
            key={m.id.toString()}
            primary={
              <Box>
                <Typography sx={{ fontWeight: 600 }}>
                  {m.firstName} {m.middleName} {m.lastName}
                </Typography>
                <Chip
                  label={m.memberNumber}
                  size="small"
                  variant="outlined"
                  sx={{ fontWeight: 500, mt: 0.5 }}
                />
              </Box>
            }
            secondaryRows={[
              {
                label: t('pages.members.table.colTask'),
                value: t(
                  `components.taskWithinTheClubOptions.${getTaskLabel(m.taskWithinTheClub)}`,
                ),
              },
              { label: t('pages.members.table.colEmail'), value: m.email },
              {
                label: t('pages.members.table.colContribution'),
                value:
                  m.contributionPlanId !== null
                    ? `${contributionPlans.find((x) => x.id === m.contributionPlanId)?.amount} €`
                    : t('pages.members.table.noContribution'),
              },
              {
                label: t('pages.members.table.colMemberCategory'),
                value:
                  m.memberCategoryId !== null && m.memberCategoryId !== undefined
                    ? getCategoryTranslation(
                        memberCategories.find((x) => x.id === m.memberCategoryId),
                      )
                    : '',
              },
            ]}
            actions={
              <>
                {m.deletedAt !== null && (
                  <Tooltip title={t('pages.members.actions.restore')}>
                    <IconButton onClick={() => onRestore(m.id)} size="small" color="primary">
                      <RestoreFromTrashIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                {m.deletedAt === null && (
                  <Tooltip title={t('pages.members.actions.view')}>
                    <IconButton onClick={() => onView(m)} size="small" color="primary">
                      <VisibilityIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                {m.deletedAt === null && (
                  <Tooltip title={t('pages.members.actions.edit')}>
                    <IconButton onClick={() => onEdit(m)} size="small" color="primary">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                <Tooltip title={t('pages.members.actions.delete')}>
                  <IconButton onClick={() => onDelete(m.id)} size="small" color="error">
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </>
            }
          />
        ))
      )}
      <ResponsiveTablePagination
        component="div"
        count={totalCount}
        rowsPerPage={rowsPerPage}
        page={page}
        onPageChange={(_, newPage) => onPageChange(newPage)}
        onRowsPerPageChange={(e) => onRowsPerPageChange(parseInt(e.target.value, 10))}
        labelRowsPerPage={t('pages.members.table.rowsPerPage')}
      />
    </Box>
  );
}
