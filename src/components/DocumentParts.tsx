import { Box, Divider, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import type { DocPayment, OrderLine } from '../types'
import { date, money2, num, todayIso, words } from '../utils/format'
import { FormDialog } from './FormDialog'
import { useMutate } from '../app/api'

export function LinesTable({ lines, priceLabel = 'Unit price' }: { lines: OrderLine[]; priceLabel?: string }) {
  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>#</TableCell><TableCell>Product</TableCell><TableCell align="right">Qty</TableCell>
            <TableCell align="right">{priceLabel}</TableCell><TableCell align="right">Tax %</TableCell><TableCell align="right">Line total</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {lines.map((l, i) => (
            <TableRow key={l.id}>
              <TableCell>{i + 1}</TableCell>
              <TableCell><Typography variant="body2" sx={{ fontWeight: 500 }}>{l.productName}</Typography><Typography variant="caption" color="text.secondary">{l.sku}</Typography></TableCell>
              <TableCell align="right">{num(l.quantity)}</TableCell>
              <TableCell align="right">{money2(l.unitPrice)}</TableCell>
              <TableCell align="right">{num(l.taxRate)}</TableCell>
              <TableCell align="right">{money2(l.lineTotal)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

export function Totals({ rows }: { rows: { label: string; value: number; bold?: boolean }[] }) {
  return (
    <Box sx={{ ml: 'auto', maxWidth: 320, mt: 2 }}>
      {rows.map((r, i) => (
        <Box key={r.label}>
          {r.bold && i > 0 && <Divider sx={{ my: 0.5 }} />}
          <Stack direction="row" sx={{ justifyContent: 'space-between', py: 0.4 }}>
            <Typography variant="body2" color={r.bold ? 'text.primary' : 'text.secondary'} sx={{ fontWeight: r.bold ? 700 : 400 }}>{r.label}</Typography>
            <Typography variant="body2" sx={{ fontWeight: r.bold ? 700 : 500 }}>{money2(r.value)}</Typography>
          </Stack>
        </Box>
      ))}
    </Box>
  )
}

export function PaymentsTable({ payments }: { payments: DocPayment[] }) {
  if (payments.length === 0) return <Typography variant="body2" color="text.secondary">No payments recorded yet.</Typography>
  return (
    <Table size="small">
      <TableHead><TableRow><TableCell>Number</TableCell><TableCell>Date</TableCell><TableCell>Method</TableCell><TableCell>Reference</TableCell><TableCell align="right">Amount</TableCell></TableRow></TableHead>
      <TableBody>
        {payments.map((p) => (
          <TableRow key={p.id}>
            <TableCell>{p.paymentNumber}</TableCell><TableCell>{date(p.paymentDate)}</TableCell><TableCell>{words(p.method)}</TableCell>
            <TableCell>{p.reference ?? '—'}</TableCell><TableCell align="right">{money2(p.amount)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

export const PAYMENT_METHODS = ['BankTransfer', 'Upi', 'Cash', 'Card', 'Cheque'].map((m) => ({ value: m, label: words(m) }))

/** Records a customer receipt (invoiceId) or supplier payment (purchaseOrderId). */
export function PaymentDialog({ open, onClose, balance, invoiceId, purchaseOrderId }: {
  open: boolean; onClose: () => void; balance: number; invoiceId?: number; purchaseOrderId?: number
}) {
  const [mutate] = useMutate()
  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={invoiceId ? 'Record customer payment' : 'Record supplier payment'}
      submitText="Record payment"
      initial={{ amount: balance, method: 'BankTransfer', paymentDate: todayIso() }}
      fields={[
        { name: 'amount', label: 'Amount', type: 'number', required: true, min: 0.01, max: balance, helperText: `Balance due ${money2(balance)}` },
        { name: 'method', label: 'Method', type: 'select', required: true, options: PAYMENT_METHODS },
        { name: 'paymentDate', label: 'Payment date', type: 'date', required: true },
        { name: 'reference', label: 'Reference / UTR' },
        { name: 'notes', label: 'Notes', type: 'textarea' },
      ]}
      onSubmit={(v) => mutate('/payments', 'POST', {
        ...v, direction: invoiceId ? 'Incoming' : 'Outgoing', invoiceId, purchaseOrderId,
      })}
    />
  )
}
