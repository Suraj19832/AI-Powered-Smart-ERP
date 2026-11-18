import { SimpleListPage } from './SimpleListPage'
import { R } from '../../roles'
import type { Category } from '../../types'

export default function CategoriesPage() {
  return (
    <SimpleListPage<Category>
      title="Categories" subtitle="Product categories" url="/categories" entity="category"
      editRoles={R.stock} deleteRoles={R.stock}
      columns={[
        { field: 'name', headerName: 'Name', flex: 1, minWidth: 200 },
        { field: 'description', headerName: 'Description', flex: 2, minWidth: 240 },
        { field: 'productCount', headerName: 'Products', width: 110, type: 'number' },
      ]}
      fields={[
        { name: 'name', label: 'Name', required: true, span: 2 },
        { name: 'description', label: 'Description', type: 'textarea' },
      ]}
    />
  )
}
