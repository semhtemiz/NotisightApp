import React, { useEffect, useState, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Extension, Node as TiptapNode, mergeAttributes } from '@tiptap/core';
import { DOMParser as ProseMirrorDOMParser } from '@tiptap/pm/model';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet } from '@tiptap/pm/view';
import { useEditor, EditorContent, NodeViewWrapper, ReactNodeViewRenderer } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Placeholder from '@tiptap/extension-placeholder';
import Link from '@tiptap/extension-link';
import Underline from '@tiptap/extension-underline';
import Highlight from '@tiptap/extension-highlight';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import Image from '@tiptap/extension-image';
import { Table } from '@tiptap/extension-table';
import TableRow from '@tiptap/extension-table-row';
import TableCell from '@tiptap/extension-table-cell';
import TableHeader from '@tiptap/extension-table-header';
import { 
  Bold, Italic, Strikethrough, Underline as UnderlineIcon, Highlighter,
  Heading1, Heading2, List, ListOrdered, CheckSquare, Quote, Code, 
  Sparkles, Wand2, HelpCircle, Table2, Workflow,
  Rows3, Columns3, Trash2, Plus, Minus
} from 'lucide-react';
import type { Note } from '../types';
import { apiClient } from '../utils/apiClient';
import { buildApiUrl } from '../utils/apiConfig';

interface EditorProps {
  note: Note;
  onUpdate: (id: string, updates: Partial<Note>) => void;
  folderPathStr?: string;
}

const SLASH_MENU_MAX_HEIGHT = 360;

const DEFAULT_MERMAID_CODE = `flowchart TD
  A[Başlangıç] --> B{Karar}
  B -->|Evet| C[Uygula]
  B -->|Hayır| D[Revize et]`;

type InlineAiPreviewDecoration = {
  from: number;
  to: number;
  text: string;
} | null;

const inlineAiPreviewPluginKey = new PluginKey<InlineAiPreviewDecoration>('inlineAiPreview');

const InlineAiPreview = Extension.create({
  name: 'inlineAiPreview',

  addProseMirrorPlugins() {
    return [
      new Plugin<InlineAiPreviewDecoration>({
        key: inlineAiPreviewPluginKey,
        state: {
          init: () => null,
          apply: (transaction, value) => {
            const meta = transaction.getMeta(inlineAiPreviewPluginKey);
            if (meta !== undefined) {
              return meta;
            }

            if (!value || !transaction.docChanged) {
              return value;
            }

            return {
              ...value,
              from: transaction.mapping.map(value.from),
              to: transaction.mapping.map(value.to),
            };
          },
        },
        props: {
          decorations(state) {
            const preview = inlineAiPreviewPluginKey.getState(state);
            if (!preview?.text) return null;

            const widget = document.createElement('span');
            widget.className = 'notisight-ai-ghost-preview';
            widget.textContent = preview.text;

            return DecorationSet.create(state.doc, [
              Decoration.widget(preview.to, widget, { side: 1, key: 'inline-ai-preview' }),
            ]);
          },
        },
      }),
    ];
  },
});

const isTiptapEditorReady = (editor: any) =>
  Boolean(editor && !editor.isDestroyed && editor.view && editor.state?.doc && editor.schema?.nodes);

const setInlineAiPreviewDecoration = (editor: any, preview: InlineAiPreviewDecoration) => {
  if (!isTiptapEditorReady(editor)) return;
  editor.view.dispatch(editor.state.tr.setMeta(inlineAiPreviewPluginKey, preview));
};

const escapeHtml = (value: string) =>
  value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');

const escapeAttribute = (value: string) => escapeHtml(value).replace(/\n/g, '&#10;');

const parseMarkdownTableRow = (line: string) => {
  const trimmed = line.trim().replace(/^\|/, '').replace(/\|$/, '');
  return trimmed.split('|').map(cell => cell.trim());
};

const isMarkdownTableSeparator = (line: string) => {
  const cells = parseMarkdownTableRow(line);
  return cells.length > 1 && cells.every(cell => /^:?-{3,}:?$/.test(cell));
};

