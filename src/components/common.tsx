import { useState, type ReactNode } from 'react'
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogContentText,
  DialogTitle, Stack, Typography, type ChipProps,
} from '@mui/material'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { errorMessage } from '../app/api'
import { words } from '../utils/format'

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 3, alignItems: { sm: 'center' }, justifyContent: 'space-between' }}>
      <Box>
        <Typography variant="h5">{title}</Typography>
        {subtitle && <Typography component="div" color="text.secondary" variant="body2">{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>{actions}</Stack>}
    </Stack>
  )
}

const STATUS_COLORS: Record<string, ChipProps['color']> = {
  Quotation: 'info', Invoiced: 'success', Cancelled: 'default', Draft: 'default', Ordered: 'info', Received: 'success',
  Unpaid: 'warning', PartiallyPaid: 'info', Paid: 'success', Overdue: 'error', Incoming: 'success', Outgoing: 'warning',
  Critical: 'error', High: 'warning', Medium: 'info', Low: 'default', Active: 'success', Inactive: 'default',
  PurchaseReceipt: 'success', SaleIssue: 'warning', AdjustmentIn: 'info', AdjustmentOut: 'error', OpeningBalance: 'default',
}

export function StatusChip({ status, overdue }: { status: string; overdue?: boolean }) {
  const s = overdue ? 'Overdue' : status
  return <Chip size="small" label={words(s)} color={STATUS_COLORS[s] ?? 'default'} variant={STATUS_COLORS[s] === 'default' ? 'outlined' : 'filled'} />
}

export function ErrorAlert({ error }: { error: unknown }) {
  if (!error) return null
  return <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>
}

export function Loading({ label }: { label?: string }) {
  return (
    <Stack sx={{ alignItems: 'center', py: 6 }} spacing={1}>
      <CircularProgress size={28} />
      {label && <Typography variant="body2" color="text.secondary">{label}</Typography>}
    </Stack>
  )
}

export function SectionCard({ title, action, children, sx }: { title?: ReactNode; action?: ReactNode; children: ReactNode; sx?: object }) {
  return (
    <Card sx={{ height: '100%', ...sx }}>
      <CardContent>
        {(title || action) && (
          <Stack direction="row" sx={{ mb: 1.5, alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
            {typeof title === 'string' ? <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>{title}</Typography> : title}
            {action}
          </Stack>
        )}
        {children}
      </CardContent>
    </Card>
  )
}

export function Markdown({ children }: { children: string }) {
  return (
    <Box
      sx={{
        '& h1': { fontSize: 22, mt: 0, mb: 1.5 }, '& h2': { fontSize: 17, mt: 2.5, mb: 1 }, '& h3': { fontSize: 15, mt: 2, mb: 1 },
        '& p': { my: 1, lineHeight: 1.6 }, '& ul, & ol': { pl: 3, my: 1 }, '& li': { mb: 0.5 },
        '& table': { borderCollapse: 'collapse', my: 1.5, fontSize: 13, display: 'block', overflowX: 'auto', maxWidth: '100%' },
        '& th, & td': { border: 1, borderColor: 'divider', px: 1.2, py: 0.6, textAlign: 'left', whiteSpace: 'nowrap' },
        '& th': { bgcolor: 'action.hover', fontWeight: 600 },
        '& code': { bgcolor: 'action.hover', px: 0.5, borderRadius: 1, fontSize: 12 },
        '& em': { color: 'text.secondary' },
      }}
    >
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{children}</ReactMarkdown>
    </Box>
  )
}

/** Confirmation dialog driven by a promise: `if (await confirm({...})) ...` */
export function useConfirm() {
  const [state, setState] = useState<{ title: string; message: string; confirmText?: string; resolve: (v: boolean) => void } | null>(null)
  const confirm = (opts: { title: string; message: string; confirmText?: string }) =>
    new Promise<boolean>((resolve) => setState({ ...opts, resolve }))
  const close = (v: boolean) => { state?.resolve(v); setState(null) }
  const dialog = (
    <Dialog open={!!state} onClose={() => close(false)}>
      <DialogTitle>{state?.title}</DialogTitle>
      <DialogContent><DialogContentText>{state?.message}</DialogContentText></DialogContent>
      <DialogActions>
        <Button onClick={() => close(false)}>Cancel</Button>
        <Button variant="contained" onClick={() => close(true)}>{state?.confirmText ?? 'Confirm'}</Button>
      </DialogActions>
    </Dialog>
  )
  return [confirm, dialog] as const
}

export function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Stack direction="row" sx={{ justifyContent: 'space-between', py: 0.6, gap: 2 }}>
      <Typography variant="body2" color="text.secondary">{label}</Typography>
      <Typography component="div" variant="body2" sx={{ fontWeight: 500, textAlign: 'right' }}>{value}</Typography>
    </Stack>
  )
}
