import React, { useState, useRef, useEffect } from 'react';
import { Send, FileText, Bot, PanelRightClose, X, Plus, Eye, EyeOff, ChevronUp, Clock, History, Trash2, Settings } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import ReactMarkdown from 'react-markdown';
import { apiClient } from '../utils/apiClient';

interface AIAssistantProps {
  onCollapse: () => void;
  onSelectNote?: (noteId: string) => void;
  width?: number;
}

interface Citation {
  refId: string;
  noteId: string;
  title: string;
  sourceType: string;
  sourceLabel: string;
  snippet: string;
}

interface ToneProfile {
  value: number;
  key: string;
  displayName: string;
  description: string;
  icon: string;
}

interface Message {
  id: number;
  role: string;
  text: string;
  sources?: Array<{ id: string; title: string }>;
  citations?: Citation[];
  producedByMode?: string;
  suggestModeSwitch?: boolean;
}

interface ChatSession {
  id: string;
  title: string;
  messages: Message[];
}

const providerDefaultModels: Record<number, string> = {
  0: 'gpt-4o-mini',
  1: 'qwen-plus',
  2: 'claude-3-haiku-20240307',
  3: 'gemini-1.5-flash',
  4: 'deepseek-chat',
  5: 'openai/gpt-4o-mini',
  6: 'grok-beta'
};

const modelsByProvider: Record<number, { id: string; name: string }[]> = {
  0: [{ id: 'gpt-4o', name: 'GPT-4o' }, { id: 'gpt-4o-mini', name: 'GPT-4o Mini' }],
  1: [{ id: 'qwen-max', name: 'Qwen Max' }, { id: 'qwen-plus', name: 'Qwen Plus' }],
  2: [{ id: 'claude-3-5-sonnet-20240620', name: 'Claude 3.5 Sonnet' }, { id: 'claude-3-haiku-20240307', name: 'Claude 3 Haiku' }],
  3: [{ id: 'gemini-1.5-pro', name: 'Gemini 1.5 Pro' }, { id: 'gemini-1.5-flash', name: 'Gemini 1.5 Flash' }, { id: 'gemini-1.5-flash-8b', name: 'Gemini 1.5 Flash-8B' }],
  4: [{ id: 'deepseek-chat', name: 'DeepSeek Chat' }, { id: 'deepseek-coder', name: 'DeepSeek Coder' }],
  5: [{ id: 'openai/gpt-4o-mini', name: 'OpenRouter: GPT-4o Mini' }, { id: 'anthropic/claude-3.5-sonnet', name: 'OpenRouter: Claude 3.5 Sonnet' }, { id: 'google/gemini-flash-1.5', name: 'OpenRouter: Gemini Flash 1.5' }],
  6: [{ id: 'grok-beta', name: 'Grok Beta' }, { id: 'grok-vision-beta', name: 'Grok Vision Beta' }, { id: 'grok-2', name: 'Grok 2' }]
};

type MarkdownSegment =
  | { type: 'text'; content: string }
  | { type: 'table'; headers: string[]; rows: string[][] };

const citationMarkdownComponents = (msg: Message) => ({
  a: ({ href, children }: { href?: string; children?: React.ReactNode }) => {
    if (href?.startsWith('#cite-')) {
      const refId = href.replace('#cite-', '');
      const citationIdx = msg.citations?.findIndex(c => c.refId === refId);
      const num = citationIdx !== undefined && citationIdx >= 0 ? citationIdx + 1 : refId.replace(/^c/i, '');
      const citation = msg.citations?.find(c => c.refId === refId);
      const tooltipText = citation
        ? `${citation.title || 'İsimsiz Dosya'}${citation.sourceLabel ? ` (${citation.sourceLabel})` : ''}\n\n"${citation.snippet}"`
        : '';

      return (
        <sup
          key={refId}
          className="inline-flex items-center justify-center min-w-[16px] h-4 px-1 text-[9px] font-bold bg-ns-primary/10 text-ns-primary rounded-full cursor-help hover:bg-ns-primary hover:text-white transition-colors ml-0.5 align-super border border-ns-primary/20"
          title={tooltipText}
        >
          {num}
        </sup>
      );
    }

    return <a href={href} target="_blank" rel="noreferrer" className="text-ns-primary hover:underline">{children}</a>;
  },
  p: ({ children }: { children?: React.ReactNode }) => <p className="min-w-0 break-words">{children}</p>,
  pre: ({ children }: { children?: React.ReactNode }) => (
    <pre className="max-w-full overflow-x-auto whitespace-pre">{children}</pre>
  ),
  code: ({ children, className }: { children?: React.ReactNode; className?: string }) => (
    <code className={`${className ?? ''} break-words whitespace-pre-wrap`}>{children}</code>
  )
});

const parseMarkdownTableRow = (line: string) =>
  line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map(cell => cell.trim());

const isMarkdownTableSeparator = (line: string) => {
  const cells = parseMarkdownTableRow(line);
  return cells.length > 1 && cells.every(cell => /^:?-{3,}:?$/.test(cell));
};

