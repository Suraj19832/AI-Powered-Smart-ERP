import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import {
  Alert, Autocomplete, Box, Button, Card, CardContent, IconButton, MenuItem, Table, TableBody, TableCell, TableHead, TableRow,
  TextField, Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { errorMessage, useGet, useMutate } from '../../app/api'
import { Loading, PageHeader } from '../../components/common'
import { Totals } from '../../components/DocumentParts'
import { money2, num, todayIso } from '../../utils/format'
import type { Lookup, OrderPrefill, Paged, Product, PurchaseOrder, SalesOrder, Warehouse } from '../../types'

interface Line { key: number; productId: number | null; quantity: number; unitPrice: number; taxRate: number }
let keySeq = 1
const newLine = (): Line => ({ key: keySeq++, productId: null, quantity: 1, unitPrice: 0, taxRate: 0 })
const today = todayIso

export default function OrderFormPage({ kind }: { kind: 'sales' | 'purchase' }) {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const prefill = (location.state as { prefill?: OrderPrefill } | null)?.prefill
  const isSales = kind === 'sales'
  const base = isSales ? '/sales-orders' : '/purchase-orders'

  const { data: existing, isLoading } = useGet<SalesOrder & PurchaseOrder>(id ? `${base}/${id}` : null)
  const { data: parties = [] } = useGet<Lookup[]>(isSales ? '/customers/lookup' : '/suppliers/lookup')
  const { data: warehouses = [] } = useGet<Warehouse[]>('/warehouses')
  const { data: productPage } = useGet<Paged<Product>>('/products', { pageSize: 200, activeOnly: true, sortBy: 'name' })
  const products = useMemo(() => productPage?.items ?? [], [productPage])
  const [mutate, { isLoading: saving }] = useMutate()

  const [partyId, setPartyId] = useState<number | null>(prefill?.partyId ?? null)
  const [warehouseId, setWarehouseId] = useState<number | ''>('')
  const [orderDate, setOrderDate] = useState(today())
  const [expectedDate, setExpectedDate] = useState('')
  const [discount, setDiscount] = useState(0)
  const [notes, setNotes] = useState(prefill?.notes ?? '')
  const [supplierInvoiceNumber, setSupplierInvoiceNumber] = useState(prefill?.supplierInvoiceNumber ?? '')
  const [lines, setLines] = useState<Line[]>(() =>
    prefill?.lines.length ? prefill.lines.map((l) => ({ key: keySeq++, ...l })) : [newLine()])
  const [error, setError] = useState<string | null>(null)

  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    if (!existing) return
    setPartyId(isSales ? existing.customerId : existing.supplierId)
    setWarehouseId(existing.warehouseId)
    setOrderDate(existing.orderDate.slice(0, 10))
    setExpectedDate(existing.expectedDate?.slice(0, 10) ?? '')
    setDiscount(existing.discount ?? 0)
    setNotes(existing.notes ?? '')
    setSupplierInvoiceNumber(existing.supplierInvoiceNumber ?? '')
    setLines(existing.lines.map((l) => ({ key: keySeq++, productId: l.productId, quantity: l.quantity, unitPrice: l.unitPrice, taxRate: l.taxRate })))
  }, [existing, isSales])

  useEffect(() => {
    if (warehouseId === '' && warehouses.length) setWarehouseId(warehouses.find((w) => w.isActive)?.id ?? '')
  }, [warehouses, warehouseId])
  /* eslint-enable react-hooks/set-state-in-effect */

  const setLine = (key: number, patch: Partial<Line>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)))
  const pickProduct = (key: number, p: Product | null) =>
    setLine(key, { productId: p?.id ?? null, unitPrice: p ? (isSales ? p.salePrice : p.costPrice) : 0, taxRate: p?.taxRate ?? 0 })

  const subTotal = lines.reduce((s, l) => s + Math.round(l.quantity * l.unitPrice * 100) / 100, 0)
  const tax = lines.reduce((s, l) => s + Math.round(l.quantity * l.unitPrice * l.taxRate) / 100, 0)
  const total = subTotal + tax - (isSales ? discount : 0)

  const save = async () => {
    setError(null)
    const valid = lines.filter((l) => l.productId && l.quantity > 0)
    if (!partyId) return setError(`Select a ${isSales ? 'customer' : 'supplier'}.`)
    if (!warehouseId) return setError('Select a warehouse.')
    if (valid.length === 0) return setError('Add at least one product line.')
    const body = {
      [isSales ? 'customerId' : 'supplierId']: partyId,
      warehouseId, orderDate, notes: notes || null,
      ...(isSales ? { discount } : { expectedDate: expectedDate || null, supplierInvoiceNumber: supplierInvoiceNumber || null }),
      lines: valid.map(({ productId, quantity, unitPrice, taxRate }) => ({ productId, quantity, unitPrice, taxRate })),
    }
    try {
      if (id) { await mutate(`${base}/${id}`, 'PUT', body); navigate(`${base}/${id}`) }
      else { const r = await mutate<{ id: number }>(base, 'POST', body); navigate(`${base}/${r.id}`) }
    } catch (e) { setError(errorMessage(e)) }
  }

  if (id && isLoading) return <Loading />
  const productById = (pid: number | null) => products.find((p) => p.id === pid) ?? null
  const title = `${id ? 'Edit' : 'New'} ${isSales ? 'quotation / sales order' : 'purchase order'}`

  return (
    <>
      <PageHeader title={title} subtitle={existing ? existing.orderNumber : isSales ? 'Saved as a quotation — confirm it to issue stock and raise the invoice' : 'Saved as a draft purchase order'}
        actions={<>
          <Button onClick={() => navigate(-1)}>Cancel</Button>
          <Button variant="contained" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </>} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Card sx={{ mb: 2 }}><CardContent>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(4, 1fr)' }, gap: 2 }}>
          <Autocomplete
            sx={{ gridColumn: { md: 'span 2' } }}
            options={parties}
            getOptionLabel={(o) => `${o.name}${o.extra ? ` (${o.extra})` : ''}`}
            value={parties.find((p) => p.id === partyId) ?? null}
            onChange={(_, v) => setPartyId(v?.id ?? null)}
            renderInput={(p) => <TextField {...p} label={isSales ? 'Customer' : 'Supplier'} required />}
          />
          <TextField select label="Warehouse" value={warehouseId} onChange={(e) => setWarehouseId(Number(e.target.value))} required>
            {warehouses.filter((w) => w.isActive).map((w) => <MenuItem key={w.id} value={w.id}>{w.name}</MenuItem>)}
          </TextField>
          <TextField label="Order date" type="date" value={orderDate} onChange={(e) => setOrderDate(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          {!isSales && <TextField label="Expected delivery" type="date" value={expectedDate} onChange={(e) => setExpectedDate(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />}
          {!isSales && <TextField label="Supplier invoice no." value={supplierInvoiceNumber} onChange={(e) => setSupplierInvoiceNumber(e.target.value)} />}
          <TextField label="Notes" value={notes} onChange={(e) => setNotes(e.target.value)} sx={{ gridColumn: { md: isSales ? 'span 4' : 'span 2' } }} />
        </Box>
      </CardContent></Card>

      <Card><CardContent>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>Line items</Typography>
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small" sx={{ minWidth: 760 }}>
            <TableHead>
              <TableRow>
                <TableCell sx={{ width: '40%' }}>Product</TableCell><TableCell align="right">Qty</TableCell>
                <TableCell align="right">{isSales ? 'Unit price' : 'Unit cost'}</TableCell><TableCell align="right">Tax %</TableCell>
                <TableCell align="right">Line total</TableCell><TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {lines.map((l) => {
                const p = productById(l.productId)
                const net = l.quantity * l.unitPrice
                const short = isSales && p && l.quantity > p.quantityOnHand
                return (
                  <TableRow key={l.key}>
                    <TableCell>
                      <Autocomplete
                        options={products}
                        getOptionLabel={(o) => `${o.name} · ${o.sku}`}
                        value={p}
                        onChange={(_, v) => pickProduct(l.key, v)}
                        renderOption={(props, o) => (
                          <li {...props} key={o.id}>
                            <Box><Typography variant="body2">{o.name}</Typography>
                              <Typography variant="caption" color="text.secondary">{o.sku} · on hand {num(o.quantityOnHand)} {o.unit}</Typography></Box>
                          </li>
                        )}
                        renderInput={(params) => <TextField {...params} placeholder="Select product" helperText={short ? `Only ${num(p!.quantityOnHand)} in stock` : undefined} error={!!short} />}
                      />
                    </TableCell>
                    <TableCell align="right"><TextField type="number" value={l.quantity} onChange={(e) => setLine(l.key, { quantity: Number(e.target.value) })} sx={{ width: 90 }} slotProps={{ htmlInput: { min: 0, step: 'any' } }} /></TableCell>
                    <TableCell align="right"><TextField type="number" value={l.unitPrice} onChange={(e) => setLine(l.key, { unitPrice: Number(e.target.value) })} sx={{ width: 130 }} slotProps={{ htmlInput: { min: 0, step: 'any' } }} /></TableCell>
                    <TableCell align="right"><TextField type="number" value={l.taxRate} onChange={(e) => setLine(l.key, { taxRate: Number(e.target.value) })} sx={{ width: 80 }} slotProps={{ htmlInput: { min: 0, max: 100, step: 'any' } }} /></TableCell>
                    <TableCell align="right">{money2(net + (net * l.taxRate) / 100)}</TableCell>
                    <TableCell><IconButton aria-label="Remove line" onClick={() => setLines((ls) => (ls.length > 1 ? ls.filter((x) => x.key !== l.key) : [newLine()]))}><DeleteIcon fontSize="small" /></IconButton></TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </Box>
        <Button startIcon={<AddIcon />} onClick={() => setLines((ls) => [...ls, newLine()])} sx={{ mt: 1 }}>Add line</Button>

        <Box sx={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'flex-end', flexDirection: 'column', gap: 1 }}>
          {isSales && <TextField label="Discount" type="number" value={discount} onChange={(e) => setDiscount(Number(e.target.value))} sx={{ width: 180, mt: 2 }} slotProps={{ htmlInput: { min: 0 } }} />}
          <Box sx={{ width: 320 }}>
            <Totals rows={[
              { label: 'Subtotal', value: subTotal },
              { label: 'Tax', value: tax },
              ...(isSales && discount ? [{ label: 'Discount', value: -discount }] : []),
              { label: 'Total', value: total, bold: true },
            ]} />
          </Box>
        </Box>
      </CardContent></Card>
    </>
  )
}