const looksLikeMarkdown = (text: string) => {
  if (!text.trim()) return false;

  return [
    /^#{1,6}\s+\S/m,
    /^\s*[-*+]\s+\S/m,
    /^\s*\d+[.)]\s+\S/m,
    /^\s*[-*+]\s+\[[ xX]\]\s+\S/m,
    /^\s*>\s+\S/m,
    /^```[\s\S]*```/m,
    /^\|.+\|\s*\n\s*\|?\s*:?-{3,}:?/m,
    /\*\*[^*\n]+\*\*/,
    /__[^_\n]+__/,
    /~~[^~\n]+~~/,
    /`[^`\n]+`/,
    /\[[^\]\n]+\]\([^)]+\)/
  ].some(pattern => pattern.test(text));
};

const renderInlineMarkdown = (value: string) => {
  let html = escapeHtml(value);

  html = html.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g, (_match, alt, src) => {
    return `<img src="${escapeAttribute(src)}" alt="${escapeAttribute(alt)}" />`;
  });
  html = html.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g, (_match, label, href) => {
    return `<a href="${escapeAttribute(href)}">${label}</a>`;
  });
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/__([^_]+)__/g, '<strong>$1</strong>');
  html = html.replace(/~~([^~]+)~~/g, '<s>$1</s>');
  html = html.replace(/(^|[^*])\*([^*\n]+)\*/g, '$1<em>$2</em>');
  html = html.replace(/(^|[^_])_([^_\n]+)_/g, '$1<em>$2</em>');

  return html;
};

const markdownToEditorHtml = (markdown: string) => {
  const lines = markdown.replace(/\r\n?/g, '\n').split('\n');
  const blocks: string[] = [];
  let index = 0;

  while (index < lines.length) {
    const line = lines[index];

    if (!line.trim()) {
      index += 1;
      continue;
    }

    const fenceMatch = line.match(/^```(\w+)?\s*$/);
    if (fenceMatch) {
      const language = (fenceMatch[1] || '').toLowerCase();
      const codeLines: string[] = [];
      index += 1;

      while (index < lines.length && !/^```\s*$/.test(lines[index])) {
        codeLines.push(lines[index]);
        index += 1;
      }
      if (index < lines.length) index += 1;

      const code = codeLines.join('\n');
      if (language === 'mermaid') {
        blocks.push(`<div data-type="mermaid-diagram" data-code="${escapeAttribute(code || DEFAULT_MERMAID_CODE)}"></div>`);
      } else {
        blocks.push(`<pre><code>${escapeHtml(code)}</code></pre>`);
      }
      continue;
    }

    if (/^---+$/.test(line.trim())) {
      blocks.push('<hr />');
      index += 1;
      continue;
    }

    const headingMatch = line.match(/^(#{1,6})\s+(.+)$/);
    if (headingMatch) {
      const level = Math.min(headingMatch[1].length, 6);
      blocks.push(`<h${level}>${renderInlineMarkdown(headingMatch[2])}</h${level}>`);
      index += 1;
      continue;
    }

    const taskMatch = line.match(/^\s*[-*+]\s+\[([ xX])\]\s+(.+)$/);
    if (taskMatch) {
      const items: string[] = [];
      while (index < lines.length) {
        const match = lines[index].match(/^\s*[-*+]\s+\[([ xX])\]\s+(.+)$/);
        if (!match) break;
        const checked = match[1].toLowerCase() === 'x';
        items.push(`<li data-type="taskItem" data-checked="${checked ? 'true' : 'false'}"><p>${renderInlineMarkdown(match[2])}</p></li>`);
        index += 1;
      }
      blocks.push(`<ul data-type="taskList">${items.join('')}</ul>`);
      continue;
    }

    const unorderedMatch = line.match(/^\s*[-*+]\s+(.+)$/);
    if (unorderedMatch) {
      const items: string[] = [];
      while (index < lines.length) {
        const match = lines[index].match(/^\s*[-*+]\s+(.+)$/);
        if (!match) break;
        items.push(`<li><p>${renderInlineMarkdown(match[1])}</p></li>`);
        index += 1;
      }
      blocks.push(`<ul>${items.join('')}</ul>`);
      continue;
    }

    const orderedMatch = line.match(/^\s*\d+[.)]\s+(.+)$/);
    if (orderedMatch) {
      const items: string[] = [];
      while (index < lines.length) {
        const match = lines[index].match(/^\s*\d+[.)]\s+(.+)$/);
        if (!match) break;
        items.push(`<li><p>${renderInlineMarkdown(match[1])}</p></li>`);
        index += 1;
      }
      blocks.push(`<ol>${items.join('')}</ol>`);
      continue;
    }

    const quoteMatch = line.match(/^\s*>\s?(.*)$/);
    if (quoteMatch) {
      const quoteLines: string[] = [];
      while (index < lines.length) {
        const match = lines[index].match(/^\s*>\s?(.*)$/);
        if (!match) break;
        quoteLines.push(match[1]);
        index += 1;
      }
      blocks.push(`<blockquote><p>${quoteLines.map(renderInlineMarkdown).join('<br />')}</p></blockquote>`);
      continue;
    }

    if (line.includes('|') && index + 1 < lines.length && isMarkdownTableSeparator(lines[index + 1])) {
      const headers = parseMarkdownTableRow(line);
      const rows: string[][] = [];
      index += 2;

      while (index < lines.length && lines[index].includes('|') && lines[index].trim()) {
        rows.push(parseMarkdownTableRow(lines[index]));
        index += 1;
      }

      const headerHtml = headers.map(header => `<th><p>${renderInlineMarkdown(header)}</p></th>`).join('');
      const bodyHtml = rows.map(row => {
        const cells = headers.map((_header, cellIndex) => `<td><p>${renderInlineMarkdown(row[cellIndex] || '')}</p></td>`).join('');
        return `<tr>${cells}</tr>`;
      }).join('');
      blocks.push(`<table><thead><tr>${headerHtml}</tr></thead><tbody>${bodyHtml}</tbody></table>`);
      continue;
    }

    const paragraphLines = [line.trim()];
    index += 1;
    while (
      index < lines.length &&
      lines[index].trim() &&
      !/^```/.test(lines[index]) &&
      !/^#{1,6}\s+/.test(lines[index]) &&
      !/^\s*[-*+]\s+/.test(lines[index]) &&
      !/^\s*\d+[.)]\s+/.test(lines[index]) &&
      !/^\s*>\s?/.test(lines[index]) &&
      !(lines[index].includes('|') && index + 1 < lines.length && isMarkdownTableSeparator(lines[index + 1]))
    ) {
      paragraphLines.push(lines[index].trim());
      index += 1;
    }
    blocks.push(`<p>${renderInlineMarkdown(paragraphLines.join(' '))}</p>`);
  }

  return blocks.join('');
};

type MermaidApi = typeof import('mermaid').default;

let mermaidPromise: Promise<MermaidApi> | null = null;

const getMermaid = async () => {
  mermaidPromise ??= import('mermaid').then(module => module.default);
  return mermaidPromise;
};

const MermaidDiagramView = ({ node, updateAttributes, selected }: any) => {
  const [code, setCode] = useState(node.attrs.code || DEFAULT_MERMAID_CODE);
  const [svg, setSvg] = useState('');
  const [error, setError] = useState('');
  const renderIdRef = useRef(`mermaid-${Math.random().toString(36).slice(2)}`);

  useEffect(() => {
    setCode(node.attrs.code || DEFAULT_MERMAID_CODE);
  }, [node.attrs.code]);

  useEffect(() => {
    let cancelled = false;

    const render = async () => {
      try {
        setError('');
        const mermaid = await getMermaid();
        mermaid.initialize({
          startOnLoad: false,
          securityLevel: 'strict',
          theme: 'dark'
        });
        const { svg: renderedSvg } = await mermaid.render(
          `${renderIdRef.current}-${Date.now()}`,
          code || DEFAULT_MERMAID_CODE
        );
        if (!cancelled) {
          setSvg(renderedSvg);
        }
      } catch (err: any) {
        if (!cancelled) {
          setSvg('');
          setError(err?.message || 'Şema render edilemedi.');
        }
      }
    };

    render();
    return () => {
      cancelled = true;
    };
  }, [code]);

  const handleCodeChange = (nextCode: string) => {
    setCode(nextCode);
    updateAttributes({ code: nextCode });
  };

  return (
    <NodeViewWrapper
      className={`notisight-mermaid-node rounded-2xl border bg-ns-bg-secondary/65 p-3 my-5 ${
        selected ? 'border-ns-primary/60 shadow-lg shadow-ns-primary/10' : 'border-ns-border/75'
      }`}
      data-code={code}
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-xs font-semibold text-ns-text-secondary">
          <Workflow className="h-3.5 w-3.5 text-ns-primary" />
          Mermaid Şema
        </div>
        <span className="rounded-full border border-ns-primary/20 bg-ns-primary/10 px-2 py-0.5 text-[10px] font-semibold text-ns-primary">
          canlı önizleme
        </span>
      </div>
      <textarea
        value={code}
        onChange={(event) => handleCodeChange(event.target.value)}
        spellCheck={false}
        className="mb-3 min-h-28 w-full resize-y rounded-xl border border-ns-border/75 bg-ns-bg-primary/75 p-3 font-mono text-xs leading-5 text-ns-text-primary outline-none transition-colors focus:border-ns-primary/50"
      />
      <div className="notisight-mermaid-preview rounded-xl border border-ns-border/60 bg-ns-bg-primary/55 p-4 overflow-x-auto">
        {error ? (
          <div className="text-xs leading-5 text-ns-error">{error}</div>
        ) : (
          <div dangerouslySetInnerHTML={{ __html: svg }} />
        )}
      </div>
    </NodeViewWrapper>
  );
};

