import { SimpleListPage } from './SimpleListPage'
import { StatusChip } from '../../components/common'
import { R } from '../../roles'
import type { Warehouse } from '../../types'

export default function WarehousesPage() {
  return (
    <SimpleListPage<Warehouse>
      title="Warehouses" subtitle="Storage locations for stock" url="/warehouses" entity="warehouse"
      editRoles={R.stock} deleteRoles={R.managers}
      columns={[
        { field: 'code', headerName: 'Code', width: 110 },
        { field: 'name', headerName: 'Name', flex: 1, minWidth: 200 },
        { field: 'location', headerName: 'Location', flex: 1, minWidth: 200 },
        { field: 'isActive', headerName: 'Status', width: 110, renderCell: (p) => <StatusChip status={p.value ? 'Active' : 'Inactive'} /> },
      ]}
      fields={[
        { name: 'code', label: 'Code', required: true },
        { name: 'name', label: 'Name', required: true },
        { name: 'location', label: 'Location', span: 2 },
        { name: 'isActive', label: 'Active', type: 'switch' },
      ]}
    />
  )
}
