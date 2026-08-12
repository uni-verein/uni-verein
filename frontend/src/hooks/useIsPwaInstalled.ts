import { useEffect, useState } from 'react';

function detectPwaInstalled(): boolean {
  const isStandaloneDisplay =
    typeof window !== 'undefined' && window.matchMedia?.('(display-mode: standalone)').matches;
  const isIosStandalone = (navigator as any).standalone === true;
  return Boolean(isStandaloneDisplay || isIosStandalone);
}

export function useIsPwaInstalled(): boolean {
  const [installed, setInstalled] = useState(detectPwaInstalled);

  useEffect(() => {
    const mediaQuery = window.matchMedia('(display-mode: standalone)');
    const handler = () => setInstalled(detectPwaInstalled());
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, []);

  return installed;
}
