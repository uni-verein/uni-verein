import React, { useRef, useEffect, useState } from 'react';
import { useEditor, EditorContent } from '@tiptap/react';

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
import CloseIcon from '@mui/icons-material/Close';
import MailOutlineIcon from '@mui/icons-material/MailOutlined';
import { EmailEditorProps } from '../types';
import { useTranslation } from 'react-i18next';
import { useSnackbar } from '../hooks/useSnackbar';
import RichTextToolbar from './richText/RichTextToolbar';
import { createRichTextExtensions, richTextContentSx } from './richText/richTextExtensions';

const extensions = createRichTextExtensions({ images: true });

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
    extensions,
    content:
      initialContent !== '' ? initialContent : t('pages.mail.editorPage.editorDefaultContent'),
    onUpdate: ({ editor }) => {
      onContentChange?.(editor.getHTML());
    },
  });

  useEffect(() => {
    if (!editor || editor.isDestroyed) return;

    if (initialContent !== '' && initialContent !== undefined) {
      if (editor.getHTML() !== initialContent) {
        editor.commands.setContent(initialContent);
      }
    }

    const html = editor.getHTML();
    if ((html.includes('{fullname}') || html.includes('{firstname}')) && recipientCount > 150) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setInformation(t('pages.mail.editorPage.info'));
      setDisableSendButton(true);
    } else {
      setInformation(null);
      setDisableSendButton(false);
    }
  }, [initialContent, editor, recipientCount, t]);

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
    const htmlBody = editor && !editor.isDestroyed ? editor.getHTML() : '';
    if (!htmlBody || htmlBody === '<p></p>') {
      setSnackbar({ status: 'error', message: t('pages.mail.editorPage.bodyMissing') });
      return;
    }
    onSend({ subject, htmlBody, attachments });
  };

  if (!editor || editor.isDestroyed) return null;

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
      <Box sx={{ mb: 2 }}>
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

      <RichTextToolbar
        editor={editor}
        extras={
          <>
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
                  <input
                    ref={fileInputRef}
                    type="file"
                    multiple
                    hidden
                    onChange={handleAttachment}
                  />
                </IconButton>
              </span>
            </Tooltip>
          </>
        }
      />

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
            ...richTextContentSx,
          },
        }}
      >
        <EditorContent editor={editor} />
      </Paper>

      {attachments.length > 0 && (
        <Box sx={{ mb: 2 }}>
          <Typography variant="subtitle2" gutterBottom>
            {t('pages.mail.editorPage.attachments.sectionTitle')}
          </Typography>
          <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1 }}>
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
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 2,
        }}
      >
        <Stack direction="column" spacing={0.5}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
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
