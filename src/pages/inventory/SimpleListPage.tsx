import { useState } from 'react'
import { Box, Button, Card, IconButton } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { DataGrid, type GridColDef, type GridValidRowModel } from '@mui/x-data-grid'
import { ErrorAlert, PageHeader, useConfirm } from '../../components/common'
import { FormDialog, type Field } from '../../components/FormDialog'
import { useGet, useMutate } from '../../app/api'
import { useHasRole } from '../../app/store'

/** CRUD page for small, non-paged master lists (categories, warehouses). */
export function SimpleListPage<T extends GridValidRowModel & { id: number; name: string }>({
  title, subtitle, url, columns, fields, editRoles, deleteRoles, entity,
}: {
  title: string; subtitle: string; url: string; columns: GridColDef<T>[]; fields: Field[]; editRoles: string[]; deleteRoles: string[]; entity: string
}) {
  const { data = [], isFetching, error } = useGet<T[]>(url)
  const [editing, setEditing] = useState<T | null | undefined>(undefined)
  const [actionError, setActionError] = useState<unknown>(null)
  const [mutate] = useMutate()
  const [confirm, confirmDialog] = useConfirm()
  const canEdit = useHasRole(...editRoles)
  const canDelete = useHasRole(...deleteRoles)

  const remove = async (row: T) => {
    if (!(await confirm({ title: `Delete ${entity}?`, message: `Delete ${row.name}?`, confirmText: 'Delete' }))) return
    try { setActionError(null); await mutate(`${url}/${row.id}`, 'DELETE') } catch (e) { setActionError(e) }
  }

  const all: GridColDef<T>[] = [
    ...columns,
    {
      field: 'actions', headerName: '', width: 100, sortable: false,
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
      <PageHeader title={title} subtitle={subtitle}
        actions={canEdit && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing(null)}>New {entity}</Button>} />
      <ErrorAlert error={error ?? actionError} />
      <Card><Box sx={{ height: 520 }}>
        <DataGrid rows={data} columns={all} loading={isFetching} disableRowSelectionOnClick sx={{ border: 0 }} pageSizeOptions={[25, 50]} />
      </Box></Card>
      <FormDialog open={editing !== undefined} title={editing ? `Edit ${entity}` : `New ${entity}`} fields={fields}
        initial={editing ?? undefined} onClose={() => setEditing(undefined)}
        onSubmit={(v) => editing ? mutate(`${url}/${editing.id}`, 'PUT', v) : mutate(url, 'POST', v)} />
      {confirmDialog}
    </>
  )
}
