import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { MenuItem, TextField } from '@mui/material'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { date, money2, words } from '../../utils/format'
import type { Payment } from '../../types'

export default function PaymentsPage() {
  const navigate = useNavigate()
  const [direction, setDirection] = useState('')

  const columns: GridColDef<Payment>[] = [
    { field: 'paymentNumber', headerName: 'Payment #', width: 160 },
    { field: 'paymentDate', headerName: 'Date', width: 120, valueFormatter: (v: string) => date(v) },
    { field: 'direction', headerName: 'Direction', width: 120, renderCell: (p) => <StatusChip status={p.value} /> },
    { field: 'partyName', headerName: 'Customer / supplier', flex: 1, minWidth: 200, sortable: false },
    { field: 'invoiceNumber', headerName: 'Document', width: 170, sortable: false, valueGetter: (_: unknown, r: Payment) => r.invoiceNumber ?? r.purchaseOrderNumber },
    { field: 'method', headerName: 'Method', width: 130, valueFormatter: (v: string) => words(v) },
    { field: 'reference', headerName: 'Reference', width: 150 },
    { field: 'amount', headerName: 'Amount', width: 140, type: 'number', valueFormatter: (v: number) => money2(v) },
  ]

  return (
    <>
      <PageHeader title="Payments" subtitle="Customer receipts and supplier payments — record them from an invoice or purchase order" />
      <ServerGrid<Payment>
        url="/payments" columns={columns} params={{ direction: direction || undefined }}
        searchPlaceholder="Search payment #, reference, party…"
        onRowClick={(r) => navigate(r.invoiceId ? `/invoices/${r.invoiceId}` : `/purchase-orders/${r.purchaseOrderId}`)}
        filters={
          <TextField select label="Direction" value={direction} onChange={(e) => setDirection(e.target.value)} sx={{ minWidth: 200 }}>
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Incoming">Incoming (customers)</MenuItem>
            <MenuItem value="Outgoing">Outgoing (suppliers)</MenuItem>
          </TextField>
        }
      />
    </>
  )
}