const splitMarkdownTables = (text: string): MarkdownSegment[] => {
  const lines = text.split('\n');
  const segments: MarkdownSegment[] = [];
  let textBuffer: string[] = [];
  let index = 0;

  const flushText = () => {
    if (textBuffer.length > 0) {
      segments.push({ type: 'text', content: textBuffer.join('\n') });
      textBuffer = [];
    }
  };

  while (index < lines.length) {
    const current = lines[index];
    const next = lines[index + 1];

    if (current?.includes('|') && next && isMarkdownTableSeparator(next)) {
      flushText();
      const headers = parseMarkdownTableRow(current);
      const rows: string[][] = [];
      index += 2;

      while (index < lines.length && lines[index].includes('|') && lines[index].trim() !== '') {
        const row = parseMarkdownTableRow(lines[index]);
        rows.push(row);
        index += 1;
      }

      segments.push({ type: 'table', headers, rows });
      continue;
    }

    textBuffer.push(current);
    index += 1;
  }

  flushText();
  return segments;
};

const MarkdownContent = ({ msg }: { msg: Message }) => {
  const normalizedText = msg.text.replace(/\[ID:\s*(\w+)\]/gi, '[$1](#cite-$1)');
  const segments = splitMarkdownTables(normalizedText);
  const components = citationMarkdownComponents(msg);

  return (
    <div className="ai-markdown w-full min-w-0 max-w-full">
      {segments.map((segment, index) => {
        if (segment.type === 'text') {
          return (
            <ReactMarkdown key={`text-${index}`} components={components}>
              {segment.content}
            </ReactMarkdown>
          );
        }

        return (
          <div key={`table-${index}`} className="ai-markdown-table-wrap">
            <table>
              <thead>
                <tr>
                  {segment.headers.map((header, headerIndex) => (
                    <th key={headerIndex}>
                      <ReactMarkdown components={components}>{header}</ReactMarkdown>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {segment.rows.map((row, rowIndex) => (
                  <tr key={rowIndex}>
                    {segment.headers.map((_, cellIndex) => (
                      <td key={cellIndex}>
                        <ReactMarkdown components={components}>{row[cellIndex] ?? ''}</ReactMarkdown>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        );
      })}
    </div>
  );
};

export const AIAssistant: React.FC<AIAssistantProps> = ({ onCollapse, onSelectNote, width }) => {
  const [sessions, setSessions] = useState<ChatSession[]>([{ id: 'default', title: 'Sohbet 1', messages: [] }]);
  const [openSessionIds, setOpenSessionIds] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem('notisight_open_sessions');
      return saved ? JSON.parse(saved) : ['default'];
    } catch {
      return ['default'];
    }
  });
  const [activeSessionId, setActiveSessionId] = useState<string>('default');
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [sightMode, setSightMode] = useState(true);
  const [tones, setTones] = useState<ToneProfile[]>([]);
  const [activeTone, setActiveTone] = useState<number>(0);
  const [isToneDropdownOpen, setIsToneDropdownOpen] = useState(false);
  const [progressText, setProgressText] = useState<string>('Düşünüyor...');
  const [apiProviders, setApiProviders] = useState<any[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<number>(() => Number(localStorage.getItem('notisight_ai_provider') ?? 0));
  const [selectedModel, setSelectedModel] = useState<string>(() => localStorage.getItem('notisight_ai_model') || 'gpt-4o-mini');
  const [isAISettingsOpen, setIsAISettingsOpen] = useState(false);
  const [customModelId, setCustomModelId] = useState(() => localStorage.getItem('notisight_ai_custom_model') || "");
  const [isCustomModel, setIsCustomModel] = useState(() => localStorage.getItem('notisight_ai_is_custom_model') === 'true');
  const chatContainerRef = useRef<HTMLDivElement>(null);
  const toneDropdownRef = useRef<HTMLDivElement>(null);

  const activeSession = sessions.find(s => s.id === activeSessionId) || sessions[0];
  const messages = activeSession.messages;

  const scrollToBottom = () => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTo({
        top: chatContainerRef.current.scrollHeight,
        behavior: 'smooth'
      });
    }
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  useEffect(() => {
    const fetchSessions = async () => {
      try {
        const res = await apiClient.fetchWithAuth('/ai/sessions');
        if (res.ok) {
          const data = await res.json();
          if (data && data.length > 0) {
            setSessions(data.map((s: any) => ({
              id: s.id,
              title: s.title,
              messages: []
            })));
            
            let initialOpenIds = [...openSessionIds];
            initialOpenIds = initialOpenIds.filter(id => data.some((s: any) => s.id === id));
            
            if (initialOpenIds.length === 0) {
              initialOpenIds = [data[0].id];
            }
            setOpenSessionIds(initialOpenIds);
            
            if (!initialOpenIds.includes(activeSessionId) || activeSessionId === 'default') {
              setActiveSessionId(initialOpenIds[0]);
            }
          } else {
            const newId = 'new_' + Date.now().toString();
            setSessions([{ id: newId, title: 'Yeni Sohbet', messages: [] }]);
            setOpenSessionIds([newId]);
            setActiveSessionId(newId);
          }
        }
      } catch (err) {
        console.error('Failed to fetch sessions', err);
      }
    };

    const fetchTones = async () => {
      try {
        const res = await apiClient.fetchWithAuth('/ai/tones');
        if (res.ok) {
          const data = await res.json();
          setTones(data);
        }
      } catch (err) {
        console.error('Failed to fetch tones', err);
      }
    };

    const handleClickOutside = (event: MouseEvent) => {
      if (toneDropdownRef.current && !toneDropdownRef.current.contains(event.target as Node)) {
        setIsToneDropdownOpen(false);
      }
    };

    const fetchApiProviders = async () => {
      try {
        const res = await apiClient.fetchWithAuth('/api/settings/ai-providers');
        if (res.ok) {
          const data = await res.json();
          setApiProviders(data);
          
          const configured = data.filter((p: any) => p.isConfigured);
          if (configured.length > 0) {
            const savedProvider = Number(localStorage.getItem('notisight_ai_provider') ?? selectedProvider);
            const activeConfigured = configured.some((p: any) => p.providerType === savedProvider);
            const providerType = activeConfigured ? savedProvider : configured[0].providerType;
            const savedModel = localStorage.getItem('notisight_ai_model');

            setSelectedProvider(providerType);
            if (!savedModel) {
              setSelectedModel(providerDefaultModels[providerType] || '');
            }
          }
        }
      } catch (err) {
        console.error('Failed to fetch AI providers', err);
      }
    };

    fetchSessions();
    fetchTones();
    fetchApiProviders();
    
    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, []);

  useEffect(() => {
    localStorage.setItem('notisight_open_sessions', JSON.stringify(openSessionIds));
  }, [openSessionIds]);

  useEffect(() => {
    localStorage.setItem('notisight_ai_provider', String(selectedProvider));
    localStorage.setItem('notisight_ai_model', selectedModel);
    localStorage.setItem('notisight_ai_custom_model', customModelId);
    localStorage.setItem('notisight_ai_is_custom_model', String(isCustomModel));
  }, [selectedProvider, selectedModel, customModelId, isCustomModel]);

  useEffect(() => {
    if (activeSessionId && !activeSessionId.startsWith('new_') && activeSessionId !== 'default' && activeSession.messages.length === 0) {
      const fetchMessages = async () => {
        try {
          setIsLoading(true);
          const res = await apiClient.fetchWithAuth(`/ai/sessions/${activeSessionId}/messages`);
          if (res.ok) {
            const msgs = await res.json();
            const formattedMsgs = msgs.map((m: any) => {
              let sources = undefined;
              let citations = undefined;
              if (m.metadataJson) {
                try {
                  const meta = JSON.parse(m.metadataJson);
                  if (meta.sources) {
                    sources = meta.sources.map((s: any) => ({
                      id: s.NoteId || s.id || '',
                      title: s.Title || s.title || ''
                    }));
                  }
                  if (meta.citations) {
                    citations = meta.citations.map((c: any) => ({
                      refId: c.RefId || c.refId || '',
                      noteId: c.NoteId || c.noteId || '',
                      title: c.Title || c.title || '',
                      sourceType: c.SourceType || c.sourceType || 'note',
                      sourceLabel: c.SourceLabel || c.sourceLabel || '',
                      snippet: c.Snippet || c.snippet || ''
                    }));
                  }
                } catch(e) {}
              }
              return {
                id: m.id,
                role: m.role,
                text: m.text || m.content,
                producedByMode: m.mode,
                sources,
                citations
              };
            });
            updateSessionMessages(activeSessionId, () => formattedMsgs);
          }
        } catch (err) {
          console.error('Failed to fetch messages', err);
        } finally {
          setIsLoading(false);
        }
      };
      fetchMessages();
    }
  }, [activeSessionId]);

  const updateSessionMessages = (targetSessionId: string, updater: (prev: Message[]) => Message[]) => {
    setSessions(prevSessions => prevSessions.map(s => {
      if (s.id === targetSessionId) {
        return { ...s, messages: updater(s.messages) };
      }
      return s;
    }));
  };

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!input.trim() || isLoading) return;
    if (isCustomModel && !customModelId.trim()) {
      updateSessionMessages(activeSessionId, prev => [
        ...prev,
        { id: Date.now(), role: 'ai', text: 'Özel model seçili. Mesaj göndermeden önce AI model ayarlarından model ID girin.' }
      ]);
      return;
    }
    
    const userMsgId = Date.now();
    const userText = input;
    const isFirstMessage = messages.length === 0;
    
    let targetSessionId = activeSessionId;
    
    const historyPayload = messages.map(m => ({ role: m.role, text: m.text }));

    updateSessionMessages(targetSessionId, prev => [...prev, { id: userMsgId, role: 'user', text: userText }]);
    setInput('');
    setIsLoading(true);

    const aiMsgId = Date.now() + 1;
    updateSessionMessages(targetSessionId, prev => [...prev, { id: aiMsgId, role: 'ai', text: '' }]);
    setProgressText('Düşünüyor...');

    try {
      const response = await apiClient.fetchWithAuth('/ai/ask', {
        method: 'POST',
        body: JSON.stringify({ 
          question: userText, 
          history: historyPayload, 
          mode: sightMode ? 1 : 0,
          tone: activeTone,
          sessionId: !targetSessionId.startsWith('new_') && targetSessionId !== 'default' ? targetSessionId : undefined,
          provider: selectedProvider,
          modelId: isCustomModel ? customModelId.trim() : selectedModel
        })
      });

      if (!response.body) throw new Error('No response body');

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let aiText = '';
      let buffer = '';
      let currentEvent = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('event: ')) {
            currentEvent = line.replace('event: ', '').trim();
          } else if (line.startsWith('data: ')) {
            const dataStr = line.replace('data: ', '').trim();
            if (dataStr) {
              try {
                const data = JSON.parse(dataStr);
                if (currentEvent === 'chunk') {
                  aiText += data.content;
                  updateSessionMessages(targetSessionId, prev => prev.map(m => m.id === aiMsgId ? { ...m, text: aiText } : m));
                } else if (currentEvent === 'progress') {
                  setProgressText(data.step);
                } else if (currentEvent === 'complete') {
                  const updates: Partial<Message> = {};
                  if (data.sources && data.sources.length > 0) {
                    updates.sources = data.sources.map((s: any) => ({
                      id: s.NoteId || s.noteId || '',
                      title: s.Title || s.title || ''
                    }));
                  }
                  if (data.citations && data.citations.length > 0) {
                    updates.citations = data.citations.map((c: any) => ({
                      refId: c.RefId || c.refId || '',
                      noteId: c.NoteId || c.noteId || '',
                      title: c.Title || c.title || '',
                      sourceType: c.SourceType || c.sourceType || 'note',
                      sourceLabel: c.SourceLabel || c.sourceLabel || '',
                      snippet: c.Snippet || c.snippet || ''
                    }));
                  }
                  if (data.producedByMode) {
                    updates.producedByMode = data.producedByMode;
                  }
                  if (data.suggestModeSwitch) {
                    updates.suggestModeSwitch = true;
                  }
                  if (data.sessionId && (targetSessionId.startsWith('new_') || targetSessionId === 'default')) {
                    const oldSessionId = targetSessionId;
                    targetSessionId = data.sessionId;
                    
                    setSessions(prev => prev.map(s => s.id === oldSessionId ? { ...s, id: data.sessionId } : s));
                    setActiveSessionId(data.sessionId);
                  }
                  if (Object.keys(updates).length > 0) {
                    updateSessionMessages(targetSessionId, prev => prev.map(m => m.id === aiMsgId ? { ...m, ...updates } : m));
                  }

                  const currentTitle = sessions.find(s => s.id === activeSessionId)?.title;
                  if ((isFirstMessage || currentTitle === 'Yeni Sohbet') && targetSessionId && !targetSessionId.startsWith('new_')) {
                    apiClient.fetchWithAuth('/ai/generate-title', {
                      method: 'POST',
                      body: JSON.stringify({ 
                        question: userText,
                        sessionId: targetSessionId,
                        provider: selectedProvider,
                        modelId: isCustomModel ? customModelId.trim() : selectedModel
                      })
                    })
                    .then(res => res.json())
                    .then(titleData => {
                      if (titleData.title && titleData.title !== 'Yeni Sohbet') {
                        setSessions(prev => prev.map(s => s.id === targetSessionId ? { ...s, title: titleData.title } : s));
                      }
                    })
                    .catch(err => console.error("Title generation failed", err));
                  }
                } else if (currentEvent === 'error') {
                  aiText = data.message;
                  updateSessionMessages(targetSessionId, prev => prev.map(m => m.id === aiMsgId ? { ...m, text: aiText } : m));
                }
              } catch(err) {
                console.error('JSON parse error:', err);
              }
            }
          }
        }
      }
    } catch (err) {
      console.error(err);
      updateSessionMessages(targetSessionId, prev => prev.map(m => m.id === aiMsgId ? { ...m, text: 'Bağlantı hatası oluştu veya cevap alınamadı.' } : m));
    } finally {
      setIsLoading(false);
    }
  };

  const createNewSession = () => {
    const newId = 'new_' + Date.now().toString();
    setSessions(prev => [...prev, { id: newId, title: `Yeni Sohbet`, messages: [] }]);
    setOpenSessionIds(prev => [...prev, newId]);
    setActiveSessionId(newId);
  };

  const closeSession = (idToClose: string, e: React.MouseEvent) => {
    e.stopPropagation();

    const newOpenIds = openSessionIds.filter(id => id !== idToClose);
    
    if (newOpenIds.length === 0) {
      const newId = 'new_' + Date.now().toString();
      setSessions(prev => [...prev, { id: newId, title: 'Yeni Sohbet', messages: [] }]);
      setOpenSessionIds([newId]);
      setActiveSessionId(newId);
    } else {
      setOpenSessionIds(newOpenIds);
      if (activeSessionId === idToClose) {
        setActiveSessionId(newOpenIds[newOpenIds.length - 1]);
      }
    }
  };

  const deleteSession = async (idToDelete: string, e: React.MouseEvent) => {
    e.stopPropagation();
    
    if (!window.confirm('Bu sohbet oturumunu silmek istediğinize emin misiniz?')) return;

    try {
      if (!idToDelete.startsWith('new_')) {
        await apiClient.fetchWithAuth(`/ai/sessions/${idToDelete}`, { method: 'DELETE' });
      }

      setSessions(prev => prev.filter(s => s.id !== idToDelete));
      
      const newOpenIds = openSessionIds.filter(id => id !== idToDelete);
      if (newOpenIds.length === 0) {
        const newId = 'new_' + Date.now().toString();
        setSessions(prev => [...prev, { id: newId, title: 'Yeni Sohbet', messages: [] }]);
        setOpenSessionIds([newId]);
        setActiveSessionId(newId);
      } else {
        setOpenSessionIds(newOpenIds);
        if (activeSessionId === idToDelete) {
          setActiveSessionId(newOpenIds[newOpenIds.length - 1]);
        }
      }
    } catch (err) {
      console.error('Silme hatası:', err);
    }
  };

  return (
    <aside 
      className={`absolute right-0 md:relative z-20 h-full min-w-0 overflow-hidden shrink-0 flex flex-col ns-panel-shell shadow-2xl shadow-black/20 backdrop-blur-xl md:my-2 md:mr-2 md:h-[calc(100%-1rem)] md:rounded-3xl md:shadow-none transition-all ${width ? '' : 'w-[85%] sm:w-[320px] md:w-[307px]'}`}
      style={width ? { width: `${width}px` } : undefined}
    >
      <div className="h-12 border-b ns-hairline flex items-center justify-between px-4 shrink-0 bg-ns-bg-primary/70 backdrop-blur-xl">
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 bg-ns-primary rounded-full animate-pulse"></div>
          <span className="text-xs font-bold text-ns-text-primary tracking-wide">NOTISIGHT AI</span>
        </div>
        <button 
          onClick={onCollapse}
          className="hover:text-ns-primary-hover hover:bg-ns-surface-hover/70 p-1.5 rounded-lg transition-colors text-ns-text-secondary"
          title="Yapay Zeka'yı Kapat"
        >
          <PanelRightClose className="w-4 h-4" />
        </button>
      </div>

      <div className="flex items-center gap-2 px-3 py-2 border-b ns-hairline overflow-x-auto whitespace-nowrap scrollbar-hide bg-ns-bg-secondary/70 backdrop-blur-xl shrink-0 relative max-w-full">
        <div className="flex items-center gap-1 shrink-0">
            <button 
              onClick={() => setIsHistoryOpen(true)}
              className="flex items-center justify-center p-1.5 rounded-xl bg-ns-bg-tertiary/70 border border-ns-border/70 text-ns-text-muted hover:text-ns-primary hover:border-ns-primary/50 hover:bg-ns-surface-hover/70 transition-colors"
              title="Sohbet Geçmişi"
            >
              <History className="w-4 h-4" />
            </button>
          <button 
            onClick={createNewSession}
            className="flex items-center justify-center p-1.5 rounded-xl bg-ns-primary/10 border border-ns-primary/30 text-ns-primary hover:bg-ns-primary hover:text-white transition-colors"
            title="Yeni Sohbet"
          >
            <Plus className="w-4 h-4" />
          </button>
        </div>
        <div className="w-px h-5 bg-ns-border mx-1 shrink-0"></div>
        {sessions.filter(s => openSessionIds.includes(s.id)).map(s => (
          <div 
            key={s.id} 
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-medium cursor-pointer transition-colors border ${s.id === activeSessionId ? 'bg-ns-primary/12 border-ns-primary/30 text-ns-primary shadow-sm shadow-ns-primary/10' : 'bg-ns-bg-tertiary/70 border-ns-border/70 text-ns-text-muted hover:bg-ns-surface-hover/70 hover:text-ns-text-primary'}`}
            onClick={() => setActiveSessionId(s.id)}
          >
            <span>{s.title}</span>
            <X 
              className="w-3 h-3 hover:text-red-400 opacity-70 hover:opacity-100 transition-opacity" 
              onClick={(e) => closeSession(s.id, e)}
            />
          </div>
        ))}
      </div>

      <div ref={chatContainerRef} className="flex-1 min-w-0 max-w-full p-4 space-y-6 overflow-y-auto overflow-x-hidden min-h-0 no-scrollbar">
        <AnimatePresence mode="popLayout">
          {messages.length === 0 ? (
            <motion.div 
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="flex flex-col items-center justify-center h-full text-center text-ns-text-muted mt-10"
            >
              <div className="w-12 h-12 bg-ns-surface-hover rounded-full flex items-center justify-center mb-4">
                <Bot className="w-6 h-6 text-ns-text-secondary" />
              </div>
              <p className="text-sm font-medium text-ns-text-primary mb-1">Soru Sormaya Başlayın</p>
              <p className="text-xs">Notlarınızı analiz etmek veya yeni fikirler üretmek için ilk sorunuzu sorun.</p>
            </motion.div>
          ) : (
            messages.map(msg => (
              <motion.div 
                key={`${activeSessionId}-${msg.id}`}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className={`flex w-full min-w-0 max-w-full flex-col ${msg.role === 'user' ? 'items-end' : 'items-start'}`}
              >
                {msg.role === 'ai' && msg.producedByMode && (
                  <div className="flex items-center gap-1.5 mb-1">
                    <span className={`text-[9px] font-bold uppercase px-1.5 py-0.5 rounded-full border ${
                      msg.producedByMode === 'Notisight'
                        ? 'bg-ns-primary/10 text-ns-primary border-ns-primary/20'
                        : 'bg-blue-500/10 text-blue-400 border-blue-500/20'
                    }`}>
                      {msg.producedByMode === 'Notisight' ? '👁 Sight' : '💬 Standard'}
                    </span>
                  </div>
                )}
                <div 
                  className={`p-3 text-sm min-w-0 overflow-hidden font-medium ${
                    msg.role === 'user' 
                      ? 'max-w-[85%] bg-ns-bg-tertiary/85 text-ns-text-primary rounded-2xl whitespace-pre-line break-words shadow-sm shadow-black/10' 
                      : 'w-full max-w-full bg-ns-green-surface/44 backdrop-blur-md border border-ns-primary/25 shadow-[0_10px_34px_-22px_rgba(46,204,113,0.36)] text-ns-text-primary rounded-2xl prose prose-sm dark:prose-invert prose-p:leading-relaxed prose-pre:bg-ns-bg-tertiary/80 prose-pre:backdrop-blur-sm prose-pre:border prose-pre:border-ns-border prose-pre:shadow-sm prose-a:text-ns-primary prose-a:no-underline hover:prose-a:underline prose-strong:text-ns-text-primary'
                  }`}
                >
                  {msg.role === 'user' ? (
                    msg.text
                  ) : (
                    msg.text ? (
                      <MarkdownContent msg={msg} />
                    ) : (
                      isLoading && (
                        <div className="flex items-center gap-3 py-1">
                          <div className="flex gap-1">
                            <span className="w-1.5 h-1.5 rounded-full bg-ns-primary/60 animate-bounce" style={{ animationDelay: '0ms' }}></span>
                            <span className="w-1.5 h-1.5 rounded-full bg-ns-primary/60 animate-bounce" style={{ animationDelay: '150ms' }}></span>
                            <span className="w-1.5 h-1.5 rounded-full bg-ns-primary/60 animate-bounce" style={{ animationDelay: '300ms' }}></span>
                          </div>
                          <span className="text-xs text-ns-text-muted animate-pulse">{progressText}</span>
                        </div>
                      )
                    )
                  )}
                </div>
                
                {msg.citations && msg.citations.length > 0 && (
                  <div className="mt-3 flex flex-col gap-2 w-full">
                    <div className="text-[10px] text-ns-text-muted font-bold uppercase pl-1 flex items-center gap-1.5">
                      <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20"/></svg>
                      Kaynaklar
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {msg.citations.map((cite, idx) => (
                        <button 
                          key={idx}
                          onClick={() => onSelectNote && cite.noteId && onSelectNote(cite.noteId)}
                          className="bg-ns-bg-tertiary/75 border border-ns-border/75 p-2 rounded-xl flex items-center gap-2.5 text-left hover:bg-ns-surface-hover/70 hover:border-ns-primary/40 transition-all group cursor-pointer max-w-full sm:max-w-[220px]"
                          title={cite.snippet}
                        >
                          <div className="w-5 h-5 bg-ns-primary/10 rounded-md flex items-center justify-center text-ns-primary shrink-0 group-hover:bg-ns-primary/20 transition-colors font-bold text-[10px]">
                            {idx + 1}
                          </div>
                          <div className="flex-1 min-w-0 pr-1">
                            <div className="text-[11px] font-medium text-ns-text-primary truncate">
                              {cite.title || 'İsimsiz Dosya'}
                            </div>
                            <div className="text-[9px] text-ns-text-muted truncate mt-0.5">
                              {cite.sourceLabel || 'Belge'}
                            </div>
                          </div>
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {msg.suggestModeSwitch && (
                  <button
                    onClick={() => setSightMode(false)}
                    className="mt-2 w-full text-left text-xs bg-amber-500/10 border border-amber-500/20 text-amber-300 px-3 py-2 rounded-xl hover:bg-amber-500/20 transition-colors flex items-center gap-2"
                  >
                    <EyeOff className="w-3.5 h-3.5 shrink-0" />
                    <span>Bu konu notlarında yok. <strong>Standard moda</strong> geçeyim mi?</span>
                  </button>
                )}

                {msg.sources && msg.sources.length > 0 && (!msg.citations || msg.citations.length === 0) && (
                  <div className="mt-2 flex flex-col gap-1.5 w-full max-w-[90%]">
                    <div className="text-[10px] text-ns-text-muted font-bold uppercase pl-1">Kaynak Referanslar</div>
                    {msg.sources.map((src, idx) => (
                      <button 
                        key={idx}
                        onClick={() => onSelectNote && src.id && onSelectNote(src.id)}
                        className="bg-ns-bg-tertiary/75 border border-ns-border/75 p-2 rounded-xl flex items-center gap-3 text-left hover:bg-ns-surface-hover/70 transition-colors group cursor-pointer"
                      >
                        <div className="w-8 h-8 bg-ns-primary/10 rounded flex items-center justify-center text-ns-primary shrink-0 group-hover:bg-ns-primary/20 transition-colors">
                          <FileText className="w-4 h-4" />
                        </div>
                        <div className="flex-1 min-w-0 pr-2">
                          <div className="text-[11px] font-medium text-ns-text-primary truncate">{src.title || 'İsimsiz Dosya'}</div>
                        </div>
                      </button>
                    ))}
                  </div>
                )}
              </motion.div>
            ))
          )}
        </AnimatePresence>
      </div>

      <div className="p-3 sm:p-4 border-t ns-hairline shrink-0 bg-ns-bg-secondary/72 backdrop-blur-xl space-y-2 min-w-0">
        <div className="flex min-w-0 flex-wrap items-center justify-between gap-2 px-1">
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setSightMode(!sightMode)}
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] font-semibold transition-all border ${
                sightMode
                  ? 'bg-ns-primary/15 text-ns-primary border-ns-primary/30 shadow-sm shadow-ns-primary/10'
                  : 'bg-ns-bg-tertiary/70 text-ns-text-muted border-ns-border/70 hover:bg-ns-surface-hover/70 hover:text-ns-text-primary'
              }`}
              title={sightMode ? 'Sight Mode aktif — Notlarından cevap verir' : 'Standard mod — Serbest AI sohbeti'}
            >
              {sightMode ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
              Sight Mode
            </button>

            {tones.length > 0 && (
              <div className="relative" ref={toneDropdownRef}>
                <button
                  type="button"
                  onClick={() => setIsToneDropdownOpen(!isToneDropdownOpen)}
                  className={`flex items-center gap-1.5 bg-ns-bg-tertiary/70 border text-ns-text-primary text-[11px] font-semibold rounded-xl px-2.5 py-1 transition-colors focus:outline-none ${
                    isToneDropdownOpen ? 'border-ns-primary/50 shadow-sm shadow-ns-primary/10' : 'border-ns-border/70 hover:bg-ns-surface-hover/70 hover:border-ns-primary/30'
                  }`}
                  title="AI Davranış Tonu"
                >
                  <span className="text-[12px]">{tones.find(t => t.value === activeTone)?.icon || '💬'}</span>
                  <span>{tones.find(t => t.value === activeTone)?.displayName}</span>
                  <ChevronUp className={`w-3 h-3 text-ns-text-muted transition-transform duration-200 ${isToneDropdownOpen ? 'rotate-180' : ''}`} />
                </button>
                
                <AnimatePresence>
                  {isToneDropdownOpen && (
                    <motion.div
                      initial={{ opacity: 0, y: 5, scale: 0.95 }}
                      animate={{ opacity: 1, y: 0, scale: 1 }}
                      exit={{ opacity: 0, y: 5, scale: 0.95 }}
                      transition={{ duration: 0.15, ease: "easeOut" }}
                      className="absolute bottom-[calc(100%+8px)] left-0 min-w-[150px] ns-glass border border-ns-border/80 rounded-xl shadow-[0_8px_30px_rgb(0,0,0,0.28)] overflow-hidden z-50 flex flex-col p-1.5 backdrop-blur-xl"
                    >
                      <div className="px-2 py-1.5 mb-1 border-b border-ns-border/50">
                        <span className="text-[9px] font-bold tracking-wider text-ns-text-disabled uppercase">Davranış Tonu Seç</span>
                      </div>
                      {tones.map(t => (
                        <button
                          key={t.value}
                          type="button"
                          onClick={() => {
                            setActiveTone(t.value);
                            setIsToneDropdownOpen(false);
                          }}
                          className={`flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-left text-[11px] font-medium transition-all ${
                            activeTone === t.value 
                              ? 'bg-ns-primary/15 text-ns-primary' 
                              : 'text-ns-text-secondary hover:bg-ns-surface-hover hover:text-ns-text-primary'
                          }`}
                        >
                          <span className="text-[14px]">{t.icon}</span>
                          <span className="flex-1">{t.displayName}</span>
                        </button>
                      ))}
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            )}

            {apiProviders.filter(p => p.isConfigured).length > 0 && (
              <button
                type="button"
                onClick={() => setIsAISettingsOpen(true)}
                className="flex items-center gap-1.5 bg-ns-bg-tertiary/70 border text-ns-text-muted border-ns-border/70 hover:bg-ns-surface-hover/70 hover:text-ns-text-primary hover:border-ns-primary/30 text-[11px] font-semibold rounded-xl px-2.5 py-1 transition-colors focus:outline-none"
                title="AI Model Ayarları"
              >
                <Settings className="w-3.5 h-3.5" />
                <span className="hidden sm:inline">Model</span>
              </button>
            )}
          </div>
          <span className="min-w-0 truncate text-[10px] text-ns-text-disabled">
            {sightMode ? 'Notlardan cevap' : 'Serbest sohbet'}
          </span>
        </div>
        <form 
          onSubmit={handleSend}
          className="relative flex min-w-0 items-center"
        >
          <input 
            type="text"
            className="w-full bg-ns-bg-tertiary/78 border border-ns-border/70 rounded-2xl py-2.5 px-4 text-sm focus:outline-none focus:border-ns-primary placeholder:text-ns-text-disabled text-ns-text-primary shadow-inner shadow-black/5"
            placeholder={sightMode ? 'Notlarına sor...' : 'Bir şey sor...'}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            disabled={isLoading}
          />
          <button 
            type="submit"
            disabled={isLoading || !input.trim()}
            className="absolute right-2 p-1.5 bg-ns-primary text-ns-bg-primary rounded-xl hover:bg-ns-primary-hover disabled:opacity-50 transition-colors shadow-sm shadow-ns-primary/20"
          >
            <Send className="w-4 h-4" />
          </button>
        </form>
      </div>

      <AnimatePresence>
        {isHistoryOpen && (
          <>
            <motion.div 
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.15 }}
              className="absolute inset-0 bg-black/60 backdrop-blur-sm z-40"
              onClick={() => setIsHistoryOpen(false)}
            />
            <motion.div 
              initial={{ opacity: 0, scale: 0.95, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 20 }}
              transition={{ duration: 0.2 }}
              className="absolute top-16 left-4 right-4 bottom-16 bg-ns-bg-primary border border-ns-border/50 rounded-2xl shadow-2xl z-50 flex flex-col overflow-hidden"
            >
              <div className="px-5 py-4 border-b border-ns-border flex items-center justify-between bg-ns-surface">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-ns-primary/10 flex items-center justify-center">
                    <History className="w-4 h-4 text-ns-primary" />
                  </div>
                  <h3 className="text-sm font-semibold text-ns-text-primary">Sohbet Geçmişi</h3>
                </div>
                <button 
                  onClick={() => setIsHistoryOpen(false)}
                  className="p-1.5 rounded-lg hover:bg-ns-bg-tertiary text-ns-text-muted hover:text-ns-text-primary transition-colors"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
              
              <div className="flex-1 overflow-y-auto p-3 space-y-1 custom-scrollbar bg-ns-bg-primary">
                {sessions.map(s => (
                  <button 
                    key={s.id} 
                    onClick={() => {
                      if (!openSessionIds.includes(s.id)) {
                        setOpenSessionIds(prev => [...prev, s.id]);
                      }
                      setActiveSessionId(s.id);
                      setIsHistoryOpen(false);
                    }}
                    className={`w-full flex items-center justify-between px-4 py-3 rounded-xl hover:bg-ns-bg-tertiary transition-all group ${openSessionIds.includes(s.id) ? 'bg-ns-bg-tertiary/50 border border-ns-primary/20' : 'border border-transparent'}`}
                  >
                    <div className="flex flex-col items-start gap-1">
                      <span className={`text-sm font-medium ${openSessionIds.includes(s.id) ? 'text-ns-primary' : 'text-ns-text-primary group-hover:text-ns-primary/80'}`}>
                        {s.title}
                      </span>
                      {openSessionIds.includes(s.id) && (
                        <span className="text-[10px] text-ns-text-muted mt-0.5 px-1.5 py-0.5 rounded-md bg-ns-bg-primary border border-ns-border">Açık</span>
                      )}
                    </div>
                    <button 
                      onClick={(e) => deleteSession(s.id, e)}
                      className="p-1.5 rounded-lg text-ns-text-muted hover:bg-red-500/10 hover:text-red-400 transition-colors opacity-0 group-hover:opacity-100"
                      title="Sohbeti Sil"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </button>
                ))}
                {sessions.length === 0 && (
                  <div className="h-full flex items-center justify-center text-sm text-ns-text-muted">
                    Henüz kayıtlı bir sohbetiniz bulunmuyor.
                  </div>
                )}
              </div>
            </motion.div>
          </>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {isAISettingsOpen && (
          <>
            <motion.div 
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 bg-black/60 backdrop-blur-sm z-40"
              onClick={() => setIsAISettingsOpen(false)}
            />
            <motion.div 
              initial={{ opacity: 0, scale: 0.95, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 20 }}
              className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[90%] max-w-sm bg-ns-bg-primary border border-ns-border/50 rounded-2xl shadow-2xl z-50 flex flex-col overflow-hidden"
            >
              <div className="px-5 py-4 border-b border-ns-border flex items-center justify-between bg-ns-surface">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-ns-primary/10 flex items-center justify-center">
                    <Settings className="w-4 h-4 text-ns-primary" />
                  </div>
                  <h3 className="text-sm font-semibold text-ns-text-primary">AI Modeli Seçimi</h3>
                </div>
                <button 
                  onClick={() => setIsAISettingsOpen(false)}
                  className="p-1.5 rounded-lg hover:bg-ns-bg-tertiary text-ns-text-muted hover:text-ns-text-primary transition-colors"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
              
              <div className="p-5 space-y-4">
                <div className="space-y-1.5">
                  <label className="text-xs font-medium text-ns-text-secondary">Sağlayıcı</label>
                  <select
                    value={selectedProvider}
                    onChange={(e) => {
                      const pType = parseInt(e.target.value);
                      setSelectedProvider(pType);
                      setIsCustomModel(false);
                      setSelectedModel(providerDefaultModels[pType] || '');
                    }}
                    className="w-full bg-ns-bg-secondary border border-ns-border rounded-lg px-3 py-2.5 text-sm text-ns-text-primary focus:outline-none focus:border-ns-primary"
                  >
                    {apiProviders.filter(p => p.isConfigured).map(p => {
                      const names: Record<number, string> = {0: 'OpenAI', 1: 'DashScope', 2: 'Anthropic', 3: 'Gemini', 4: 'DeepSeek', 5: 'OpenRouter / Özel Sunucu', 6: 'Grok'};
                      return <option key={p.providerType} value={p.providerType}>{names[p.providerType] || 'Bilinmeyen'}</option>;
                    })}
                  </select>
                </div>

                <div className="space-y-1.5">
                  <label className="text-xs font-medium text-ns-text-secondary">Model</label>
                  <select
                    value={isCustomModel ? 'other' : selectedModel}
                    onChange={(e) => {
                      if (e.target.value === 'other') {
                        setIsCustomModel(true);
                      } else {
                        setIsCustomModel(false);
                        setSelectedModel(e.target.value);
                      }
                    }}
                    className="w-full bg-ns-bg-secondary border border-ns-border rounded-lg px-3 py-2.5 text-sm text-ns-text-primary focus:outline-none focus:border-ns-primary"
                  >
                    {(modelsByProvider[selectedProvider] || []).map(m => (
                      <option key={m.id} value={m.id}>{m.name}</option>
                    ))}
                    <option value="other">Diğer (Özel Model ID)</option>
                  </select>
                  {selectedProvider === 5 && (
                    <p className="text-[11px] leading-4 text-ns-text-muted">
                      OpenRouter veya özel OpenAI-compatible sunucu için model ID kullanılır. Base URL ayarı Hesabım &gt; API Bağlantıları bölümündedir.
                    </p>
                  )}
                </div>

                {isCustomModel && (
                  <div className="space-y-1.5 animate-in fade-in slide-in-from-top-1">
                    <label className="text-xs font-medium text-ns-text-secondary">Özel Model ID</label>
                    <input
                      type="text"
                      value={customModelId}
                      onChange={(e) => setCustomModelId(e.target.value)}
                      placeholder={selectedProvider === 5 ? 'örn: meta-llama/llama-3.1-70b-instruct' : 'örn: sağlayıcının model kimliği'}
                      className="w-full block bg-ns-bg-secondary border border-ns-border rounded-lg px-3 py-2.5 text-sm text-ns-text-primary focus:outline-none focus:border-ns-primary"
                    />
                  </div>
                )}

                {isCustomModel && !customModelId.trim() && (
                  <p className="rounded-xl border border-ns-warning/25 bg-ns-warning/10 px-3 py-2 text-xs text-ns-warning">
                    Özel model seçiliyse mesaj göndermeden önce model ID girin.
                  </p>
                )}

                <div className="pt-2">
                  <button 
                    onClick={() => setIsAISettingsOpen(false)}
                    className="w-full bg-ns-primary hover:bg-ns-primary-hover text-white text-sm font-medium rounded-lg px-4 py-2.5 transition-colors"
                  >
                    Tamam
                  </button>
                </div>
              </div>
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </aside>
  );
};
