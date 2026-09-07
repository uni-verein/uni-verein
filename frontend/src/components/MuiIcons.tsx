import React, { useEffect, useState } from 'react';
import type { SvgIconComponent } from '@mui/icons-material';
import { loadAllIcons } from '../utils/iconLoader';

export function DynamicIcon({
  name,
  ...props
}: { name: string } & React.ComponentProps<SvgIconComponent>) {
  const [Icon, setIcon] = useState<SvgIconComponent | null>(null);

  useEffect(() => {
    if (!name) return;
    let cancelled = false;
    loadAllIcons().then((MuiIcons) => {
      if (cancelled) return;
      const icon = MuiIcons[name];
      setIcon(() => icon ?? null);
    });
    return () => {
      cancelled = true;
    };
  }, [name]);

  if (!Icon) return null;
  return <Icon {...props} />;
}
