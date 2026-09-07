import type { SvgIconComponent } from '@mui/icons-material';

let muiIconsPromise: Promise<Record<string, SvgIconComponent>> | null = null;

export function loadAllIcons(): Promise<Record<string, SvgIconComponent>> {
  if (!muiIconsPromise) {
    muiIconsPromise = import('@mui/icons-material') as Promise<Record<string, SvgIconComponent>>;
  }
  return muiIconsPromise;
}
