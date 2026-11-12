import { Box } from '@mui/material';
import type { SxProps, Theme } from '@mui/material';
import { richTextContentSx } from './richTextExtensions';

export default function RichTextView({ html, sx }: { html: string; sx?: SxProps<Theme> }) {
  return (
    <Box
      sx={[richTextContentSx, ...(Array.isArray(sx) ? sx : sx ? [sx] : [])]}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
