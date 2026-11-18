import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Alert, Avatar, Box, Card, Chip, IconButton, Stack, TextField, Tooltip, Typography } from '@mui/material'
import SendIcon from '@mui/icons-material/Send'
import SmartToyIcon from '@mui/icons-material/SmartToyOutlined'
import PersonIcon from '@mui/icons-material/PersonOutlined'
import BuildIcon from '@mui/icons-material/BuildOutlined'
import RestartAltIcon from '@mui/icons-material/RestartAlt'
import { errorMessage, useChatMutation } from '../../app/api'
import { Markdown, PageHeader } from '../../components/common'
import { useSettings } from '../../components/AppContext'
import type { ChatReply, ChatTurn } from '../../types'

const SUGGESTIONS = [
  "What were this month's total sales?",
  'Which products are low in stock?',
  'Show pending customer payments',
  'What should I reorder?',
  'Forecast sales for the next 3 months',
  'Profit and loss for last month',
  'Who are my top customers this year?',
  'Compare expenses this quarter vs last quarter',
]

interface Msg extends ChatTurn { tools?: ChatReply['toolsUsed']; mode?: string; error?: boolean }

const STORAGE = 'smarterp.chat'

export default function AssistantPage() {
  const settings = useSettings()
  const [messages, setMessages] = useState<Msg[]>(() => {
    try { return JSON.parse(sessionStorage.getItem(STORAGE) ?? '[]') as Msg[] } catch { return [] }
  })
  const [input, setInput] = useState('')
  const [chat, { isLoading }] = useChatMutation()
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    try { sessionStorage.setItem(STORAGE, JSON.stringify(messages.slice(-30))) } catch { /* ignore */ }
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isLoading])

  const send = async (text: string) => {
    const q = text.trim()
    if (!q || isLoading) return
    setInput('')
    const history: Msg[] = [...messages, { role: 'user', content: q }]
    setMessages(history)
    try {
      const res = await chat({ messages: history.filter((m) => !m.error).map(({ role, content }) => ({ role, content })) }).unwrap()
      setMessages((m) => [...m, { role: 'assistant', content: res.reply, tools: res.toolsUsed, mode: res.mode }])
    } catch (e) {
      setMessages((m) => [...m, { role: 'assistant', content: errorMessage(e), error: true }])
    }
  }

  const submit = (e: FormEvent) => { e.preventDefault(); send(input) }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: { md: 'calc(100vh - 112px)' } }}>
      <PageHeader title="AI Assistant" subtitle="Ask questions about your business in plain English"
        actions={messages.length > 0 && <Tooltip title="New conversation"><IconButton onClick={() => setMessages([])} aria-label="New conversation"><RestartAltIcon /></IconButton></Tooltip>} />
      {!settings.aiEnabled && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Running in <b>offline mode</b>: common questions are answered by the built-in engine. Set <code>OpenAI:ApiKey</code> in the API configuration for free-form conversation.
        </Alert>
      )}
      <Card sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', minHeight: 420 }}>
        <Box sx={{ flexGrow: 1, overflowY: 'auto', p: { xs: 2, md: 3 } }}>
          {messages.length === 0 && (
            <Stack spacing={2} sx={{ alignItems: 'center', textAlign: 'center', py: 6 }}>
              <Avatar sx={{ width: 56, height: 56, bgcolor: 'primary.main' }}><SmartToyIcon /></Avatar>
              <Typography variant="h6">How can I help with your business today?</Typography>
              <Typography color="text.secondary" variant="body2" sx={{ maxWidth: 520 }}>
                I read live ERP data through secure, read-only services — I never run raw database queries and can’t change records.
              </Typography>
              <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1, justifyContent: 'center', maxWidth: 760 }}>
                {SUGGESTIONS.map((s) => <Chip key={s} label={s} onClick={() => send(s)} variant="outlined" clickable />)}
              </Stack>
            </Stack>
          )}
          <Stack spacing={2.5}>
            {messages.map((m, i) => (
              <Stack key={i} direction="row" spacing={1.5} sx={{ alignItems: 'flex-start', flexDirection: m.role === 'user' ? 'row-reverse' : 'row' }}>
                <Avatar sx={{ width: 32, height: 32, bgcolor: m.role === 'user' ? 'secondary.main' : 'primary.main', ml: m.role === 'user' ? 1.5 : 0 }}>
                  {m.role === 'user' ? <PersonIcon fontSize="small" /> : <SmartToyIcon fontSize="small" />}
                </Avatar>
                <Box sx={{
                  maxWidth: { xs: '85%', md: '75%' }, px: 2, py: 1.2, borderRadius: 3, minWidth: 0,
                  bgcolor: m.role === 'user' ? 'primary.main' : m.error ? 'error.main' : 'action.hover',
                  color: m.role === 'user' || m.error ? 'primary.contrastText' : 'text.primary',
                }}>
                  {m.role === 'user' ? <Typography sx={{ whiteSpace: 'pre-wrap' }}>{m.content}</Typography> : <Markdown>{m.content}</Markdown>}
                  {m.tools && m.tools.length > 0 && (
                    <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.5, mt: 1 }}>
                      {m.tools.map((t, j) => (
                        <Tooltip key={j} title={t.arguments}>
                          <Chip size="small" icon={<BuildIcon />} label={t.name} variant="outlined" sx={{ fontSize: 11 }} />
                        </Tooltip>
                      ))}
                      {m.mode && <Chip size="small" label={m.mode === 'openai' ? 'OpenAI' : 'offline engine'} sx={{ fontSize: 11 }} />}
                    </Stack>
                  )}
                </Box>
              </Stack>
            ))}
            {isLoading && (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                <Avatar sx={{ width: 32, height: 32, bgcolor: 'primary.main' }}><SmartToyIcon fontSize="small" /></Avatar>
                <Typography color="text.secondary" variant="body2">Analysing your data…</Typography>
              </Stack>
            )}
          </Stack>
          <div ref={endRef} />
        </Box>
        <Box component="form" onSubmit={submit} sx={{ p: 2, borderTop: 1, borderColor: 'divider', display: 'flex', gap: 1 }}>
          <TextField fullWidth placeholder="e.g. Which customers have overdue invoices?" value={input} onChange={(e) => setInput(e.target.value)}
            disabled={isLoading} slotProps={{ htmlInput: { maxLength: 2000 } }} />
          <IconButton type="submit" color="primary" disabled={!input.trim() || isLoading} aria-label="Send"><SendIcon /></IconButton>
        </Box>
      </Card>
    </Box>
  )
}
