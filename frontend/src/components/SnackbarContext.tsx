import React, { createContext, useContext, useState, Dispatch, SetStateAction } from 'react';
import { CustomSnackbar } from './CustomSnackbar';
import { SNACKBAR_INITIAL_STATE, SnackbarState } from '../types';

const SnackbarContext = createContext<Dispatch<SetStateAction<SnackbarState>>>(null!);

export const SnackbarProvider = ({ children }: any) => {
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

export const useSnackbar = () => useContext(SnackbarContext);
