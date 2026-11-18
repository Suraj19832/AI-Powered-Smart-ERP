import { useState } from 'react'
import { Box, Button, Card, CardContent, MenuItem, Stack, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material'
import DownloadIcon from '@mui/icons-material/DownloadOutlined'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import dayjs from 'dayjs'
import { useGet } from '../app/api'
import { ErrorAlert, Loading, PageHeader, SectionCard, StatusChip } from '../components/common'
import { ChartTooltip, useChart } from '../components/charts'
import { date, downloadCsv, money, money2, moneyCompact, num } from '../utils/format'
import type { InventoryRow, ProfitLoss, Receivables, Report } from '../types'

const PRESETS: Record<string, () => [string, string]> = {
  'This month': () => [dayjs().startOf('month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')],
  'Last month': () => [dayjs().subtract(1, 'month').startOf('month').format('YYYY-MM-DD'), dayjs().subtract(1, 'month').endOf('month').format('YYYY-MM-DD')],
  'Last 3 months': () => [dayjs().subtract(3, 'month').startOf('month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')],
  'Year to date': () => [dayjs().startOf('year').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')],
  'Last 12 months': () => [dayjs().subtract(11, 'month').startOf('month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')],
}

export default function ReportsPage() {
  const [tab, setTab] = useState(0)
  const [preset, setPreset] = useState('Last 12 months')
  const [[from, to], setRange] = useState(PRESETS['Last 12 months']())

  const rangeControls = (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2 }}>
      <TextField select label="Period" value={preset} onChange={(e) => { setPreset(e.target.value); if (PRESETS[e.target.value]) setRange(PRESETS[e.target.value]()) }} sx={{ minWidth: 170 }}>
        {[...Object.keys(PRESETS), 'Custom'].map((p) => <MenuItem key={p} value={p}>{p}</MenuItem>)}
      </TextField>
      <TextField label="From" type="date" value={from} onChange={(e) => { setPreset('Custom'); setRange([e.target.value, to]) }} slotProps={{ inputLabel: { shrink: true } }} />
      <TextField label="To" type="date" value={to} onChange={(e) => { setPreset('Custom'); setRange([from, e.target.value]) }} slotProps={{ inputLabel: { shrink: true } }} />
    </Stack>
  )

  return (
    <>
      <PageHeader title="Reports" subtitle="Sales, purchases, profitability, inventory and receivables" />
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }} variant="scrollable" allowScrollButtonsMobile>
        <Tab label="Sales" /><Tab label="Purchases" /><Tab label="Profit & loss" /><Tab label="Inventory" /><Tab label="Receivables" />
      </Tabs>
      {tab <= 2 && rangeControls}
      {tab === 0 && <GroupedReport url="/reports/sales" from={from} to={to} groupOptions={['month', 'day', 'customer', 'product', 'category']} />}
      {tab === 1 && <GroupedReport url="/reports/purchases" from={from} to={to} groupOptions={['month', 'day', 'supplier', 'product', 'category']} />}
      {tab === 2 && <ProfitLossReport from={from} to={to} />}
      {tab === 3 && <InventoryReport />}
      {tab === 4 && <ReceivablesReport />}
    </>
  )
}

