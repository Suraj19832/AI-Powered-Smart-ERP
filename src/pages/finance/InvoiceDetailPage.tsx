import { useState } from 'react'
import { Link as RouterLink, useParams } from 'react-router-dom'
import { Box, Button, Card, CardContent, Divider, GlobalStyles, Link, Stack, Typography } from '@mui/material'
import PaymentsIcon from '@mui/icons-material/PaymentsOutlined'
import PrintIcon from '@mui/icons-material/PrintOutlined'
import { useGet } from '../../app/api'
import { useHasRole } from '../../app/store'
import { R } from '../../roles'
import { ErrorAlert, Loading, PageHeader, SectionCard, StatusChip } from '../../components/common'
import { LinesTable, PaymentDialog, PaymentsTable, Totals } from '../../components/DocumentParts'
import { useSettings } from '../../components/AppContext'
import { date } from '../../utils/format'
import type { Invoice } from '../../types'

export default function InvoiceDetailPage() {
  const { id } = useParams()
  const { data, isLoading, error } = useGet<Invoice>(`/invoices/${id}`)
  const [paying, setPaying] = useState(false)
  const canPay = useHasRole(...R.finance)
  const settings = useSettings()

  if (isLoading) return <Loading />
  if (!data) return <ErrorAlert error={error} />
  const h = data.header
  const payable = h.status === 'Unpaid' || h.status === 'PartiallyPaid'

  return (
    <>
      {/* print only the invoice document */}
      <GlobalStyles styles={{ '@media print': { 'header, nav, .no-print': { display: 'none !important' }, main: { margin: '0 !important', padding: '0 !important' } } }} />
      <Box className="no-print">
        <PageHeader
          title={`Invoice ${h.invoiceNumber}`}
          subtitle={<>{h.customerName} · <StatusChip status={h.status} overdue={h.isOverdue} /></>}
          actions={<>
            {canPay && payable && <Button variant="contained" startIcon={<PaymentsIcon />} onClick={() => setPaying(true)}>Record payment</Button>}
            <Button variant="outlined" startIcon={<PrintIcon />} onClick={() => window.print()}>Print</Button>
          </>}
        />
      </Box>

      <Card sx={{ mb: 2 }}><CardContent sx={{ p: { xs: 2, md: 4 } }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', gap: 2 }}>
          <Box>
            <Typography variant="h6">{settings.companyName}</Typography>
            <Typography variant="body2" color="text.secondary">Tax invoice</Typography>
          </Box>
          <Box sx={{ textAlign: { sm: 'right' } }}>
            <Typography variant="h6">{h.invoiceNumber}</Typography>
            <Typography variant="body2">Invoice date: {date(h.invoiceDate)}</Typography>
            <Typography variant="body2">Due date: {date(h.dueDate)}</Typography>
            <Typography variant="body2">Order: <Link component={RouterLink} to={`/sales-orders/${h.salesOrderId}`}>{h.orderNumber}</Link></Typography>
          </Box>
        </Stack>
        <Divider sx={{ my: 2 }} />
        <Typography variant="caption" color="text.secondary">BILL TO</Typography>
        <Typography sx={{ fontWeight: 600 }}><Link component={RouterLink} to={`/customers/${h.customerId}`} color="inherit">{h.customerName}</Link></Typography>
        <Typography variant="body2" color="text.secondary">{data.customerAddress}</Typography>
        <Typography variant="body2" color="text.secondary">{data.customerEmail}</Typography>
        <Box sx={{ mt: 3 }}><LinesTable lines={data.lines} /></Box>
        <Totals rows={[
          { label: 'Subtotal', value: data.subTotal }, { label: 'Tax', value: data.taxAmount },
          ...(data.discount ? [{ label: 'Discount', value: -data.discount }] : []),
          { label: 'Total', value: h.totalAmount, bold: true }, { label: 'Paid', value: h.amountPaid }, { label: 'Balance due', value: h.balanceDue, bold: true },
        ]} />
      </CardContent></Card>

      <SectionCard title="Payments received"><PaymentsTable payments={data.payments} /></SectionCard>
      <PaymentDialog open={paying} onClose={() => setPaying(false)} balance={h.balanceDue} invoiceId={h.id} />
    </>
  )
}
