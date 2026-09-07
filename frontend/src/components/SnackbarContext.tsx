import { useState, ReactNode } from 'react';
import { CustomSnackbar } from './CustomSnackbar';
import { SNACKBAR_INITIAL_STATE, SnackbarState } from '../types';
import { SnackbarContext } from '../hooks/useSnackbar';

export const SnackbarProvider = ({ children }: { children: ReactNode }) => {
  const [deleteOrUpdateMember, setDeleteOrUpdateMember] =
    useState<SnackbarState>(SNACKBAR_INITIAL_STATE);

  return (
    <SnackbarContext.Provider value={setDeleteOrUpdateMember}>
      {children}
      <CustomSnackbar
        status={deleteOrUpdateMember.status}
        message={deleteOrUpdateMember.message}
        onClose={() => setDeleteOrUpdateMember({ status: null, message: '' })}
      />
    </SnackbarContext.Provider>
  );
};
