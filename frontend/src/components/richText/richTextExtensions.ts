import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Link from '@tiptap/extension-link';
import TextAlign from '@tiptap/extension-text-align';
import { TextStyle } from '@tiptap/extension-text-style';
import Color from '@tiptap/extension-color';

export function createRichTextExtensions({ images }: { images: boolean }) {
  return [
    StarterKit,
    ...(images ? [Image.configure({ inline: true, allowBase64: true })] : []),
    Link.configure({ openOnClick: false }),
    TextAlign.configure({ types: ['heading', 'paragraph'] }),
    TextStyle,
    Color,
  ];
}

export const richTextContentSx = {
  fontSize: '0.95rem',
  lineHeight: 1.7,
  '& p': { margin: '0.3em 0' },
  '& h1': { fontSize: '1.6rem', fontWeight: 700 },
  '& h2': { fontSize: '1.3rem', fontWeight: 600 },
  '& img': { maxWidth: '100%' },
};
