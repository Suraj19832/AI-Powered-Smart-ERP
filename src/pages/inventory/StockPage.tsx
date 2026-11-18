import { useState } from 'react'
import { Button, MenuItem, TextField } from '@mui/material'
import TuneIcon from '@mui/icons-material/Tune'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { FormDialog } from '../../components/FormDialog'
import { useGet, useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { dateTime, money2, num } from '../../utils/format'
import type { Lookup, StockTransaction, Warehouse } from '../../types'

const TYPES = ['PurchaseReceipt', 'SaleIssue', 'AdjustmentIn', 'AdjustmentOut', 'OpeningBalance']

export default function StockPage() {
  const [type, setType] = useState('')
  const [warehouseId, setWarehouseId] = useState<number | ''>('')
  const [adjusting, setAdjusting] = useState(false)
  const { data: warehouses = [] } = useGet<Warehouse[]>('/warehouses')
  const { data: products = [] } = useGet<Lookup[]>('/products/lookup')
  const [mutate] = useMutate()
  const canAdjust = useHasRole(...R.stock)

  const columns: GridColDef<StockTransaction>[] = [
    { field: 'transactionDate', headerName: 'Date', width: 170, valueFormatter: (v: string) => dateTime(v) },
    { field: 'type', headerName: 'Type', width: 150, renderCell: (p) => <StatusChip status={p.value} /> },
    { field: 'sku', headerName: 'SKU', width: 120, sortable: false },
    { field: 'productName', headerName: 'Product', flex: 1, minWidth: 220, sortable: false },
    { field: 'warehouseName', headerName: 'Warehouse', width: 170, sortable: false },
    {
      field: 'quantity', headerName: 'Qty', width: 100, type: 'number',
      renderCell: (p) => <span style={{ color: p.value < 0 ? '#d03b3b' : '#0ca30c', fontWeight: 600 }}>{p.value > 0 ? '+' : ''}{num(p.value)}</span>,
    },
    { field: 'unitCost', headerName: 'Unit cost', width: 120, type: 'number', valueFormatter: (v: number) => money2(v) },
    { field: 'reference', headerName: 'Reference', width: 150 },
    { field: 'notes', headerName: 'Notes', width: 200, sortable: false },
  ]

  return (
    <>
      <PageHeader title="Stock movements" subtitle="Every stock in/out is recorded as an auditable transaction"
        actions={canAdjust && <Button variant="contained" startIcon={<TuneIcon />} onClick={() => setAdjusting(true)}>Stock adjustment</Button>} />
      <ServerGrid<StockTransaction>
        url="/stock/transactions"
        columns={columns}
        params={{ type: type || undefined, warehouseId: warehouseId || undefined }}
        searchPlaceholder="Search product, SKU or reference…"
        filters={<>
          <TextField select label="Type" value={type} onChange={(e) => setType(e.target.value)} sx={{ minWidth: 180 }}>
            <MenuItem value="">All types</MenuItem>
            {TYPES.map((t) => <MenuItem key={t} value={t}>{t.replace(/([a-z])([A-Z])/g, '$1 $2')}</MenuItem>)}
          </TextField>
          <TextField select label="Warehouse" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value as number | '')} sx={{ minWidth: 200 }}>
            <MenuItem value="">All warehouses</MenuItem>
            {warehouses.map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
          </TextField>
        </>}
      />
      <FormDialog
        open={adjusting}
        title="Stock adjustment"
        submitText="Post adjustment"
        initial={{ warehouseId: warehouses[0]?.id }}
        fields={[
          { name: 'productId', label: 'Product', type: 'select', required: true, span: 2, options: products.map((p) => ({ value: p.id, label: `${p.name} (on hand ${num(p.stock)})` })) },
          { name: 'warehouseId', label: 'Warehouse', type: 'select', required: true, options: warehouses.filter((w) => w.isActive).map((w) => ({ value: w.id, label: w.name })) },
          { name: 'quantity', label: 'Quantity (+ in / − out)', type: 'number', required: true },
          { name: 'reason', label: 'Reason', required: true, span: 2, helperText: 'e.g. damaged goods, stock count variance' },
        ]}
        onClose={() => setAdjusting(false)}
        onSubmit={(v) => mutate('/stock/adjust', 'POST', v)}
      />
    </>
  )
}
