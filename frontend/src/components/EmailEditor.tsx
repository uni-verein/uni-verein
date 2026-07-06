import React, { useRef, useEffect, useState } from 'react';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Link from '@tiptap/extension-link';
import TextAlign from '@tiptap/extension-text-align';
import { TextStyle } from '@tiptap/extension-text-style';
import Color from '@tiptap/extension-color';

import {
  Box,
  TextField,
  Button,
  IconButton,
  Tooltip,
  Typography,
  Chip,
  Stack,
  Divider,
  Paper,
  Alert,
} from '@mui/material';
import SendIcon from '@mui/icons-material/Send';
import AttachFileIcon from '@mui/icons-material/AttachFile';
import ImageIcon from '@mui/icons-material/Image';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatUnderlinedIcon from '@mui/icons-material/FormatUnderlined';
import FormatAlignLeftIcon from '@mui/icons-material/FormatAlignLeft';
import FormatAlignCenterIcon from '@mui/icons-material/FormatAlignCenter';
import FormatAlignRightIcon from '@mui/icons-material/FormatAlignRight';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import CloseIcon from '@mui/icons-material/Close';
import MailOutlineIcon from '@mui/icons-material/MailOutline';
import { EmailEditorProps } from '../types';
import { useTranslation } from 'react-i18next';
import { useSnackbar } from './SnackbarContext';

