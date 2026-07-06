import React, { useEffect, useState } from 'react';
import { SvgIconComponent } from '@mui/icons-material';

export async function loadIconNames(): Promise<string[]> {
  const MuiIcons = await import('@mui/icons-material');
  return Object.keys(MuiIcons).filter((key) => !key.endsWith('Outlined') && key !== 'default');
}

export function DynamicIcon({
  name,
  ...props
}: { name: string } & React.ComponentProps<SvgIconComponent>) {
  const [Icon, setIcon] = useState<SvgIconComponent | null>(null);

  useEffect(() => {
    if (!name) return;
    import('@mui/icons-material').then((MuiIcons) => {
      const icon = (MuiIcons as Record<string, unknown>)[name] as SvgIconComponent | undefined;
      setIcon(() => icon ?? null);
    });
  }, [name]);

  if (!Icon) return null;
  return <Icon {...props} />;
}