const MermaidDiagram = TiptapNode.create({
  name: 'mermaidDiagram',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return {
      code: {
        default: DEFAULT_MERMAID_CODE,
        parseHTML: element => element.getAttribute('data-code') || DEFAULT_MERMAID_CODE,
        renderHTML: attributes => ({ 'data-code': attributes.code })
      }
    };
  },

  parseHTML() {
    return [{ tag: 'div[data-type="mermaid-diagram"]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      'div',
      mergeAttributes(HTMLAttributes, { 'data-type': 'mermaid-diagram', class: 'notisight-mermaid-export-source' }),
      ['pre', { class: 'mermaid-source' }, HTMLAttributes['data-code'] || HTMLAttributes.code || DEFAULT_MERMAID_CODE]
    ];
  },

  addNodeView() {
    return ReactNodeViewRenderer(MermaidDiagramView);
  }
});

const MenuBar = ({ editor }: { editor: any }) => {
  if (!editor) return null;

  const buttons = [
    {
      icon: Bold,
      onClick: () => editor.chain().focus().toggleBold().run(),
      isActive: editor.isActive('bold'),
      title: 'Kalın',
    },
    {
      icon: Italic,
      onClick: () => editor.chain().focus().toggleItalic().run(),
      isActive: editor.isActive('italic'),
      title: 'İtalik',
    },
    {
      icon: UnderlineIcon,
      onClick: () => editor.chain().focus().toggleUnderline().run(),
      isActive: editor.isActive('underline'),
      title: 'Altı Çizili',
    },
    {
      icon: Strikethrough,
      onClick: () => editor.chain().focus().toggleStrike().run(),
      isActive: editor.isActive('strike'),
      title: 'Üstü Çizili',
    },
    {
      icon: Highlighter,
      onClick: () => editor.chain().focus().toggleHighlight().run(),
      isActive: editor.isActive('highlight'),
      title: 'Vurgula',
    },
    {
      icon: Heading1,
      onClick: () => editor.chain().focus().toggleHeading({ level: 1 }).run(),
      isActive: editor.isActive('heading', { level: 1 }),
      title: 'Başlık 1',
    },
    {
      icon: Heading2,
      onClick: () => editor.chain().focus().toggleHeading({ level: 2 }).run(),
      isActive: editor.isActive('heading', { level: 2 }),
      title: 'Başlık 2',
    },
    {
      icon: List,
      onClick: () => editor.chain().focus().toggleBulletList().run(),
      isActive: editor.isActive('bulletList'),
      title: 'Madde İmli Liste',
    },
    {
      icon: ListOrdered,
      onClick: () => editor.chain().focus().toggleOrderedList().run(),
      isActive: editor.isActive('orderedList'),
      title: 'Numaralı Liste',
    },
    {
      icon: CheckSquare,
      onClick: () => editor.chain().focus().toggleTaskList().run(),
      isActive: editor.isActive('taskList'),
      title: 'Yapılacaklar Listesi',
    },
    {
      icon: Quote,
      onClick: () => editor.chain().focus().toggleBlockquote().run(),
      isActive: editor.isActive('blockquote'),
      title: 'Alıntı Bloğu',
    },
    {
      icon: Code,
      onClick: () => editor.chain().focus().toggleCodeBlock().run(),
      isActive: editor.isActive('codeBlock'),
      title: 'Kod Bloğu',
    },
  ];

  return (
    <div className="notisight-editor-toolbar no-scrollbar flex w-full max-w-full flex-nowrap items-center gap-1 overflow-x-auto p-1 bg-ns-bg-secondary border border-ns-border/80 rounded-xl mb-3 md:mb-6 shadow-xl shadow-black/30 sticky top-0 z-10 shrink-0 md:w-max md:flex-wrap md:overflow-visible">
      {buttons.map((btn, index) => (
        <React.Fragment key={index}>
          {index === 5 || index === 7 || index === 10 ? <div className="w-px h-5 bg-ns-border mx-1 shrink-0" /> : null}
          <button
            type="button"
            onMouseDown={(e) => e.preventDefault()}
            onClick={btn.onClick}
            title={btn.title}
            className={`shrink-0 p-1.5 rounded-lg transition-all ${
              btn.isActive 
                ? 'bg-ns-primary/10 text-ns-primary border border-ns-primary/20' 
                : 'text-ns-text-secondary hover:text-ns-text-primary hover:bg-ns-surface-hover/60 border border-transparent'
            }`}
          >
            <btn.icon className="w-4 h-4" />
          </button>
        </React.Fragment>
      ))}
      {editor.isActive('table') && (
        <>
          <div className="w-px h-5 bg-ns-border mx-1 shrink-0" />
          {[
            { icon: Columns3, title: 'Sütun ekle', onClick: () => editor.chain().focus().addColumnAfter().run() },
            { icon: Minus, title: 'Sütun sil', onClick: () => editor.chain().focus().deleteColumn().run() },
            { icon: Rows3, title: 'Satır ekle', onClick: () => editor.chain().focus().addRowAfter().run() },
            { icon: Minus, title: 'Satır sil', onClick: () => editor.chain().focus().deleteRow().run() },
            { icon: Table2, title: 'Başlık hücresi değiştir', onClick: () => editor.chain().focus().toggleHeaderCell().run() },
            { icon: Trash2, title: 'Tabloyu sil', onClick: () => editor.chain().focus().deleteTable().run() },
          ].map((btn, index) => (
            <button
              key={`table-${index}-${btn.title}`}
              type="button"
              onMouseDown={(e) => e.preventDefault()}
              onClick={btn.onClick}
              title={btn.title}
              className="shrink-0 p-1.5 rounded-lg transition-all text-ns-text-secondary hover:text-ns-text-primary hover:bg-ns-surface-hover/60 border border-transparent"
            >
              <btn.icon className="w-4 h-4" />
            </button>
          ))}
        </>
      )}
    </div>
  );
};

type SelectionMenuState = {
  x: number;
  y: number;
  text: string;
  source: 'title' | 'body';
  from: number;
  to: number;
};

type InlineAiAction = 'rewrite' | 'explain';

type InlineAiState = {
  status: 'loading' | 'preview' | 'error';
  action: InlineAiAction;
  source: 'title' | 'body';
  from: number;
  to: number;
  selectedText: string;
  result?: string;
  error?: string;
  requestId: number;
};

