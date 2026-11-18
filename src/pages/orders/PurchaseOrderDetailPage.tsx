import { useState } from 'react'
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom'
import { Box, Button, Link } from '@mui/material'
import SendIcon from '@mui/icons-material/SendOutlined'
import InventoryIcon from '@mui/icons-material/Inventory2Outlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import BlockIcon from '@mui/icons-material/Block'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import PaymentsIcon from '@mui/icons-material/PaymentsOutlined'
import { useGet, useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { ErrorAlert, InfoRow, Loading, PageHeader, SectionCard, StatusChip, useConfirm } from '../../components/common'
import { LinesTable, PaymentDialog, PaymentsTable, Totals } from '../../components/DocumentParts'
import { date, money2 } from '../../utils/format'
import type { PurchaseOrder } from '../../types'

export default function PurchaseOrderDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: o, isLoading, error } = useGet<PurchaseOrder>(`/purchase-orders/${id}`)
  const [mutate, { isLoading: busy }] = useMutate()
  const [actionError, setActionError] = useState<unknown>(null)
  const [paying, setPaying] = useState(false)
  const [confirm, dialog] = useConfirm()
  const canBuy = useHasRole(...R.buy)
  const canReceive = useHasRole(...R.stock)
  const canPay = useHasRole(...R.finance)

  if (isLoading) return <Loading />
  if (!o) return <ErrorAlert error={error} />

  const open = o.status === 'Draft' || o.status === 'Ordered'
  const run = async (title: string, message: string, fn: () => Promise<unknown>) => {
    if (!(await confirm({ title, message }))) return
    try { setActionError(null); await fn() } catch (e) { setActionError(e) }
  }

  return (
    <>
      <PageHeader
        title={`Purchase order ${o.orderNumber}`}
        subtitle={<>{o.supplierName} · {date(o.orderDate)} · <StatusChip status={o.status} /></>}
        actions={<>
          {canBuy && o.status === 'Draft' && <Button variant="outlined" startIcon={<SendIcon />} disabled={busy}
            onClick={() => run('Send to supplier?', 'The purchase order will be marked as Ordered.', () => mutate(`/purchase-orders/${o.id}/order`))}>Mark ordered</Button>}
          {canReceive && open && <Button variant="contained" startIcon={<InventoryIcon />} disabled={busy}
            onClick={() => run('Receive goods?', 'All lines will be received into stock and product costs updated (weighted average). This runs as one database transaction.',
              () => mutate(`/purchase-orders/${o.id}/receive`))}>Receive goods</Button>}
          {canPay && o.status !== 'Cancelled' && o.balanceDue > 0 && <Button variant="outlined" startIcon={<PaymentsIcon />} onClick={() => setPaying(true)}>Record payment</Button>}
          {canBuy && open && <Button startIcon={<EditIcon />} onClick={() => navigate(`/purchase-orders/${o.id}/edit`)}>Edit</Button>}
          {canBuy && open && <Button color="warning" startIcon={<BlockIcon />} disabled={busy}
            onClick={() => run('Cancel purchase order?', 'The order will be cancelled.', () => mutate(`/purchase-orders/${o.id}/cancel`))}>Cancel</Button>}
          {canBuy && o.status === 'Draft' && <Button color="error" startIcon={<DeleteIcon />} disabled={busy}
            onClick={() => run('Delete draft?', 'This cannot be undone.', async () => { await mutate(`/purchase-orders/${o.id}`, 'DELETE'); navigate('/purchase-orders') })}>Delete</Button>}
        </>}
      />
      <ErrorAlert error={actionError} />
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '3fr 1fr' }, gap: 2 }}>
        <Box sx={{ display: 'grid', gap: 2 }}>
          <SectionCard title="Items">
            <LinesTable lines={o.lines} priceLabel="Unit cost" />
            <Totals rows={[{ label: 'Subtotal', value: o.subTotal }, { label: 'Tax', value: o.taxAmount }, { label: 'Total', value: o.totalAmount, bold: true },
              { label: 'Paid', value: o.amountPaid }, { label: 'Balance due', value: o.balanceDue, bold: true }]} />
          </SectionCard>
          <SectionCard title="Supplier payments"><PaymentsTable payments={o.payments} /></SectionCard>
        </Box>
        <SectionCard title="Details">
          <InfoRow label="Supplier" value={<Link component={RouterLink} to={`/suppliers/${o.supplierId}`}>{o.supplierName}</Link>} />
          <InfoRow label="Warehouse" value={o.warehouseName} />
          <InfoRow label="Supplier invoice" value={o.supplierInvoiceNumber ?? '—'} />
          <InfoRow label="Expected" value={date(o.expectedDate)} />
          <InfoRow label="Received" value={date(o.receivedDate)} />
          <InfoRow label="Balance due" value={money2(o.balanceDue)} />
          <InfoRow label="Notes" value={o.notes ?? '—'} />
        </SectionCard>
      </Box>
      <PaymentDialog open={paying} onClose={() => setPaying(false)} balance={o.balanceDue} purchaseOrderId={o.id} />
      {dialog}
    </>
  )
}
