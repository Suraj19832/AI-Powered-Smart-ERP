import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, MenuItem, TextField } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DocumentScannerIcon from '@mui/icons-material/DocumentScannerOutlined'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { date, money } from '../../utils/format'
import type { PurchaseOrderListItem } from '../../types'

export default function PurchaseOrdersPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState('')
  const canCreate = useHasRole(...R.buy)

  const columns: GridColDef<PurchaseOrderListItem>[] = [
    { field: 'orderNumber', headerName: 'PO #', width: 160 },
    { field: 'orderDate', headerName: 'Ordered', width: 120, valueFormatter: (v: string) => date(v) },
    { field: 'supplierName', headerName: 'Supplier', flex: 1, minWidth: 200, sortable: false },
    { field: 'status', headerName: 'Status', width: 120, renderCell: (p) => <StatusChip status={p.value} /> },
    { field: 'receivedDate', headerName: 'Received', width: 120, valueFormatter: (v?: string) => date(v) },
    { field: 'totalAmount', headerName: 'Total', width: 140, type: 'number', valueFormatter: (v: number) => money(v) },
    { field: 'balanceDue', headerName: 'Balance', width: 140, type: 'number', sortable: false, valueFormatter: (v: number) => money(v) },
  ]

  return (
    <>
      <PageHeader title="Purchase orders" subtitle="Orders to suppliers, receiving and payables"
        actions={canCreate && <>
          <Button variant="outlined" startIcon={<DocumentScannerIcon />} onClick={() => navigate('/ai/invoice-reader')}>From invoice (AI)</Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/purchase-orders/new')}>New purchase order</Button>
        </>} />
      <ServerGrid<PurchaseOrderListItem>
        url="/purchase-orders" columns={columns} params={{ status: status || undefined }}
        searchPlaceholder="Search PO #, supplier or supplier invoice…"
        onRowClick={(r) => navigate(`/purchase-orders/${r.id}`)}
        filters={
          <TextField select label="Status" value={status} onChange={(e) => setStatus(e.target.value)} sx={{ minWidth: 160 }}>
            <MenuItem value="">All</MenuItem>
            {['Draft', 'Ordered', 'Received', 'Cancelled'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </TextField>
        }
      />
    </>
  )
}
