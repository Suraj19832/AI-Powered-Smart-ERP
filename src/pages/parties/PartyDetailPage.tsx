import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Box, Button, Card, CardContent, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import AddIcon from '@mui/icons-material/Add'
import { errorMessage, useGet, useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { ErrorAlert, InfoRow, Loading, PageHeader, SectionCard, StatusChip, useConfirm } from '../../components/common'
import { FormDialog } from '../../components/FormDialog'
import { date, money } from '../../utils/format'
import type { CustomerDetail, SupplierDetail } from '../../types'
import { customerFields, supplierFields } from './partyFields'

export default function PartyDetailPage({ kind }: { kind: 'customer' | 'supplier' }) {
  const { id } = useParams()
  const navigate = useNavigate()
  const base = kind === 'customer' ? '/customers' : '/suppliers'
  const { data, isLoading, error } = useGet<CustomerDetail & SupplierDetail>(`${base}/${id}`)
  const [mutate] = useMutate()
  const [edit, setEdit] = useState(false)
  const [actionError, setActionError] = useState<unknown>(null)
  const [confirm, confirmDialog] = useConfirm()
  const canEdit = useHasRole(...(kind === 'customer' ? R.sell : R.buy))
  const canDelete = useHasRole(...R.managers)

  if (isLoading) return <Loading />
  if (!data) return <ErrorAlert error={error} />

  const party = kind === 'customer' ? data.customer : data.supplier
  const total = kind === 'customer' ? data.totalSales : data.totalPurchases
  const balance = kind === 'customer' ? data.customer.outstandingBalance : data.supplier.payableBalance

  const remove = async () => {
    if (!(await confirm({ title: `Delete ${kind}?`, message: `Delete ${party.name}? This cannot be undone.`, confirmText: 'Delete' }))) return
    try { await mutate(`${base}/${id}`, 'DELETE'); navigate(base) } catch (e) { setActionError({ data: { detail: errorMessage(e) } }) }
  }

  const docLink = (t: { type: string; id: number }) =>
    t.type === 'Sales Order' ? `/sales-orders/${t.id}` : t.type === 'Purchase Order' ? `/purchase-orders/${t.id}` : null

  return (
    <>
      <PageHeader
        title={party.name}
        subtitle={<>{party.code} · {party.city ?? '—'} · <StatusChip status={party.isActive ? 'Active' : 'Inactive'} /></>}
        actions={<>
          {canEdit && kind === 'customer' && <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/sales-orders/new', { state: { prefill: { partyId: party.id, lines: [] } } })}>New sales order</Button>}
          {canEdit && kind === 'supplier' && <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/purchase-orders/new', { state: { prefill: { partyId: party.id, lines: [] } } })}>New purchase order</Button>}
          {canEdit && <Button variant="outlined" startIcon={<EditIcon />} onClick={() => setEdit(true)}>Edit</Button>}
          {canDelete && <Button color="error" startIcon={<DeleteIcon />} onClick={remove}>Delete</Button>}
        </>}
      />
      <ErrorAlert error={actionError} />

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' }, gap: 2, mb: 2 }}>
        <Stat label={kind === 'customer' ? 'Lifetime sales' : 'Total purchases'} value={money(total)} />
        <Stat label="Total paid" value={money(data.totalPaid)} />
        <Stat label={kind === 'customer' ? 'Outstanding' : 'Payable'} value={money(balance)} highlight={balance > 0} />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 2fr' }, gap: 2 }}>
        <SectionCard title="Profile">
          {kind === 'supplier' && <InfoRow label="Contact person" value={data.supplier.contactPerson ?? '—'} />}
          <InfoRow label="Email" value={party.email ?? '—'} />
          <InfoRow label="Phone" value={party.phone ?? '—'} />
          <InfoRow label="Address" value={party.address ?? '—'} />
          <InfoRow label="GSTIN / Tax no." value={party.taxNumber ?? '—'} />
          {kind === 'customer' && <InfoRow label="Credit limit" value={money(data.customer.creditLimit)} />}
          {kind === 'supplier' && <InfoRow label="Lead time" value={`${data.supplier.leadTimeDays} days`} />}
          <InfoRow label="Orders" value={data.orderCount} />
          <InfoRow label="Since" value={date(party.createdAt)} />
        </SectionCard>
        <SectionCard title="Transaction history">
          <Box sx={{ maxHeight: 520, overflow: 'auto' }}>
            <Table size="small" stickyHeader>
              <TableHead><TableRow><TableCell>Date</TableCell><TableCell>Type</TableCell><TableCell>Number</TableCell><TableCell>Status</TableCell><TableCell align="right">Amount</TableCell></TableRow></TableHead>
              <TableBody>
                {data.transactions.map((t) => {
                  const link = docLink(t)
                  return (
                    <TableRow key={`${t.type}-${t.id}`} hover={!!link} sx={{ cursor: link ? 'pointer' : 'default' }} onClick={() => link && navigate(link)}>
                      <TableCell>{date(t.date)}</TableCell><TableCell>{t.type}</TableCell><TableCell>{t.number}</TableCell>
                      <TableCell>{t.type === 'Payment' ? t.status : <StatusChip status={t.status} />}</TableCell>
                      <TableCell align="right">{money(t.amount)}</TableCell>
                    </TableRow>
                  )
                })}
                {data.transactions.length === 0 && <TableRow><TableCell colSpan={5}><Typography color="text.secondary">No transactions yet.</Typography></TableCell></TableRow>}
              </TableBody>
            </Table>
          </Box>
        </SectionCard>
      </Box>

      <FormDialog open={edit} title={`Edit ${kind}`} fields={kind === 'customer' ? customerFields : supplierFields} initial={party as unknown as Record<string, unknown>}
        onClose={() => setEdit(false)} onSubmit={(v) => mutate(`${base}/${id}`, 'PUT', v)} />
      {confirmDialog}
    </>
  )
}

function Stat({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) {
  return (
    <Card><CardContent>
      <Typography variant="body2" color="text.secondary">{label}</Typography>
      <Typography variant="h5" color={highlight ? 'warning.main' : 'text.primary'}>{value}</Typography>
    </CardContent></Card>
  )
}
