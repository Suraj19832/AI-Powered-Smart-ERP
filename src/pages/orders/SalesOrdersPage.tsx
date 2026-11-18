import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, MenuItem, TextField } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { date, money } from '../../utils/format'
import type { SalesOrderListItem } from '../../types'

export default function SalesOrdersPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState('')
  const canCreate = useHasRole(...R.sell)

  const columns: GridColDef<SalesOrderListItem>[] = [
    { field: 'orderNumber', headerName: 'Order #', width: 160 },
    { field: 'orderDate', headerName: 'Date', width: 130, valueFormatter: (v: string) => date(v) },
    { field: 'customerName', headerName: 'Customer', flex: 1, minWidth: 220, sortable: false },
    { field: 'status', headerName: 'Status', width: 130, renderCell: (p) => <StatusChip status={p.value} /> },
    { field: 'invoiceNumber', headerName: 'Invoice', width: 160, sortable: false, valueFormatter: (v?: string) => v ?? '—' },
    { field: 'totalAmount', headerName: 'Total', width: 150, type: 'number', valueFormatter: (v: number) => money(v) },
  ]

  return (
    <>
      <PageHeader title="Sales orders" subtitle="Quotations and invoiced orders"
        actions={canCreate && <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/sales-orders/new')}>New quotation</Button>} />
      <ServerGrid<SalesOrderListItem>
        url="/sales-orders" columns={columns} params={{ status: status || undefined }}
        searchPlaceholder="Search order # or customer…"
        onRowClick={(r) => navigate(`/sales-orders/${r.id}`)}
        filters={
          <TextField select label="Status" value={status} onChange={(e) => setStatus(e.target.value)} sx={{ minWidth: 160 }}>
            <MenuItem value="">All</MenuItem>
            {['Quotation', 'Invoiced', 'Cancelled'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </TextField>
        }
      />
    </>
  )
}
