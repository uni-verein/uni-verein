import { createContext, useContext, Dispatch, SetStateAction } from 'react';
import { SnackbarState } from '../types';

export const SnackbarContext = createContext<Dispatch<SetStateAction<SnackbarState>>>(null!);

export const useSnackbar = () => useContext(SnackbarContext);
