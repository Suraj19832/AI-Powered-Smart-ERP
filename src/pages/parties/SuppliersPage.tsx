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
import type { Supplier } from '../../types'
import { supplierFields } from './partyFields'

export default function SuppliersPage() {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [mutate] = useMutate()
  const canEdit = useHasRole(...R.buy)

  const columns: GridColDef<Supplier>[] = [
    { field: 'code', headerName: 'Code', width: 100 },
    { field: 'name', headerName: 'Name', flex: 1, minWidth: 200 },
    { field: 'contactPerson', headerName: 'Contact', width: 150 },
    { field: 'city', headerName: 'City', width: 120 },
    { field: 'phone', headerName: 'Phone', width: 150, sortable: false },
    { field: 'leadTimeDays', headerName: 'Lead time', width: 100, type: 'number', valueFormatter: (v: number) => `${v} days` },
    { field: 'payableBalance', headerName: 'Payable', width: 140, type: 'number', sortable: false, valueFormatter: (v: number) => money(v) },
    { field: 'isActive', headerName: 'Status', width: 100, renderCell: (p) => <StatusChip status={p.value ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <>
      <PageHeader title="Suppliers" subtitle="Supplier profiles, lead times and payables"
        actions={canEdit && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>New supplier</Button>} />
      <ServerGrid<Supplier> url="/suppliers" columns={columns} searchPlaceholder="Search name, code, contact…" onRowClick={(r) => navigate(`/suppliers/${r.id}`)} />
      <FormDialog open={open} title="New supplier" fields={supplierFields} initial={{ leadTimeDays: 7 }} onClose={() => setOpen(false)}
        onSubmit={async (v) => { const r = await mutate<{ id: number }>('/suppliers', 'POST', v); navigate(`/suppliers/${r.id}`) }} />
    </>
  )
}
