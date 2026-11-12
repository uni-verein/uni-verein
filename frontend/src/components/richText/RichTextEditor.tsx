import { useEffect, useMemo } from 'react';
import { EditorContent, useEditor } from '@tiptap/react';
import { Paper } from '@mui/material';
import RichTextToolbar from './RichTextToolbar';
import { createRichTextExtensions, richTextContentSx } from './richTextExtensions';

export default function RichTextEditor({
  value,
  onChange,
  minHeight = 200,
}: {
  value: string;
  onChange: (html: string) => void;
  minHeight?: number;
}) {
  const extensions = useMemo(() => createRichTextExtensions({ images: false }), []);

  const editor = useEditor({
    extensions,
    content: value,
    onUpdate: ({ editor }) => {
      onChange(editor.isEmpty ? '' : editor.getHTML());
    },
  });

  useEffect(() => {
    if (!editor || editor.isDestroyed) return;
    const current = editor.isEmpty ? '' : editor.getHTML();
    if (current !== value) editor.commands.setContent(value, { emitUpdate: false });
  }, [editor, value]);

  if (!editor || editor.isDestroyed) return null;

  return (
    <>
      <RichTextToolbar editor={editor} />
      <Paper
        variant="outlined"
        sx={{
          p: 2,
          borderRadius: 2,
          '& .ProseMirror': {
            outline: 'none',
            minHeight,
            ...richTextContentSx,
          },
        }}
      >
        <EditorContent editor={editor} />
      </Paper>
    </>
  );
}
