import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { FormDialog } from '../../components/FormDialog'
import { useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { money } from '../../utils/format'
import type { Customer } from '../../types'
import { customerFields } from './partyFields'

export default function CustomersPage() {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [mutate] = useMutate()
  const canEdit = useHasRole(...R.sell)

  const columns: GridColDef<Customer>[] = [
    { field: 'code', headerName: 'Code', width: 100 },
    { field: 'name', headerName: 'Name', flex: 1, minWidth: 200 },
    { field: 'city', headerName: 'City', width: 130 },
    { field: 'phone', headerName: 'Phone', width: 150, sortable: false },
    { field: 'email', headerName: 'Email', width: 220 },
    { field: 'creditLimit', headerName: 'Credit limit', width: 130, type: 'number', valueFormatter: (v: number) => money(v) },
    { field: 'outstandingBalance', headerName: 'Outstanding', width: 140, type: 'number', sortable: false, valueFormatter: (v: number) => money(v) },
    { field: 'isActive', headerName: 'Status', width: 100, renderCell: (p) => <StatusChip status={p.value ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <>
      <PageHeader title="Customers" subtitle="Customer profiles, credit and outstanding balances"
        actions={canEdit && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>New customer</Button>} />
      <ServerGrid<Customer> url="/customers" columns={columns} searchPlaceholder="Search name, code, email, city…" onRowClick={(r) => navigate(`/customers/${r.id}`)} />
      <FormDialog open={open} title="New customer" fields={customerFields} onClose={() => setOpen(false)}
        onSubmit={async (v) => { const r = await mutate<{ id: number }>('/customers', 'POST', v); navigate(`/customers/${r.id}`) }} />
    </>
  )
}
