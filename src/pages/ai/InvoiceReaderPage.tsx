import { useEffect, useState, type DragEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert, Autocomplete, Box, Button, Card, CardContent, Chip, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material'
import UploadFileIcon from '@mui/icons-material/UploadFileOutlined'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import AddShoppingCartIcon from '@mui/icons-material/AddShoppingCart'
import { errorMessage, useGet, useReadInvoiceMutation } from '../../app/api'
import { InfoRow, Loading, PageHeader, SectionCard } from '../../components/common'
import { useSettings } from '../../components/AppContext'
import { money2, num } from '../../utils/format'
import type { ExtractedInvoice, Lookup, OrderPrefill } from '../../types'

const ACCEPT = ['application/pdf', 'image/png', 'image/jpeg', 'image/webp', 'image/gif']

export default function InvoiceReaderPage() {
  const settings = useSettings()
  const navigate = useNavigate()
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [result, setResult] = useState<ExtractedInvoice | null>(null)
  const [localError, setLocalError] = useState<string | null>(null)
  const [read, { isLoading, error }] = useReadInvoiceMutation()
  const { data: products = [] } = useGet<Lookup[]>('/products/lookup')
  const { data: suppliers = [] } = useGet<Lookup[]>('/suppliers/lookup')

  useEffect(() => {
    if (!file) return
    const url = URL.createObjectURL(file)
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  const choose = (f?: File | null) => {
    setLocalError(null); setResult(null)
    if (!f) return
    if (!ACCEPT.includes(f.type)) return setLocalError('Please choose a PDF, PNG, JPG, WEBP or GIF file.')
    if (f.size > 10 * 1024 * 1024) return setLocalError('File must be 10 MB or smaller.')
    setFile(f)
  }

  const extract = async () => {
    if (!file) return
    try { setResult(await read(file).unwrap()) } catch { /* shown via error */ }
  }

  const setLineProduct = (i: number, p: Lookup | null) =>
    setResult((r) => r && { ...r, items: r.items.map((l, j) => (j === i ? { ...l, matchedProductId: p?.id ?? null, matchedProductName: p?.name ?? null } : l)) })

  const createPo = () => {
    if (!result) return
    const prefill: OrderPrefill = {
      partyId: result.matchedSupplierId,
      supplierInvoiceNumber: result.invoiceNumber,
      notes: `Imported by AI invoice reader${result.invoiceDate ? ` — supplier invoice dated ${result.invoiceDate}` : ''}`,
      lines: result.items.filter((l) => l.matchedProductId).map((l) => ({
        productId: l.matchedProductId!, quantity: l.quantity, unitPrice: l.unitPrice, taxRate: l.taxRate,
      })),
    }
    navigate('/purchase-orders/new', { state: { prefill } })
  }

  const onDrop = (e: DragEvent) => { e.preventDefault(); choose(e.dataTransfer.files?.[0]) }

  return (
    <>
      <PageHeader title="AI Invoice Reader" subtitle="Upload a supplier invoice — AI extracts the data and pre-fills a purchase order" />
      {!settings.aiEnabled && (
        <Alert severity="warning" sx={{ mb: 2 }}>The invoice reader uses a vision-capable OpenAI model. Configure <code>OpenAI:ApiKey</code> on the API to enable it.</Alert>
      )}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, gap: 2, alignItems: 'start' }}>
        <Card>
          <CardContent>
            <Box
              component="label"
              onDragOver={(e) => e.preventDefault()}
              onDrop={onDrop}
              sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1, p: 4, border: 2, borderStyle: 'dashed', borderColor: 'divider', borderRadius: 3, cursor: 'pointer', textAlign: 'center', '&:hover': { borderColor: 'primary.main' } }}
            >
              <UploadFileIcon color="primary" sx={{ fontSize: 40 }} />
              <Typography sx={{ fontWeight: 600 }}>{file ? file.name : 'Drop an invoice here or click to browse'}</Typography>
              <Typography variant="caption" color="text.secondary">PDF, PNG, JPG, WEBP · up to 10 MB</Typography>
              <input hidden type="file" accept={ACCEPT.join(',')} onChange={(e) => choose(e.target.files?.[0])} />
            </Box>
            {localError && <Alert severity="error" sx={{ mt: 2 }}>{localError}</Alert>}
            {error && <Alert severity="error" sx={{ mt: 2 }}>{errorMessage(error)}</Alert>}
            <Button fullWidth variant="contained" startIcon={<AutoAwesomeIcon />} sx={{ mt: 2 }} disabled={!file || isLoading} onClick={extract}>
              {isLoading ? 'Reading invoice…' : 'Extract with AI'}
            </Button>
            {preview && file && (
              <Box sx={{ mt: 2, border: 1, borderColor: 'divider', borderRadius: 2, overflow: 'hidden', height: 520 }}>
                {file.type === 'application/pdf'
                  ? <object data={preview} type="application/pdf" width="100%" height="100%"><Typography sx={{ p: 2 }}>PDF preview not available.</Typography></object>
                  : <Box component="img" src={preview} alt="Invoice preview" sx={{ width: '100%', height: '100%', objectFit: 'contain' }} />}
              </Box>
            )}
          </CardContent>
        </Card>

        <Box sx={{ display: 'grid', gap: 2 }}>
          {isLoading && <Card><Loading label="The AI is reading the document…" /></Card>}
          {!result && !isLoading && (
            <Card><CardContent><Typography color="text.secondary">Extracted invoice details will appear here. You can correct product matches before creating the purchase order.</Typography></CardContent></Card>
          )}
          {result && (
            <>
              {result.warnings.map((w) => <Alert key={w} severity="warning">{w}</Alert>)}
              <SectionCard title="Invoice header">
                <InfoRow label="Invoice number" value={result.invoiceNumber ?? '—'} />
                <InfoRow label="Invoice date" value={result.invoiceDate ?? '—'} />
                <InfoRow label="Due date" value={result.dueDate ?? '—'} />
                <InfoRow label="Supplier (on document)" value={result.supplierName ?? '—'} />
                <InfoRow label="Supplier tax no." value={result.supplierTaxNumber ?? '—'} />
                <Box sx={{ mt: 1.5 }}>
                  <Autocomplete
                    options={suppliers}
                    getOptionLabel={(o) => o.name}
                    value={suppliers.find((s) => s.id === result.matchedSupplierId) ?? null}
                    onChange={(_, v) => setResult({ ...result, matchedSupplierId: v?.id ?? null, matchedSupplierName: v?.name ?? null })}
                    renderInput={(p) => <TextField {...p} label="Matched supplier in ERP" />}
                  />
                </Box>
              </SectionCard>
              <SectionCard title={`Line items (${result.items.length})`}>
                <Box sx={{ overflowX: 'auto' }}>
                  <Table size="small" sx={{ minWidth: 640 }}>
                    <TableHead><TableRow><TableCell>Description → ERP product</TableCell><TableCell align="right">Qty</TableCell><TableCell align="right">Unit price</TableCell><TableCell align="right">Tax %</TableCell><TableCell align="right">Amount</TableCell></TableRow></TableHead>
                    <TableBody>
                      {result.items.map((l, i) => (
                        <TableRow key={i}>
                          <TableCell sx={{ minWidth: 260 }}>
                            <Typography variant="body2">{l.description}</Typography>
                            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mt: 0.5 }}>
                              <Autocomplete size="small" sx={{ flexGrow: 1 }} options={products} getOptionLabel={(o) => o.name}
                                value={products.find((p) => p.id === l.matchedProductId) ?? null} onChange={(_, v) => setLineProduct(i, v)}
                                renderInput={(p) => <TextField {...p} placeholder="Match product" error={!l.matchedProductId} />} />
                              {l.matchedProductId && l.matchScore > 0 && <Chip size="small" label={`${Math.round(l.matchScore * 100)}%`} />}
                            </Stack>
                          </TableCell>
                          <TableCell align="right">{num(l.quantity)}</TableCell><TableCell align="right">{money2(l.unitPrice)}</TableCell>
                          <TableCell align="right">{num(l.taxRate)}</TableCell><TableCell align="right">{money2(l.amount)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
                <Box sx={{ ml: 'auto', maxWidth: 280, mt: 2 }}>
                  <InfoRow label="Subtotal" value={result.subTotal !== null && result.subTotal !== undefined ? money2(result.subTotal) : '—'} />
                  <InfoRow label="Tax" value={result.taxAmount !== null && result.taxAmount !== undefined ? money2(result.taxAmount) : '—'} />
                  <InfoRow label="Total" value={<b>{result.total !== null && result.total !== undefined ? money2(result.total) : '—'}</b>} />
                </Box>
              </SectionCard>
              <Button variant="contained" size="large" startIcon={<AddShoppingCartIcon />} onClick={createPo}
                disabled={!result.matchedSupplierId || !result.items.some((l) => l.matchedProductId)}>
                Create purchase order from this invoice
              </Button>
            </>
          )}
        </Box>
      </Box>
    </>
  )
}
