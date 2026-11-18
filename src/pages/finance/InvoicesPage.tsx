import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { MenuItem, TextField } from '@mui/material'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { date, money } from '../../utils/format'
import type { InvoiceListItem } from '../../types'

export default function InvoicesPage() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState('')

  const columns: GridColDef<InvoiceListItem>[] = [
    { field: 'invoiceNumber', headerName: 'Invoice #', width: 160 },
    { field: 'invoiceDate', headerName: 'Date', width: 120, valueFormatter: (v: string) => date(v) },
    { field: 'dueDate', headerName: 'Due', width: 120, valueFormatter: (v: string) => date(v) },
    { field: 'customerName', headerName: 'Customer', flex: 1, minWidth: 200, sortable: false },
    { field: 'status', headerName: 'Status', width: 140, renderCell: (p) => <StatusChip status={p.value} overdue={p.row.isOverdue} /> },
    { field: 'totalAmount', headerName: 'Total', width: 140, type: 'number', valueFormatter: (v: number) => money(v) },
    { field: 'amountPaid', headerName: 'Paid', width: 130, type: 'number', valueFormatter: (v: number) => money(v) },
    { field: 'balanceDue', headerName: 'Balance', width: 130, type: 'number', sortable: false, valueFormatter: (v: number) => money(v) },
  ]

  const params = filter === 'open' ? { openOnly: true } : filter === 'overdue' ? { overdueOnly: true } : filter ? { status: filter } : {}

  return (
    <>
      <PageHeader title="Invoices" subtitle="Customer invoices and receivables" />
      <ServerGrid<InvoiceListItem>
        url="/invoices" columns={columns} params={params}
        searchPlaceholder="Search invoice # or customer…"
        onRowClick={(r) => navigate(`/invoices/${r.id}`)}
        filters={
          <TextField select label="Show" value={filter} onChange={(e) => setFilter(e.target.value)} sx={{ minWidth: 180 }}>
            <MenuItem value="">All invoices</MenuItem>
            <MenuItem value="open">Open (unpaid / partial)</MenuItem>
            <MenuItem value="overdue">Overdue</MenuItem>
            <MenuItem value="Paid">Paid</MenuItem>
            <MenuItem value="Cancelled">Cancelled</MenuItem>
          </TextField>
        }
      />
    </>
  )
}