function GroupedReport({ url, from, to, groupOptions }: { url: string; from: string; to: string; groupOptions: string[] }) {
  const [groupBy, setGroupBy] = useState('month')
  const { data, isFetching, error } = useGet<Report>(url, { from, to, groupBy })
  const c = useChart()
  const axis = { stroke: c.axis, tick: { fill: c.muted, fontSize: 12 }, tickLine: false }
  const timeSeries = groupBy === 'month' || groupBy === 'day'
  const chartRows = timeSeries ? data?.rows ?? [] : (data?.rows ?? []).slice(0, 12)

  return (
    <>
      <ErrorAlert error={error} />
      <Stack direction="row" spacing={1.5} sx={{ mb: 2, alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <TextField select label="Group by" value={groupBy} onChange={(e) => setGroupBy(e.target.value)} sx={{ minWidth: 160 }}>
          {groupOptions.map((g) => <MenuItem key={g} value={g}>{g[0].toUpperCase() + g.slice(1)}</MenuItem>)}
        </TextField>
        {data && <Typography variant="h6" sx={{ ml: 'auto' }}>Total: {money(data.totalAmount)}</Typography>}
        <Button startIcon={<DownloadIcon />} disabled={!data?.rows.length}
          onClick={() => data && downloadCsv(`${data.title.replace(/\s/g, '-').toLowerCase()}-${groupBy}.csv`, data.rows.map((r) => ({ [groupBy]: r.label, documents: r.documents, quantity: r.quantity, amount: r.amount })))}>
          Export CSV</Button>
      </Stack>
      {isFetching && !data ? <Loading /> : data && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '3fr 2fr' }, gap: 2 }}>
          <SectionCard title={timeSeries ? `${data.title} over time` : `Top ${chartRows.length} by amount`}>
            <ResponsiveContainer width="100%" height={timeSeries ? 320 : Math.max(240, chartRows.length * 30)}>
              {timeSeries ? (
                <BarChart data={chartRows} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
                  <CartesianGrid vertical={false} stroke={c.grid} />
                  <XAxis dataKey="label" {...axis} minTickGap={12} />
                  <YAxis tickFormatter={(v) => moneyCompact(v)} {...axis} axisLine={false} width={70} />
                  <Tooltip content={<ChartTooltip format={money} />} cursor={{ fill: c.grid, opacity: 0.4 }} />
                  <Bar dataKey="amount" name="Amount" fill={c.series[0]} radius={[4, 4, 0, 0]} maxBarSize={36} />
                </BarChart>
              ) : (
                <BarChart data={chartRows} layout="vertical" margin={{ top: 0, right: 16, left: 0, bottom: 0 }} barCategoryGap={4}>
                  <CartesianGrid horizontal={false} stroke={c.grid} />
                  <XAxis type="number" tickFormatter={(v) => moneyCompact(v)} {...axis} />
                  <YAxis type="category" dataKey="label" width={170} {...axis} axisLine={false} tickFormatter={(v: string) => (v.length > 24 ? v.slice(0, 23) + '…' : v)} />
                  <Tooltip content={<ChartTooltip format={money} />} cursor={{ fill: c.grid, opacity: 0.4 }} />
                  <Bar dataKey="amount" name="Amount" fill={c.series[0]} radius={[0, 4, 4, 0]} maxBarSize={20} />
                </BarChart>
              )}
            </ResponsiveContainer>
          </SectionCard>
          <SectionCard title="Table">
            <Box sx={{ maxHeight: 420, overflow: 'auto' }}>
              <Table size="small" stickyHeader>
                <TableHead><TableRow><TableCell>{groupBy}</TableCell><TableCell align="right">Docs</TableCell><TableCell align="right">Qty</TableCell><TableCell align="right">Amount</TableCell></TableRow></TableHead>
                <TableBody>
                  {data.rows.map((r) => (
                    <TableRow key={r.key}><TableCell>{r.label}</TableCell><TableCell align="right">{r.documents}</TableCell><TableCell align="right">{num(r.quantity)}</TableCell><TableCell align="right">{money(r.amount)}</TableCell></TableRow>
                  ))}
                  {data.rows.length === 0 && <TableRow><TableCell colSpan={4}>No data for this period.</TableCell></TableRow>}
                </TableBody>
              </Table>
            </Box>
          </SectionCard>
        </Box>
      )}
    </>
  )
}

function Row({ label, value, bold, indent }: { label: string; value: number; bold?: boolean; indent?: boolean }) {
  return (
    <TableRow>
      <TableCell sx={{ pl: indent ? 4 : 2, fontWeight: bold ? 700 : 400 }}>{label}</TableCell>
      <TableCell align="right" sx={{ fontWeight: bold ? 700 : 400, color: value < 0 ? 'error.main' : undefined }}>{money2(value)}</TableCell>
    </TableRow>
  )
}

function ProfitLossReport({ from, to }: { from: string; to: string }) {
  const { data, error } = useGet<ProfitLoss>('/reports/profit-loss', { from, to })
  if (!data) return error ? <ErrorAlert error={error} /> : <Loading />
  return (
    <Card sx={{ maxWidth: 720 }}><CardContent>
      <Typography variant="h6">Profit & loss statement</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{date(data.from)} – {date(dayjs(data.to).subtract(1, 'day').toISOString())}</Typography>
      <Table size="small">
        <TableBody>
          <Row label="Net sales" value={data.netSales} />
          <Row label="Cost of goods sold" value={-data.costOfGoodsSold} />
          <Row label="Gross profit" value={data.grossProfit} bold />
          {data.expenses.map((e) => <Row key={e.name} label={e.name.replace(/([a-z])([A-Z])/g, '$1 $2')} value={-e.amount} indent />)}
          <Row label="Total operating expenses" value={-data.totalExpenses} />
          <Row label={`Net profit (${data.netMarginPercent}% margin)`} value={data.netProfit} bold />
        </TableBody>
      </Table>
    </CardContent></Card>
  )
}

