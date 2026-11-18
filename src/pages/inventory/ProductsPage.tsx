import { useState } from 'react'
import { Button, FormControlLabel, IconButton, MenuItem, Switch, TextField, Tooltip } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import WarningIcon from '@mui/icons-material/WarningAmberRounded'
import type { GridColDef } from '@mui/x-data-grid'
import { ErrorAlert, PageHeader, StatusChip, useConfirm } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { FormDialog, type Field } from '../../components/FormDialog'
import { useGet, useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { money, money2, num } from '../../utils/format'
import type { Category, Lookup, Product } from '../../types'

export default function ProductsPage() {
  const [categoryId, setCategoryId] = useState<number | ''>('')
  const [lowOnly, setLowOnly] = useState(false)
  const [editing, setEditing] = useState<Product | null | undefined>(undefined)
  const [actionError, setActionError] = useState<unknown>(null)
  const { data: categories = [] } = useGet<Category[]>('/categories')
  const { data: suppliers = [] } = useGet<Lookup[]>('/suppliers/lookup')
  const [mutate] = useMutate()
  const [confirm, confirmDialog] = useConfirm()
  const canEdit = useHasRole(...R.stock)
  const canDelete = useHasRole(...R.managers)

  const fields: Field[] = [
    { name: 'sku', label: 'SKU', required: true },
    { name: 'name', label: 'Product name', required: true },
    { name: 'categoryId', label: 'Category', type: 'select', required: true, options: categories.map((c) => ({ value: c.id, label: c.name })) },
    { name: 'preferredSupplierId', label: 'Preferred supplier', type: 'select', options: suppliers.map((s) => ({ value: s.id, label: s.name })) },
    { name: 'unit', label: 'Unit', required: true, helperText: 'pcs, box, ream…' },
    { name: 'taxRate', label: 'Tax rate %', type: 'number', min: 0, max: 100 },
    { name: 'costPrice', label: 'Cost price', type: 'number', min: 0 },
    { name: 'salePrice', label: 'Sale price', type: 'number', min: 0 },
    { name: 'reorderLevel', label: 'Reorder level', type: 'number', min: 0 },
    { name: 'isActive', label: 'Active', type: 'switch' },
    { name: 'description', label: 'Description', type: 'textarea' },
  ]

  const remove = async (p: Product) => {
    if (!(await confirm({ title: 'Delete product?', message: `Delete ${p.name}?`, confirmText: 'Delete' }))) return
    try { setActionError(null); await mutate(`/products/${p.id}`, 'DELETE') } catch (e) { setActionError(e) }
  }

  const columns: GridColDef<Product>[] = [
    { field: 'sku', headerName: 'SKU', width: 120 },
    { field: 'name', headerName: 'Product', flex: 1, minWidth: 220 },
    { field: 'categoryName', headerName: 'Category', width: 160, sortable: false },
    {
      field: 'quantityOnHand', headerName: 'On hand', width: 120, type: 'number',
      renderCell: (p) => (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontWeight: 600 }}>
          {p.row.isLowStock && <Tooltip title={`At or below reorder level (${num(p.row.reorderLevel)})`}><WarningIcon color="warning" fontSize="small" /></Tooltip>}
          {num(p.value)} {p.row.unit}
        </span>
      ),
    },
    { field: 'reorderLevel', headerName: 'Reorder lvl', width: 105, type: 'number' },
    { field: 'costPrice', headerName: 'Cost', width: 120, type: 'number', valueFormatter: (v: number) => money2(v) },
    { field: 'salePrice', headerName: 'Price', width: 120, type: 'number', valueFormatter: (v: number) => money2(v) },
    { field: 'stockValue', headerName: 'Stock value', width: 130, type: 'number', sortable: false, valueFormatter: (v: number) => money(v) },
    { field: 'preferredSupplierName', headerName: 'Supplier', width: 170, sortable: false },
    { field: 'isActive', headerName: 'Status', width: 95, renderCell: (p) => <StatusChip status={p.value ? 'Active' : 'Inactive'} /> },
    {
      field: 'actions', headerName: '', width: 96, sortable: false,
      renderCell: (p) => (
        <>
          {canEdit && <IconButton size="small" aria-label="Edit" onClick={() => setEditing(p.row)}><EditIcon fontSize="small" /></IconButton>}
          {canDelete && <IconButton size="small" aria-label="Delete" onClick={() => remove(p.row)}><DeleteIcon fontSize="small" /></IconButton>}
        </>
      ),
    },
  ]

  return (
    <>
      <PageHeader title="Products" subtitle="Catalogue, pricing and stock levels"
        actions={canEdit && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing(null)}>New product</Button>} />
      <ErrorAlert error={actionError} />
      <ServerGrid<Product>
        url="/products"
        columns={columns}
        params={{ categoryId: categoryId || undefined, lowStockOnly: lowOnly || undefined }}
        searchPlaceholder="Search name or SKU…"
        filters={<>
          <TextField select label="Category" value={categoryId} onChange={(e) => setCategoryId(e.target.value as number | '')} sx={{ minWidth: 200 }}>
            <MenuItem value="">All categories</MenuItem>
            {categories.map((c) => <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}
          </TextField>
          <FormControlLabel control={<Switch checked={lowOnly} onChange={(e) => setLowOnly(e.target.checked)} />} label="Low stock only" />
        </>}
      />
      <FormDialog
        open={editing !== undefined}
        title={editing ? `Edit ${editing.name}` : 'New product'}
        fields={fields}
        initial={editing ?? { unit: 'pcs', taxRate: 18, isActive: true }}
        onClose={() => setEditing(undefined)}
        onSubmit={(v) => editing ? mutate(`/products/${editing.id}`, 'PUT', v) : mutate('/products', 'POST', v)}
      />
      {confirmDialog}
    </>
  )
}
