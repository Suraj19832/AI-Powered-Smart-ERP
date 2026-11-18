import { useState } from 'react'
import { Button, Chip, IconButton, Stack } from '@mui/material'
import AddIcon from '@mui/icons-material/PersonAddAlt'
import EditIcon from '@mui/icons-material/EditOutlined'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader, StatusChip } from '../../components/common'
import { ServerGrid } from '../../components/ServerGrid'
import { FormDialog, type Field } from '../../components/FormDialog'
import { useGet, useMutate } from '../../app/api'
import { dateTime } from '../../utils/format'
import type { Role, UserRow } from '../../types'

export default function UsersPage() {
  const { data: roles = [] } = useGet<Role[]>('/users/roles')
  const [editing, setEditing] = useState<UserRow | null | undefined>(undefined)
  const [mutate] = useMutate()
  const roleOptions = roles.map((r) => ({ value: r.name, label: `${r.name} — ${r.description ?? ''}` }))

  const createFields: Field[] = [
    { name: 'userName', label: 'Username', required: true },
    { name: 'fullName', label: 'Full name', required: true },
    { name: 'email', label: 'Email', type: 'email', required: true, span: 2 },
    { name: 'password', label: 'Password', type: 'password', required: true, helperText: 'At least 8 characters', span: 2 },
    { name: 'roles', label: 'Roles', type: 'multiselect', options: roleOptions, span: 2 },
  ]
  const editFields: Field[] = [
    { name: 'fullName', label: 'Full name', required: true },
    { name: 'email', label: 'Email', type: 'email', required: true },
    { name: 'roles', label: 'Roles', type: 'multiselect', options: roleOptions, span: 2 },
    { name: 'newPassword', label: 'New password (optional)', type: 'password', span: 2 },
    { name: 'isActive', label: 'Active', type: 'switch' },
  ]

  const columns: GridColDef<UserRow>[] = [
    { field: 'userName', headerName: 'Username', width: 140 },
    { field: 'fullName', headerName: 'Full name', flex: 1, minWidth: 180 },
    { field: 'email', headerName: 'Email', width: 230 },
    { field: 'roles', headerName: 'Roles', width: 240, sortable: false, renderCell: (p) => <Stack direction="row" spacing={0.5}>{p.row.roles.map((r) => <Chip key={r} size="small" label={r} />)}</Stack> },
    { field: 'isActive', headerName: 'Status', width: 100, renderCell: (p) => <StatusChip status={p.value ? 'Active' : 'Inactive'} /> },
    { field: 'lastLoginAt', headerName: 'Last login', width: 170, valueFormatter: (v?: string) => dateTime(v) },
    { field: 'actions', headerName: '', width: 60, sortable: false, renderCell: (p) => <IconButton size="small" aria-label="Edit" onClick={() => setEditing(p.row)}><EditIcon fontSize="small" /></IconButton> },
  ]

  return (
    <>
      <PageHeader title="Users & roles" subtitle="Role-based access control: each role unlocks specific modules and actions"
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing(null)}>New user</Button>} />
      <ServerGrid<UserRow> url="/users" columns={columns} searchPlaceholder="Search users…" />
      <FormDialog
        open={editing !== undefined}
        title={editing ? `Edit ${editing.userName}` : 'New user'}
        fields={editing ? editFields : createFields}
        initial={editing ? { ...editing, newPassword: '' } : { roles: ['Sales'] }}
        onClose={() => setEditing(undefined)}
        onSubmit={(v) => editing ? mutate(`/users/${editing.id}`, 'PUT', { ...v, newPassword: v.newPassword || null }) : mutate('/users', 'POST', v)}
      />
    </>
  )
}
