import { useState } from 'react'
import { Button, IconButton, MenuItem, TextField } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import type { GridColDef } from '@mui/x-data-grid'
import { ErrorAlert, PageHeader, useConfirm } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { FormDialog } from '../../components/FormDialog'
import { PAYMENT_METHODS } from '../../components/DocumentParts'
import { useMutate } from '../../app/api'
import { date, money2, todayIso, words } from '../../utils/format'
import type { Expense } from '../../types'

const CATEGORIES = ['Rent', 'Salaries', 'Utilities', 'Marketing', 'Transport', 'Maintenance', 'OfficeSupplies', 'Other']

export default function ExpensesPage() {
  const [category, setCategory] = useState('')
  const [editing, setEditing] = useState<Expense | null | undefined>(undefined)
  const [actionError, setActionError] = useState<unknown>(null)
  const [mutate] = useMutate()
  const [confirm, dialog] = useConfirm()

  const columns: GridColDef<Expense>[] = [
    { field: 'expenseDate', headerName: 'Date', width: 120, valueFormatter: (v: string) => date(v) },
    { field: 'category', headerName: 'Category', width: 150, valueFormatter: (v: string) => words(v) },
    { field: 'description', headerName: 'Description', flex: 1, minWidth: 240 },
    { field: 'method', headerName: 'Method', width: 130, valueFormatter: (v: string) => words(v) },
    { field: 'reference', headerName: 'Reference', width: 140 },
    { field: 'amount', headerName: 'Amount', width: 140, type: 'number', valueFormatter: (v: number) => money2(v) },
    {
      field: 'actions', headerName: '', width: 96, sortable: false,
      renderCell: (p) => (<>
        <IconButton size="small" aria-label="Edit" onClick={() => setEditing(p.row)}><EditIcon fontSize="small" /></IconButton>
        <IconButton size="small" aria-label="Delete" onClick={async () => {
          if (!(await confirm({ title: 'Delete expense?', message: p.row.description, confirmText: 'Delete' }))) return
          try { setActionError(null); await mutate(`/expenses/${p.row.id}`, 'DELETE') } catch (e) { setActionError(e) }
        }}><DeleteIcon fontSize="small" /></IconButton>
      </>),
    },
  ]

  return (
    <>
      <PageHeader title="Expenses" subtitle="Operating expenses feed the profit & loss report"
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing(null)}>New expense</Button>} />
      <ErrorAlert error={actionError} />
      <ServerGrid<Expense>
        url="/expenses" columns={columns} params={{ category: category || undefined }}
        searchPlaceholder="Search description or reference…"
        filters={
          <TextField select label="Category" value={category} onChange={(e) => setCategory(e.target.value)} sx={{ minWidth: 180 }}>
            <MenuItem value="">All categories</MenuItem>
            {CATEGORIES.map((c) => <MenuItem key={c} value={c}>{words(c)}</MenuItem>)}
          </TextField>
        }
      />
      <FormDialog
        open={editing !== undefined}
        title={editing ? 'Edit expense' : 'New expense'}
        initial={editing ?? { expenseDate: todayIso(), category: 'Other', method: 'BankTransfer' }}
        fields={[
          { name: 'expenseDate', label: 'Date', type: 'date', required: true },
          { name: 'category', label: 'Category', type: 'select', required: true, options: CATEGORIES.map((c) => ({ value: c, label: words(c) })) },
          { name: 'description', label: 'Description', required: true, span: 2 },
          { name: 'amount', label: 'Amount', type: 'number', required: true, min: 0.01 },
          { name: 'method', label: 'Payment method', type: 'select', required: true, options: PAYMENT_METHODS },
          { name: 'reference', label: 'Reference', span: 2 },
        ]}
        onClose={() => setEditing(undefined)}
        onSubmit={(v) => editing ? mutate(`/expenses/${editing.id}`, 'PUT', v) : mutate('/expenses', 'POST', v)}
      />
      {dialog}
    </>
  )
}