export const Editor: React.FC<EditorProps> = ({ note, onUpdate, folderPathStr }) => {
  const [slashMenu, setSlashMenu] = useState<{ x: number; y: number } | null>(null);
  const [slashSearch, setSlashSearch] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [selectionMenu, setSelectionMenu] = useState<SelectionMenuState | null>(null);
  const [localTitle, setLocalTitle] = useState(note.title);
  const [inlineAi, setInlineAi] = useState<InlineAiState | null>(null);
  
  const containerRef = useRef<HTMLDivElement>(null);
  const articleRef = useRef<HTMLElement>(null);
  const slashMenuRef = useRef<HTMLDivElement>(null);
  const selectionMenuRef = useRef<HTMLDivElement>(null);

  // Mutable refs to prevent TipTap closure capture stale-state bugs
  const slashMenuRefState = useRef<any>(null);
  const selectedIndexRef = useRef<number>(0);
  const filteredSlashItemsRef = useRef<any[]>([]);
  const inlineAiRequestRef = useRef(0);

  // Auto-save debouncing for content changes
  const saveTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    return () => {
      if (saveTimeoutRef.current) {
        clearTimeout(saveTimeoutRef.current);
      }
      inlineAiRequestRef.current += 1;
    };
  }, []);

  const scheduleTitleUpdate = (newTitle: string) => {
    setLocalTitle(newTitle);
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current);
    }
    saveTimeoutRef.current = setTimeout(() => {
      onUpdate(note.id, { title: newTitle });
    }, 600);
  };

  // Available Notion-style command options mapping
  const slashItems = [
    { id: 'h1', icon: Heading1, label: 'Başlık 1', desc: 'Büyük bölüm başlığı', keywords: 'heading1 h1 büyük başlık title' },
    { id: 'h2', icon: Heading2, label: 'Başlık 2', desc: 'Orta bölüm başlığı', keywords: 'heading2 h2 orta altbaşlık subtitle' },
    { id: 'bullet', icon: List, label: 'Madde imli liste', desc: 'Basit madde imli liste', keywords: 'bullet liste öğe yuvarlak madde unordered' },
    { id: 'ordered', icon: ListOrdered, label: 'Numaralı liste', desc: 'Numaralı liste', keywords: 'ordered sayı liste numaralı öğe sıra sequence' },
    { id: 'todo', icon: CheckSquare, label: 'Yapılacaklar Listesi', desc: 'Etkileşimli görev listesi', keywords: 'todo checklist görev onay kutusu check box square' },
    { id: 'blockquote', icon: Quote, label: 'Alıntı', desc: 'Renkli bir alıntı bloğu ekle', keywords: 'quote alıntı yorum blok blockquote' },
    { id: 'code', icon: Code, label: 'Kod Bloğu', desc: 'Eşaralıklı kod sözdizimi bloğu', keywords: 'code kod eşaralıklı terminal preformatted' },
    { id: 'table', icon: Table2, label: 'Tablo', desc: '3x3 düzenlenebilir tablo ekle', keywords: 'table tablo satır sütun hücre grid çizelge' },
    { id: 'mermaid', icon: Workflow, label: 'Şema', desc: 'Mermaid kodu ve canlı önizleme', keywords: 'mermaid şema diagram akış flowchart graph' },
    { id: 'underline', icon: UnderlineIcon, label: 'Altı Çizili', desc: 'Altı çizili tipografi çizgi kalınlığı', keywords: 'underline format metin alt çizgi u draw line' },
    { id: 'highlight', icon: Highlighter, label: 'Metni Vurgula', desc: 'Neon vurgulayıcı arka plan kalemi', keywords: 'highlight renk arka plan sarı vurgu tint' },
  ];

  // Filtering list items reactively based on user typing string following slash
  const filteredSlashItems = slashItems.filter(item => {
    if (!slashSearch) return true;
    const query = slashSearch.toLowerCase();
    return (
      item.label.toLowerCase().includes(query) ||
      item.desc.toLowerCase().includes(query) ||
      item.keywords.toLowerCase().includes(query)
    );
  });

  // Sync state variables to mutable refs to feed TipTap event capture reliably
  useEffect(() => {
    slashMenuRefState.current = slashMenu;
  }, [slashMenu]);

  useEffect(() => {
    selectedIndexRef.current = selectedIndex;
  }, [selectedIndex]);

  useEffect(() => {
    filteredSlashItemsRef.current = filteredSlashItems;
  }, [filteredSlashItems]);

  useEffect(() => {
    const selectedItem = slashMenuRef.current?.querySelector<HTMLElement>(`[data-slash-index="${selectedIndex}"]`);
    const menuEl = slashMenuRef.current;

    if (!selectedItem || !menuEl) return;

    const itemTop = selectedItem.offsetTop;
    const itemBottom = itemTop + selectedItem.offsetHeight;
    const visibleTop = menuEl.scrollTop;
    const visibleBottom = visibleTop + menuEl.clientHeight;

    if (itemTop < visibleTop) {
      menuEl.scrollTop = itemTop;
    } else if (itemBottom > visibleBottom) {
      menuEl.scrollTop = itemBottom - menuEl.clientHeight;
    }
  }, [selectedIndex, filteredSlashItems]);

  // Reset indices on search query shift to prevent out-of-bounds pointer errors
  useEffect(() => {
    setSelectedIndex(0);
  }, [slashSearch]);

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        bulletList: {
          keepMarks: true,
          keepAttributes: false,
        },
        orderedList: {
          keepMarks: true,
          keepAttributes: false,
        },
      }),
      Placeholder.configure({
        placeholder: "Bloklar için '/' yazın veya güzel düşünce yapıları oluşturmaya başlayın...",
        showOnlyWhenEditable: true,
      }),
      Link.configure({
        openOnClick: false,
        HTMLAttributes: {
          class: 'text-ns-primary underline cursor-pointer',
        },
      }),
      Underline,
      Highlight.configure({
        multicolor: true,
      }),
      TaskList,
      TaskItem.configure({
        nested: true,
      }),
      Table.configure({
        resizable: true,
        HTMLAttributes: {
          class: 'notisight-editor-table',
        },
      }),
      TableRow,
      TableHeader,
      TableCell,
      Image.configure({
        allowBase64: true,
        HTMLAttributes: {
          class: 'rounded-xl max-w-full my-4 shadow-lg border border-ns-border object-contain max-h-[600px]',
        },
      }),
      MermaidDiagram,
      InlineAiPreview,
    ],
    content: note.content,
    editorProps: {
      attributes: {
        class: 'notisight-editor-prose prose prose-invert prose-zinc max-w-none text-ns-text-primary leading-relaxed outline-none min-h-[calc(100dvh-18rem)] pb-28 focus:outline-none md:min-h-[500px] md:pb-32',
      },
      handleKeyDown: (view, event) => {
        // Handle keyboard navigation of active slash menu list
        if (slashMenuRefState.current) {
          if (event.key === 'ArrowDown') {
            const itemCount = filteredSlashItemsRef.current.length;
            if (itemCount > 0) {
              setSelectedIndex(prev => (prev + 1) % itemCount);
            }
            return true;
          }
          if (event.key === 'ArrowUp') {
            const itemCount = filteredSlashItemsRef.current.length;
            if (itemCount > 0) {
              setSelectedIndex(prev => (prev - 1 + itemCount) % itemCount);
            }
            return true;
          }
          if (event.key === 'Enter') {
            const selectedItem = filteredSlashItemsRef.current[selectedIndexRef.current];
            if (selectedItem) {
              executeSlashCommand(selectedItem.id);
              return true;
            }
          }
          if (event.key === 'Escape') {
            setSlashMenu(null);
            setSlashSearch('');
            return true;
          }
        }
        return false;
      },
      handlePaste: (view, event, slice) => {
        if (event.clipboardData && event.clipboardData.files && event.clipboardData.files.length > 0) {
          let hasImage = false;
          for (let i = 0; i < event.clipboardData.files.length; i++) {
            const file = event.clipboardData.files[i];
            if (file.type.startsWith('image/')) {
              hasImage = true;
              
              const formData = new FormData();
              formData.append('file', file);
              apiClient.fetchWithAuth(`/notes/${note.id}/attachments`, {
                method: 'POST',
                body: formData
              }).then(res => res.json()).then(data => {
                if (data && data.fileUrl) {
                  const imageUrl = buildApiUrl(data.fileUrl);
                  const node = view.state.schema.nodes.image.create({ src: imageUrl });
                  const tr = view.state.tr.replaceSelectionWith(node);
                  view.dispatch(tr);
                }
              }).catch(err => console.error('Image upload error:', err));
            }
          }
          if (hasImage) return true;
        }

        const plainText = event.clipboardData?.getData('text/plain') || '';
        const htmlText = event.clipboardData?.getData('text/html') || '';

        if (
          plainText &&
          looksLikeMarkdown(plainText) &&
          !htmlText &&
          view.state.selection.$from.parent.type.name !== 'codeBlock'
        ) {
          event.preventDefault();

          const container = document.createElement('div');
          container.innerHTML = markdownToEditorHtml(plainText);
          const parsedSlice = ProseMirrorDOMParser.fromSchema(view.state.schema).parseSlice(container);

          const { selection } = view.state;
          const isEmptyTextBlock =
            selection.empty &&
            selection.$from.depth > 0 &&
            selection.$from.parent.isTextblock &&
            selection.$from.parent.content.size === 0;

          const transaction = isEmptyTextBlock
            ? view.state.tr.replace(selection.$from.before(), selection.$from.after(), parsedSlice)
            : view.state.tr.replaceSelection(parsedSlice);

          view.dispatch(transaction);
          return true;
        }

        return false;
      },
      handleDrop: (view, event, slice, moved) => {
        if (!moved && event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files.length > 0) {
          let hasImage = false;
          for (let i = 0; i < event.dataTransfer.files.length; i++) {
            const file = event.dataTransfer.files[i];
            if (file.type.startsWith('image/')) {
              hasImage = true;
              const coordinates = view.posAtCoords({ left: event.clientX, top: event.clientY });
              
              const formData = new FormData();
              formData.append('file', file);
              apiClient.fetchWithAuth(`/notes/${note.id}/attachments`, {
                method: 'POST',
                body: formData
              }).then(res => res.json()).then(data => {
                if (data && data.fileUrl) {
                  const imageUrl = buildApiUrl(data.fileUrl);
                  const node = view.state.schema.nodes.image.create({ src: imageUrl });
                  if (coordinates) {
                    const tr = view.state.tr.insert(coordinates.pos, node);
                    view.dispatch(tr);
                  } else {
                    const tr = view.state.tr.replaceSelectionWith(node);
                    view.dispatch(tr);
                  }
                }
              }).catch(err => console.error('Image drop upload error:', err));
            }
          }
          if (hasImage) return true;
        }
        return false;
      }
    },
    onUpdate: ({ editor }) => {
      if (!isTiptapEditorReady(editor)) return;

      // Analyze current line sequence to detect "/" triggers or search parameters
      const { selection } = editor.state;
      const pos = selection.$from;
      const blockText = pos.parent.textBetween(0, pos.parentOffset, null, null);
      const lastSlashIndex = blockText.lastIndexOf('/');

      if (lastSlashIndex !== -1) {
        const query = blockText.slice(lastSlashIndex + 1);
        if (!query.includes(' ')) {
          setSlashSearch(query);
          try {
            const coords = editor.view.coordsAtPos(selection.from);
            const articleEl = articleRef.current;
            if (articleEl) {
              const rect = articleEl.getBoundingClientRect();
              setSlashMenu({
                x: Math.max(16, coords.left - rect.left),
                y: (() => {
                  const preferredY = coords.top - rect.top + articleEl.scrollTop + 22;
                  const menuBottom = preferredY + SLASH_MENU_MAX_HEIGHT;
                  const visibleBottom = articleEl.scrollTop + articleEl.clientHeight - 16;
                  const minY = articleEl.scrollTop + 12;

                  if (menuBottom > visibleBottom) {
                    return Math.max(minY, preferredY - SLASH_MENU_MAX_HEIGHT - 44);
                  }

                  return preferredY;
                })(),
              });
            }
          } catch (e) {
            // fallback
            if (!slashMenu) {
              setSlashMenu({ x: 200, y: 300 });
            }
          }
        } else {
          setSlashMenu(null);
          setSlashSearch('');
        }
      } else {
        setSlashMenu(null);
        setSlashSearch('');
      }

      // Debounced auto-save (600ms)
      if (saveTimeoutRef.current) {
        clearTimeout(saveTimeoutRef.current);
      }
      saveTimeoutRef.current = setTimeout(() => {
        if (!isTiptapEditorReady(editor)) return;
        onUpdate(note.id, { content: editor.getHTML() });
      }, 600);
    },
  });

  const titleEditor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: {
          levels: [1, 2],
        },
        bulletList: false,
        orderedList: false,
        listItem: false,
        blockquote: false,
        codeBlock: false,
        horizontalRule: false,
      }),
      Placeholder.configure({
        placeholder: 'İsimsiz belge',
        showOnlyWhenEditable: true,
      }),
      Underline,
      Highlight.configure({
        multicolor: true,
      }),
      InlineAiPreview,
    ],
    content: {
      type: 'doc',
      content: [
        {
          type: 'heading',
          attrs: { level: 1 },
          content: note.title ? [{ type: 'text', text: note.title }] : [],
        },
      ],
    },
    editorProps: {
      attributes: {
        class: 'notisight-title-editor prose prose-invert prose-zinc max-w-none text-ns-text-primary outline-none focus:outline-none',
      },
      handleKeyDown: (_view, event) => {
        if (event.key === 'Enter') {
          event.preventDefault();
          editor?.commands.focus('start');
          return true;
        }
        return false;
      },
    },
    onUpdate: ({ editor }) => {
      if (!isTiptapEditorReady(editor)) return;
      scheduleTitleUpdate(editor.getText());
    },
  });

  const getEditorBySource = (source: 'title' | 'body') => {
    const selectedEditor = source === 'title' ? titleEditor : editor;
    return isTiptapEditorReady(selectedEditor) ? selectedEditor : null;
  };

  const clearInlineAi = (cancelRequest = true) => {
    if (cancelRequest) {
      inlineAiRequestRef.current += 1;
    }
    setInlineAi(null);
    setInlineAiPreviewDecoration(editor, null);
    setInlineAiPreviewDecoration(titleEditor, null);
  };

  const getSurroundingText = (activeEditor: any, from: number, to: number) => {
    if (!isTiptapEditorReady(activeEditor)) return '';
    const start = Math.max(0, from - 500);
    const end = Math.min(activeEditor.state.doc.content.size, to + 500);
    return activeEditor.state.doc.textBetween(start, end, ' ');
  };

  const updateSelectionMenuFromEditor = (
    source: 'title' | 'body',
    activeEditor: any,
    cancelInlineAi = true
  ) => {
    if (!isTiptapEditorReady(activeEditor)) return false;

    const { selection } = activeEditor.state;
    if (selection.empty) return false;

    const selectedText = activeEditor.state.doc.textBetween(selection.from, selection.to, ' ').trim();
    if (!selectedText) return false;

    try {
      if (cancelInlineAi) {
        clearInlineAi();
      }

      const coords = activeEditor.view.coordsAtPos(selection.from);
      const articleEl = articleRef.current;
      if (!articleEl) return false;

      const rect = articleEl.getBoundingClientRect();
      setSelectionMenu({
        x: Math.max(16, coords.left - rect.left),
        y: coords.top - rect.top + articleEl.scrollTop - 48,
        text: selectedText,
        source,
        from: selection.from,
        to: selection.to,
      });
      return true;
    } catch {
      return false;
    }
  };

  // Track text selection changes dynamically to align the AI/format command popover
  useEffect(() => {
    if (!isTiptapEditorReady(editor)) return;

    const handleSelection = () => {
      const { selection } = editor.state;
      if (selection.empty) {
        setSelectionMenu(null);
        clearInlineAi();
        return;
      }

      if (!updateSelectionMenuFromEditor('body', editor)) {
        setSelectionMenu(null);
      }
    };

    editor.on('selectionUpdate', handleSelection);
    return () => {
      editor.off('selectionUpdate', handleSelection);
    };
  }, [editor, titleEditor]);

  useEffect(() => {
    if (!isTiptapEditorReady(titleEditor)) return;

    const handleSelection = () => {
      const { selection } = titleEditor.state;
      if (selection.empty) {
        setSelectionMenu(current => {
          if (current?.source !== 'title') return current;
          clearInlineAi();
          return null;
        });
        return;
      }

      if (!updateSelectionMenuFromEditor('title', titleEditor)) {
        setSelectionMenu(current => current?.source === 'title' ? null : current);
      }
    };

    titleEditor.on('selectionUpdate', handleSelection);
    return () => {
      titleEditor.off('selectionUpdate', handleSelection);
    };
  }, [editor, titleEditor]);

  // Sync internal title inputs
  useEffect(() => {
    setLocalTitle(note.title);
  }, [note.title]);

  useEffect(() => {
    if (!isTiptapEditorReady(titleEditor)) return;
    const currentTitle = titleEditor.getText();
    if (currentTitle === note.title) return;

    titleEditor.commands.setContent({
      type: 'doc',
      content: [
        {
          type: 'heading',
          attrs: { level: 1 },
          content: note.title ? [{ type: 'text', text: note.title }] : [],
        },
      ],
    }, { emitUpdate: false });
  }, [note.title, titleEditor]);

  // Close menus on mouse clicks outside of our bubble panels
  useEffect(() => {
    const clickAway = (e: MouseEvent) => {
      const target = e.target as Node;
      if (slashMenuRef.current && !slashMenuRef.current.contains(target)) {
        setSlashMenu(null);
        setSlashSearch('');
      }

      const clickedInEditor =
        (isTiptapEditorReady(editor) && editor.view.dom.contains(target)) ||
        (isTiptapEditorReady(titleEditor) && titleEditor.view.dom.contains(target));

      if (selectionMenuRef.current && !selectionMenuRef.current.contains(target) && !clickedInEditor) {
        setSelectionMenu(null);
        clearInlineAi();
      }
    };

    const syncSelectionAfterMouseUp = (e: MouseEvent) => {
      const target = e.target as Node;
      const clickedInTitle = isTiptapEditorReady(titleEditor) && titleEditor.view.dom.contains(target);
      const clickedInBody = isTiptapEditorReady(editor) && editor.view.dom.contains(target);

      if (!clickedInTitle && !clickedInBody) return;

      window.requestAnimationFrame(() => {
        const didUpdate = clickedInTitle
          ? updateSelectionMenuFromEditor('title', titleEditor)
          : updateSelectionMenuFromEditor('body', editor);

        if (!didUpdate) {
          setSelectionMenu(current => {
            if (!current) return current;
            if ((clickedInTitle && current.source === 'title') || (clickedInBody && current.source === 'body')) {
              clearInlineAi();
              return null;
            }
            return current;
          });
        }
      });
    };

    document.addEventListener('mousedown', clickAway);
    document.addEventListener('mouseup', syncSelectionAfterMouseUp);
    return () => {
      document.removeEventListener('mousedown', clickAway);
      document.removeEventListener('mouseup', syncSelectionAfterMouseUp);
    };
  }, [editor, titleEditor]);

  const acceptInlineAiPreview = () => {
    if (!inlineAi || inlineAi.status !== 'preview' || inlineAi.action !== 'rewrite' || !inlineAi.result) {
      return;
    }

    const activeEditor = getEditorBySource(inlineAi.source);
    if (!isTiptapEditorReady(activeEditor)) return;

    setInlineAiPreviewDecoration(activeEditor, null);
    activeEditor
      .chain()
      .focus()
      .insertContentAt({ from: inlineAi.from, to: inlineAi.to }, inlineAi.result)
      .run();
    setSelectionMenu(null);
    clearInlineAi(false);
  };

  useEffect(() => {
    const handleInlineAiKeyDown = (event: KeyboardEvent) => {
      if (!inlineAi) return;

      if (event.key === 'Escape') {
        event.preventDefault();
        clearInlineAi();
        setSelectionMenu(null);
        return;
      }

      if (
        inlineAi.status === 'preview' &&
        inlineAi.action === 'rewrite' &&
        (event.key === 'Tab' || event.key === 'Enter')
      ) {
        event.preventDefault();
        acceptInlineAiPreview();
      }
    };

    document.addEventListener('keydown', handleInlineAiKeyDown, true);
    return () => document.removeEventListener('keydown', handleInlineAiKeyDown, true);
  }, [inlineAi, editor, titleEditor]);

  if (!isTiptapEditorReady(editor) || !isTiptapEditorReady(titleEditor)) return null;

  // Slash commands execution with content cleanup
  const executeSlashCommand = (cmd: string) => {
    if (!isTiptapEditorReady(editor)) return;

    const { selection } = editor.state;
    const pos = selection.$from;
    const blockText = pos.parent.textBetween(0, pos.parentOffset, null, null);
    const lastSlashIndex = blockText.lastIndexOf('/');

    let chain = editor.chain().focus();

    if (lastSlashIndex !== -1) {
      // Calculate absolute start position to strip "/" and any temporary search filter typed
      const startPos = selection.from - (pos.parentOffset - lastSlashIndex);
      chain = chain.deleteRange({ from: startPos, to: selection.from });
    }

    // Toggle formatting or element types
    if (cmd === 'h1') {
      chain = chain.toggleHeading({ level: 1 });
    } else if (cmd === 'h2') {
      chain = chain.toggleHeading({ level: 2 });
    } else if (cmd === 'bullet') {
      chain = chain.toggleBulletList();
    } else if (cmd === 'ordered') {
      chain = chain.toggleOrderedList();
    } else if (cmd === 'todo') {
      chain = chain.toggleTaskList();
    } else if (cmd === 'blockquote') {
      chain = chain.toggleBlockquote();
    } else if (cmd === 'code') {
      chain = chain.toggleCodeBlock();
    } else if (cmd === 'table') {
      chain = chain.insertTable({ rows: 3, cols: 3, withHeaderRow: true });
    } else if (cmd === 'mermaid') {
      chain = chain.insertContent({
        type: 'mermaidDiagram',
        attrs: { code: DEFAULT_MERMAID_CODE }
      });
    } else if (cmd === 'underline') {
      chain = chain.toggleUnderline();
    } else if (cmd === 'highlight') {
      chain = chain.toggleHighlight();
    }

    chain.run();
    
    setSlashMenu(null);
    setSlashSearch('');
  };

  // Contextual AI Actions handler
  const runAiCommand = async (action: InlineAiAction) => {
    if (!selectionMenu) return;

    const activeEditor = getEditorBySource(selectionMenu.source);
    if (!activeEditor) return;

    const selectedText = selectionMenu.text.trim();
    if (!selectedText) return;

    const requestId = inlineAiRequestRef.current + 1;
    inlineAiRequestRef.current = requestId;
    setInlineAiPreviewDecoration(activeEditor, null);
    setInlineAi({
      status: 'loading',
      action,
      source: selectionMenu.source,
      from: selectionMenu.from,
      to: selectionMenu.to,
      selectedText,
      requestId,
    });

    try {
      const isCustomModel = localStorage.getItem('notisight_ai_is_custom_model') === 'true';
      const selectedModel = isCustomModel
        ? localStorage.getItem('notisight_ai_custom_model')
        : localStorage.getItem('notisight_ai_model');
      const normalizedModel = selectedModel?.trim();
      const modelId = normalizedModel && !['undefined', 'null'].includes(normalizedModel.toLowerCase())
        ? normalizedModel
        : undefined;

      const response = await apiClient.post('/ai/inline-edit', {
        action,
        selectedText,
        surroundingText: getSurroundingText(activeEditor, selectionMenu.from, selectionMenu.to),
        target: selectionMenu.source,
        provider: Number(localStorage.getItem('notisight_ai_provider') ?? 0),
        modelId,
        tone: Number(localStorage.getItem('notisight_ai_tone') ?? 0),
      });

      if (inlineAiRequestRef.current !== requestId) return;

      const result = String(response.result || '').trim();
      if (!result) {
        throw new Error('AI boş yanıt döndürdü.');
      }

      if (action === 'rewrite') {
        setInlineAiPreviewDecoration(activeEditor, {
          from: selectionMenu.from,
          to: selectionMenu.to,
          text: result,
        });
      }

      setInlineAi({
        status: 'preview',
        action,
        source: selectionMenu.source,
        from: selectionMenu.from,
        to: selectionMenu.to,
        selectedText,
        result,
        requestId,
      });
    } catch (err: any) {
      if (inlineAiRequestRef.current !== requestId) return;
      setInlineAiPreviewDecoration(activeEditor, null);
      setInlineAi({
        status: 'error',
        action,
        source: selectionMenu.source,
        from: selectionMenu.from,
        to: selectionMenu.to,
        selectedText,
        error: err?.message || 'AI işlemi tamamlanamadı.',
        requestId,
      });
    }
  };

  const activeSelectionEditor = selectionMenu?.source === 'title' ? titleEditor : editor;
  const selectionFormatButtons = [
    { icon: Bold, title: 'Kalın', isActive: activeSelectionEditor.isActive('bold'), onClick: () => activeSelectionEditor.chain().focus().toggleBold().run() },
    { icon: Italic, title: 'İtalik', isActive: activeSelectionEditor.isActive('italic'), onClick: () => activeSelectionEditor.chain().focus().toggleItalic().run() },
    { icon: UnderlineIcon, title: 'Altı Çizili', isActive: activeSelectionEditor.isActive('underline'), onClick: () => activeSelectionEditor.chain().focus().toggleUnderline().run() },
    { icon: Strikethrough, title: 'Üstü Çizili', isActive: activeSelectionEditor.isActive('strike'), onClick: () => activeSelectionEditor.chain().focus().toggleStrike().run() },
    { icon: Highlighter, title: 'Vurgula', isActive: activeSelectionEditor.isActive('highlight'), onClick: () => activeSelectionEditor.chain().focus().toggleHighlight().run() },
    { icon: Code, title: 'Satır İçi Kod', isActive: activeSelectionEditor.isActive('code'), onClick: () => activeSelectionEditor.chain().focus().toggleCode().run() },
    { icon: Heading1, title: 'Başlık 1', isActive: activeSelectionEditor.isActive('heading', { level: 1 }), onClick: () => activeSelectionEditor.chain().focus().toggleHeading({ level: 1 }).run() },
    { icon: Heading2, title: 'Başlık 2', isActive: activeSelectionEditor.isActive('heading', { level: 2 }), onClick: () => activeSelectionEditor.chain().focus().toggleHeading({ level: 2 }).run() },
    ...(selectionMenu?.source === 'body'
      ? [{ icon: Quote, title: 'Alıntı', isActive: activeSelectionEditor.isActive('blockquote'), onClick: () => activeSelectionEditor.chain().focus().toggleBlockquote().run() }]
      : []),
  ];
  const isRewritePreview = inlineAi?.status === 'preview' && inlineAi.action === 'rewrite';
  const isCompactEditor = typeof window !== 'undefined' && window.matchMedia('(max-width: 767px)').matches;
  const selectionMenuStyle = selectionMenu
    ? {
        top: isRewritePreview
          ? Math.max((articleRef.current?.scrollTop ?? 0) + 8, selectionMenu.y - 86)
          : selectionMenu.y,
        left: isCompactEditor ? 16 : Math.max(16, Math.min(selectionMenu.x, 500)),
      }
    : undefined;
  const slashMenuStyle = slashMenu
    ? {
        top: slashMenu.y,
        left: isCompactEditor ? 16 : Math.min(slashMenu.x, 400),
        maxHeight: SLASH_MENU_MAX_HEIGHT,
      }
    : undefined;

  return (
    <main className="flex-1 flex flex-col bg-ns-bg-primary h-full relative" ref={containerRef}>
      <header className="h-10 md:h-12 border-b border-ns-border flex items-center px-4 md:px-8 gap-4 justify-between shrink-0 bg-ns-bg-primary/95">
        <div className="flex min-w-0 flex-1 items-center gap-2 overflow-hidden text-ns-text-muted text-[11px] md:text-xs">
          <span className="truncate">{folderPathStr || 'çalışma alanı'}</span>
          <span className="shrink-0">/</span>
          <span className="truncate text-ns-text-primary">{localTitle || 'İsimsiz'}</span>
        </div>
        <div className="flex items-center gap-2">
          {inlineAi?.status === 'loading' && (
            <div className="hidden items-center gap-2 text-ns-primary text-xs font-semibold animate-pulse sm:flex">
              <Sparkles className="w-3.5 h-3.5 animate-spin" />
              <span>Yapay Zeka satıriçi bloklar oluşturuyor...</span>
            </div>
          )}
        </div>
      </header>

      <article className="notisight-editor-scroll flex-1 px-4 pt-4 pb-6 md:px-16 md:py-12 overflow-y-auto relative" ref={articleRef}>
        <motion.div className="mx-auto w-full max-w-3xl md:max-w-none" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          
          <EditorContent editor={titleEditor} />

          {note.tags.length > 0 && (
            <div className="flex gap-2 mb-8 flex-wrap">
              {note.tags.map(tag => (
                <span key={tag} className="px-2 py-0.5 rounded bg-ns-primary/10 border border-ns-primary/20 text-ns-primary text-xs font-medium">
                  {tag}
                </span>
              ))}
            </div>
          )}

          {note.audioUrl && (
            <div className="mb-8 p-6 bg-ns-bg-secondary/50 border border-ns-border rounded-xl flex items-center justify-center backdrop-blur-sm relative">
              <div className="absolute inset-x-0 top-0 h-[2px] bg-gradient-to-r from-transparent via-ns-primary/50 to-transparent"></div>
              <audio controls src={note.audioUrl} className="w-full max-w-md rounded-full" />
            </div>
          )}

          <MenuBar editor={editor} />
          
          {/* Contextual AI Highlight menu */}
          <AnimatePresence>
            {selectionMenu && (
              <motion.div 
                ref={selectionMenuRef}
                onMouseDown={(e) => e.stopPropagation()}
                initial={{ opacity: 0, scale: 0.95 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.95 }}
                className="absolute flex max-w-[min(720px,calc(100vw-2rem))] flex-wrap items-center gap-1 rounded-2xl border border-ns-border bg-ns-bg-secondary/95 p-1.5 shadow-2xl shadow-black/30 backdrop-blur-md z-40"
                style={selectionMenuStyle}
              >
                {selectionFormatButtons.map((button) => (
                  <button
                    key={button.title}
                    type="button"
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={button.onClick}
                    title={button.title}
                    className={`flex h-8 w-8 items-center justify-center rounded-xl border text-xs transition-all ${
                      button.isActive
                        ? 'border-ns-primary/30 bg-ns-primary/12 text-ns-primary shadow-sm shadow-ns-primary/10'
                        : 'border-transparent text-ns-text-secondary hover:bg-ns-surface-hover/70 hover:text-ns-text-primary'
                    }`}
                  >
                    <button.icon className="h-4 w-4" />
                  </button>
                ))}
                <div className="mx-1 h-5 w-px bg-ns-border/80" />
                <button
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => runAiCommand('rewrite')}
                  disabled={inlineAi?.status === 'loading'}
                  className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs text-ns-primary hover:text-ns-primary-hover hover:bg-ns-surface-hover rounded-lg transition-all font-medium disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Wand2 className="w-3.5 h-3.5" />
                  Yeniden Yaz
                </button>
                <button
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => runAiCommand('explain')}
                  disabled={inlineAi?.status === 'loading'}
                  className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs text-ns-text-primary hover:text-ns-text-primary hover:bg-ns-surface-hover rounded-lg transition-all font-medium disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <HelpCircle className="w-3.5 h-3.5" />
                  Açıkla
                </button>
                {inlineAi && (
                  <div className="mt-1 w-full min-w-0 rounded-xl border border-ns-border/70 bg-ns-bg-primary/72 p-2.5 text-xs leading-5 text-ns-text-secondary sm:min-w-64">
                    {inlineAi.status === 'loading' && (
                      <div className="flex items-center gap-2 text-ns-primary">
                        <Sparkles className="h-3.5 w-3.5 animate-spin" />
                        AI sonucu hazırlanıyor...
                      </div>
                    )}
                    {inlineAi.status === 'error' && (
                      <div className="text-ns-error">{inlineAi.error}</div>
                    )}
                    {inlineAi.status === 'preview' && inlineAi.action === 'explain' && (
                      <div className="text-ns-text-primary">{inlineAi.result}</div>
                    )}
                    {inlineAi.status === 'preview' && inlineAi.action === 'rewrite' && (
                      <div className="space-y-1.5">
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-ns-text-muted">
                          <span className="text-ns-primary">Öneri hazır.</span>
                          <span>Tab / Enter kabul</span>
                          <span>Esc veya boş alan iptal</span>
                        </div>
                        <div className="max-h-24 overflow-y-auto rounded-lg border border-ns-primary/20 bg-ns-primary/8 px-2 py-1.5 text-ns-text-primary">
                          {inlineAi.result}
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </motion.div>
            )}
          </AnimatePresence>

          {/* Dynamic Slash Commands popup with key navigations */}
          <AnimatePresence>
            {slashMenu && (
              <motion.div 
                ref={slashMenuRef}
                onMouseDown={(e) => e.stopPropagation()}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0 }}
                className="absolute bg-ns-bg-secondary/95 border border-ns-border rounded-xl p-2 w-[min(16rem,calc(100vw-2rem))] shadow-2xl z-50 flex flex-col gap-1 backdrop-blur-md overflow-y-auto"
                style={slashMenuStyle}
              >
                <div className="px-2 py-1 flex items-center justify-between text-[10px] font-bold text-ns-text-muted uppercase tracking-wider mb-1">
                  <span>Temel bloklar</span>
                  {slashSearch && (
                    <span className="text-ns-primary normal-case bg-ns-primary/10 px-1.5 py-0.5 rounded">
                      filtre: {slashSearch}
                    </span>
                  )}
                </div>
                
                {filteredSlashItems.length === 0 ? (
                  <div className="px-3 py-4 text-xs text-ns-text-muted text-center">
                    Eşleşen blok türü bulunamadı
                  </div>
                ) : (
                  filteredSlashItems.map((item, index) => (
                    <button
                      key={item.id}
                      data-slash-index={index}
                      type="button"
                      onMouseDown={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        executeSlashCommand(item.id);
                      }}
                      onMouseEnter={() => setSelectedIndex(index)}
                      className={`flex items-center gap-3 p-1.5 rounded-lg text-left transition-all ${
                        index === selectedIndex 
                          ? 'bg-ns-bg-tertiary text-ns-text-primary' 
                          : 'text-ns-text-primary hover:text-ns-text-primary hover:bg-ns-surface-hover/40'
                      }`}
                    >
                      <div className={`w-8 h-8 rounded-md border flex items-center justify-center transition-colors ${
                        index === selectedIndex
                          ? 'bg-ns-primary border-ns-primary text-ns-bg-primary'
                          : 'bg-ns-bg-primary border-ns-border text-ns-primary'
                      }`}>
                        <item.icon className="w-4 h-4" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="text-xs font-semibold">{item.label}</div>
                        <div className="text-[10px] text-ns-text-muted truncate">{item.desc}</div>
                      </div>
                    </button>
                  ))
                )}
              </motion.div>
            )}
          </AnimatePresence>

          {/* TipTap Core Render Canvas */}
          <EditorContent editor={editor} />
          
        </motion.div>
      </article>
    </main>
  );
};