export default function EmailEditor({
  onSend,
  isSending,
  recipientCount,
  subject,
  onSubjectChange,
  attachments,
  onAttachmentsChange,
  initialContent,
  onContentChange,
}: EmailEditorProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const setSnackbar = useSnackbar();
  const { t } = useTranslation();
  const [disableSendButton, setDisableSendButton] = useState(false);
  const [information, setInformation] = useState<string | null>();

  const editor = useEditor({
    extensions: [
      StarterKit,
      Image.configure({ inline: true, allowBase64: true }),
      Link.configure({ openOnClick: false }),
      TextAlign.configure({ types: ['heading', 'paragraph'] }),
      TextStyle,
      Color,
    ],
    content:
      initialContent !== '' ? initialContent : t('pages.mail.editorPage.editorDefaultContent'),
    onUpdate: ({ editor }) => {
      onContentChange?.(editor.getHTML());
    },
  });

  useEffect(() => {
    if (editor && initialContent !== '' && initialContent !== undefined) {
      if (editor.getHTML() !== initialContent) {
        editor.commands.setContent(initialContent);
      }
    }

    if (
      (editor?.getHTML().includes('{fullname}') || editor?.getHTML().includes('{firstname}')) &&
      recipientCount > 150
    ) {
      setInformation(t('pages.mail.editorPage.info'));
      setDisableSendButton(true);
    } else {
      setInformation(null);
      setDisableSendButton(false);
    }
  }, [initialContent, editor]);

  const readFileAsBase64 = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const base64 = (reader.result as string).split(',')[1];
        resolve(base64);
      };
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  };

  const handleInlineImage = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    for (const file of files) {
      const base64 = await readFileAsBase64(file);
      const dataUrl = `data:${file.type};base64,${base64}`;
      editor?.chain().focus().setImage({ src: dataUrl }).run();
      const contentId = `img_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

      onAttachmentsChange([
        ...attachments,
        {
          fileName: file.name,
          base64Content: base64,
          contentType: file.type,
          isInline: true,
          contentId,
        },
      ]);
    }
    e.target.value = '';
  };

  const handleAttachment = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    const newAttachments = [...attachments];

    for (const file of files) {
      const base64 = await readFileAsBase64(file);
      newAttachments.push({
        fileName: file.name,
        base64Content: base64,
        contentType: file.type,
        isInline: false,
        contentId: '',
      });
    }

    onAttachmentsChange(newAttachments);
    e.target.value = '';
  };

  const removeAttachment = (index: number) => {
    onAttachmentsChange(attachments.filter((_, i) => i !== index));
  };

  const handleSend = () => {
    if (!subject.trim()) {
      setSnackbar({ status: 'error', message: t('pages.mail.editorPage.subjectMissing') });
      return;
    }
    const htmlBody = editor?.getHTML() ?? '';
    if (!htmlBody || htmlBody === '<p></p>') {
      setSnackbar({ status: 'error', message: t('pages.mail.editorPage.bodyMissing') });
      return;
    }
    onSend({ subject, htmlBody, attachments });
  };

  if (!editor) return null;

  const toolbarBtnSx = (active: boolean) => ({
    border: active ? '1px solid' : '1px solid transparent',
    borderColor: active ? 'primary.main' : 'transparent',
    bgcolor: active ? 'primary.50' : 'transparent',
    color: active ? 'primary.main' : 'text.secondary',
    borderRadius: 1,
  });

  return (
    <Box
      sx={{
        borderRadius: 3,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        padding: 1,
      }}
    >
      {information && (
        <Alert severity="error" onClose={() => setInformation(null)} sx={{ mb: 2 }}>
          {information}
        </Alert>
      )}
      <Box mb={2}>
        <TextField
          fullWidth
          label={t('pages.mail.editorPage.subjectLabel')}
          placeholder={t('pages.mail.editorPage.subjectPlaceholder')}
          value={subject}
          onChange={(e) => onSubjectChange(e.target.value)}
          disabled={isSending}
          variant="outlined"
          size="small"
        />
      </Box>

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
          bgcolor: 'grey.50',
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
              onClick={() => (editor.chain().focus() as any).toggleUnderline?.().run()}
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
              <Typography variant="caption" fontWeight="bold">
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
              <Typography variant="caption" fontWeight="bold">
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

        <Divider orientation="vertical" flexItem />

        <Stack direction="row" spacing={0.5}>
          <Tooltip title={t('pages.mail.editorPage.toolbar.embedImage')}>
            <span>
              <IconButton size="small" component="label" sx={{ color: 'text.secondary' }}>
                <ImageIcon fontSize="small" />
                <input
                  ref={imageInputRef}
                  type="file"
                  accept="image/*"
                  multiple
                  hidden
                  onChange={handleInlineImage}
                />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t('pages.mail.editorPage.toolbar.attachFile')}>
            <span>
              <IconButton size="small" component="label" sx={{ color: 'text.secondary' }}>
                <AttachFileIcon fontSize="small" />
                <input ref={fileInputRef} type="file" multiple hidden onChange={handleAttachment} />
              </IconButton>
            </span>
          </Tooltip>
        </Stack>

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

      <Paper
        variant="outlined"
        sx={{
          minHeight: 280,
          p: 2,
          borderRadius: 2,
          mb: 2,
          '& .ProseMirror': {
            outline: 'none',
            minHeight: 240,
            fontSize: '0.95rem',
            lineHeight: 1.7,
            '& p': { margin: '0.3em 0' },
            '& h1': { fontSize: '1.6rem', fontWeight: 700 },
            '& h2': { fontSize: '1.3rem', fontWeight: 600 },
          },
        }}
      >
        <EditorContent editor={editor} />
      </Paper>

      {attachments.length > 0 && (
        <Box mb={2}>
          <Typography variant="subtitle2" gutterBottom>
            {t('pages.mail.editorPage.attachments.sectionTitle')}
          </Typography>
          <Stack direction="row" flexWrap="wrap" gap={1}>
            {attachments.map((att, i) => (
              <Chip
                key={i}
                icon={
                  att.isInline ? (
                    <ImageIcon fontSize="small" />
                  ) : (
                    <AttachFileIcon fontSize="small" />
                  )
                }
                label={`${att.fileName} (${att.isInline ? t('pages.mail.editorPage.attachments.inline') : t('pages.mail.editorPage.attachments.attachment')})`}
                onDelete={() => removeAttachment(i)}
                deleteIcon={<CloseIcon />}
                variant="outlined"
                size="small"
                color={att.isInline ? 'info' : 'default'}
              />
            ))}
          </Stack>
        </Box>
      )}

      <Divider sx={{ mb: 2 }} />

      <Box
        display="flex"
        alignItems="center"
        justifyContent="space-between"
        flexWrap="wrap"
        gap={2}
      >
        <Stack direction="column" spacing={0.5}>
          <Stack direction="row" spacing={1} alignItems="center">
            <MailOutlineIcon fontSize="small" color="action" />
            <Typography variant="body2" color="text.secondary">
              {t('pages.mail.editorPage.recipients.selected', { count: recipientCount })}
            </Typography>
          </Stack>
          <Typography variant="caption" color="text.disabled">
            {t('pages.mail.editorPage.recipients.tip')}
          </Typography>
        </Stack>

        <Button
          variant="contained"
          size="large"
          endIcon={<SendIcon />}
          onClick={handleSend}
          disabled={isSending || recipientCount === 0 || disableSendButton}
          sx={{ borderRadius: 2, px: 4, fontWeight: 600 }}
        >
          {isSending
            ? t('pages.mail.editorPage.send.sending')
            : t('pages.mail.editorPage.send.button', { count: recipientCount })}
        </Button>
      </Box>
    </Box>
  );
}
