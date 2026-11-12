import React from 'react';
import type { Editor } from '@tiptap/react';
import { Box, Divider, IconButton, Paper, Stack, Tooltip, Typography } from '@mui/material';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatUnderlinedIcon from '@mui/icons-material/FormatUnderlined';
import FormatAlignLeftIcon from '@mui/icons-material/FormatAlignLeft';
import FormatAlignCenterIcon from '@mui/icons-material/FormatAlignCenter';
import FormatAlignRightIcon from '@mui/icons-material/FormatAlignRight';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import { useTranslation } from 'react-i18next';

const toolbarBtnSx = (active: boolean) => ({
  border: active ? '1px solid' : '1px solid transparent',
  borderColor: active ? 'primary.main' : 'transparent',
  bgcolor: active ? 'primary.50' : 'transparent',
  color: active ? 'primary.main' : 'text.secondary',
  borderRadius: 1,
});

export default function RichTextToolbar({
  editor,
  extras,
}: {
  editor: Editor;
  extras?: React.ReactNode;
}) {
  const { t } = useTranslation();

  return (
    <Paper
      variant="outlined"
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: 0.5,
        p: 1,
        mb: 1,
        borderRadius: 2,
        bgcolor: 'action.hover',
      }}
    >
      <Stack direction="row" spacing={0.5}>
        <Tooltip title={t('pages.mail.editorPage.toolbar.bold')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleBold().run()}
            sx={toolbarBtnSx(editor.isActive('bold'))}
          >
            <FormatBoldIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.italic')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleItalic().run()}
            sx={toolbarBtnSx(editor.isActive('italic'))}
          >
            <FormatItalicIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.underline')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleUnderline().run()}
            sx={toolbarBtnSx(false)}
          >
            <FormatUnderlinedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      <Divider orientation="vertical" flexItem />

      <Stack direction="row" spacing={0.5}>
        <Tooltip title={t('pages.mail.editorPage.toolbar.heading1')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleHeading({ level: 1 }).run()}
            sx={toolbarBtnSx(editor.isActive('heading', { level: 1 }))}
          >
            <Typography variant="caption" sx={{ fontWeight: 'bold' }}>
              H1
            </Typography>
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.heading2')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
            sx={toolbarBtnSx(editor.isActive('heading', { level: 2 }))}
          >
            <Typography variant="caption" sx={{ fontWeight: 'bold' }}>
              H2
            </Typography>
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.bulletList')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleBulletList().run()}
            sx={toolbarBtnSx(editor.isActive('bulletList'))}
          >
            <FormatListBulletedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.orderedList')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().toggleOrderedList().run()}
            sx={toolbarBtnSx(editor.isActive('orderedList'))}
          >
            <FormatListNumberedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      <Divider orientation="vertical" flexItem />

      <Stack direction="row" spacing={0.5}>
        <Tooltip title={t('pages.mail.editorPage.toolbar.alignLeft')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().setTextAlign('left').run()}
            sx={toolbarBtnSx(editor.isActive({ textAlign: 'left' }))}
          >
            <FormatAlignLeftIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.alignCenter')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().setTextAlign('center').run()}
            sx={toolbarBtnSx(editor.isActive({ textAlign: 'center' }))}
          >
            <FormatAlignCenterIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t('pages.mail.editorPage.toolbar.alignRight')}>
          <IconButton
            size="small"
            onClick={() => editor.chain().focus().setTextAlign('right').run()}
            sx={toolbarBtnSx(editor.isActive({ textAlign: 'right' }))}
          >
            <FormatAlignRightIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      {extras && (
        <>
          <Divider orientation="vertical" flexItem />
          <Stack direction="row" spacing={0.5}>
            {extras}
          </Stack>
        </>
      )}

      <Divider orientation="vertical" flexItem />

      <Tooltip title={t('pages.mail.editorPage.toolbar.textColor')}>
        <Box
          component="input"
          type="color"
          onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
            editor.chain().focus().setColor(e.target.value).run()
          }
          sx={{
            width: 32,
            height: 32,
            border: 'none',
            borderRadius: 1,
            cursor: 'pointer',
            p: 0,
            bgcolor: 'transparent',
          }}
        />
      </Tooltip>
    </Paper>
  );
}
