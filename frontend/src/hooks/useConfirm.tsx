import { useState, useCallback, useRef } from 'react';

export function useConfirm() {
  const [open, setOpen] = useState(false);
  // @ts-expect-error - useRef requires an initial value argument under strict mode
  const resolveRef = useRef<(value: boolean) => void>();

  const confirm = useCallback((): Promise<boolean> => {
    return new Promise((res) => {
      resolveRef.current = res;
      setOpen(true);
    });
  }, []);

  const handleClose = useCallback((value: boolean) => {
    setOpen(false);
    resolveRef.current?.(value);
  }, []);

  return { open, confirm, handleClose };
}