function InventoryReport() {
  const { data, error } = useGet<InventoryRow[]>('/reports/inventory')
  if (!data) return error ? <ErrorAlert error={error} /> : <Loading />
  const total = data.reduce((s, r) => s + r.stockValue, 0)
  return (
    <SectionCard title={`Inventory valuation — ${money(total)} at cost`}
      action={<Button startIcon={<DownloadIcon />} onClick={() => downloadCsv('inventory-valuation.csv', data as unknown as Record<string, unknown>[])}>Export CSV</Button>}>
      <Box sx={{ overflowX: 'auto' }}>
        <Table size="small">
          <TableHead><TableRow><TableCell>SKU</TableCell><TableCell>Product</TableCell><TableCell>Category</TableCell><TableCell align="right">On hand</TableCell><TableCell align="right">Cost</TableCell><TableCell align="right">Stock value</TableCell><TableCell align="right">Retail value</TableCell><TableCell>Status</TableCell></TableRow></TableHead>
          <TableBody>
            {data.map((r) => (
              <TableRow key={r.productId}>
                <TableCell>{r.sku}</TableCell><TableCell>{r.name}</TableCell><TableCell>{r.category}</TableCell>
                <TableCell align="right">{num(r.quantityOnHand)} {r.unit}</TableCell><TableCell align="right">{money2(r.costPrice)}</TableCell>
                <TableCell align="right">{money(r.stockValue)}</TableCell><TableCell align="right">{money(r.retailValue)}</TableCell>
                <TableCell>{r.isLowStock ? <StatusChip status="High" /> : <StatusChip status="Active" />}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
    </SectionCard>
  )
}

function ReceivablesReport() {
  const { data, error } = useGet<Receivables>('/reports/receivables')
  const c = useChart()
  if (!data) return error ? <ErrorAlert error={error} /> : <Loading />
  const axis = { stroke: c.axis, tick: { fill: c.muted, fontSize: 12 }, tickLine: false }
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 2fr' }, gap: 2 }}>
      <SectionCard title={`Outstanding ${money(data.totalOutstanding)}`}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{data.openInvoices} open invoices · {money(data.totalOverdue)} overdue</Typography>
        <ResponsiveContainer width="100%" height={260}>
          <BarChart data={data.aging} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid vertical={false} stroke={c.grid} />
            <XAxis dataKey="bucket" {...axis} />
            <YAxis tickFormatter={(v) => moneyCompact(v)} {...axis} axisLine={false} width={64} />
            <Tooltip content={<ChartTooltip format={money} />} cursor={{ fill: c.grid, opacity: 0.4 }} />
            <Bar dataKey="amount" name="Outstanding" fill={c.series[0]} radius={[4, 4, 0, 0]} maxBarSize={44} />
          </BarChart>
        </ResponsiveContainer>
      </SectionCard>
      <SectionCard title="Open invoices" action={<Button startIcon={<DownloadIcon />} onClick={() => downloadCsv('receivables.csv', data.invoices as unknown as Record<string, unknown>[])}>Export CSV</Button>}>
        <Box sx={{ maxHeight: 420, overflow: 'auto' }}>
          <Table size="small" stickyHeader>
            <TableHead><TableRow><TableCell>Invoice</TableCell><TableCell>Customer</TableCell><TableCell>Due</TableCell><TableCell align="right">Days overdue</TableCell><TableCell align="right">Balance</TableCell></TableRow></TableHead>
            <TableBody>
              {data.invoices.map((i) => (
                <TableRow key={i.invoiceId}>
                  <TableCell>{i.invoiceNumber}</TableCell><TableCell>{i.customerName}</TableCell><TableCell>{date(i.dueDate)}</TableCell>
                  <TableCell align="right" sx={{ color: i.daysOverdue > 30 ? 'error.main' : undefined, fontWeight: i.daysOverdue > 0 ? 600 : 400 }}>{i.daysOverdue}</TableCell>
                  <TableCell align="right">{money(i.balanceDue)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </SectionCard>
    </Box>
  )
}
