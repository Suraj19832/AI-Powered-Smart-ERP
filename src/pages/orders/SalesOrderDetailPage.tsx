import { useState } from 'react'
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom'
import { Alert, Box, Button, Link } from '@mui/material'
import CheckIcon from '@mui/icons-material/TaskAlt'
import EditIcon from '@mui/icons-material/EditOutlined'
import BlockIcon from '@mui/icons-material/Block'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { useGet, useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { ErrorAlert, InfoRow, Loading, PageHeader, SectionCard, StatusChip, useConfirm } from '../../components/common'
import { LinesTable, Totals } from '../../components/DocumentParts'
import { date } from '../../utils/format'
import type { SalesOrder } from '../../types'

export default function SalesOrderDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: o, isLoading, error } = useGet<SalesOrder>(`/sales-orders/${id}`)
  const [mutate, { isLoading: busy }] = useMutate()
  const [actionError, setActionError] = useState<unknown>(null)
  const [confirm, dialog] = useConfirm()
  const canEdit = useHasRole(...R.sell)

  if (isLoading) return <Loading />
  if (!o) return <ErrorAlert error={error} />

  const run = async (title: string, message: string, fn: () => Promise<unknown>) => {
    if (!(await confirm({ title, message }))) return
    try { setActionError(null); await fn() } catch (e) { setActionError(e) }
  }

  return (
    <>
      <PageHeader
        title={`Sales order ${o.orderNumber}`}
        subtitle={<>{o.customerName} · {date(o.orderDate)} · <StatusChip status={o.status} /></>}
        actions={canEdit && <>
          {o.status === 'Quotation' && <Button variant="contained" startIcon={<CheckIcon />} disabled={busy}
            onClick={() => run('Confirm order?', 'Stock will be issued from the warehouse and an invoice will be raised. This runs as one database transaction.',
              async () => { const r = await mutate<{ invoiceId: number }>(`/sales-orders/${o.id}/confirm`); navigate(`/invoices/${r.invoiceId}`) })}>
            Confirm & invoice</Button>}
          {o.status === 'Quotation' && <Button variant="outlined" startIcon={<EditIcon />} onClick={() => navigate(`/sales-orders/${o.id}/edit`)}>Edit</Button>}
          {o.status !== 'Cancelled' && <Button color="warning" startIcon={<BlockIcon />} disabled={busy}
            onClick={() => run('Cancel order?', o.status === 'Invoiced' ? 'The invoice will be cancelled and stock returned to the warehouse.' : 'The quotation will be cancelled.',
              () => mutate(`/sales-orders/${o.id}/cancel`))}>Cancel order</Button>}
          {o.status === 'Quotation' && <Button color="error" startIcon={<DeleteIcon />} disabled={busy}
            onClick={() => run('Delete quotation?', 'This cannot be undone.', async () => { await mutate(`/sales-orders/${o.id}`, 'DELETE'); navigate('/sales-orders') })}>Delete</Button>}
        </>}
      />
      <ErrorAlert error={actionError} />
      {o.invoiceId && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Invoiced as <Link component={RouterLink} to={`/invoices/${o.invoiceId}`}>{o.invoiceNumber}</Link>
        </Alert>
      )}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '3fr 1fr' }, gap: 2 }}>
        <SectionCard title="Items">
          <LinesTable lines={o.lines} />
          <Totals rows={[
            { label: 'Subtotal', value: o.subTotal }, { label: 'Tax', value: o.taxAmount },
            ...(o.discount ? [{ label: 'Discount', value: -o.discount }] : []), { label: 'Total', value: o.totalAmount, bold: true },
          ]} />
        </SectionCard>
        <SectionCard title="Details">
          <InfoRow label="Customer" value={<Link component={RouterLink} to={`/customers/${o.customerId}`}>{o.customerName}</Link>} />
          <InfoRow label="Warehouse" value={o.warehouseName} />
          <InfoRow label="Order date" value={date(o.orderDate)} />
          <InfoRow label="Status" value={<StatusChip status={o.status} />} />
          <InfoRow label="Notes" value={o.notes ?? '—'} />
        </SectionCard>
      </Box>
      {dialog}
    </>
  )
}
