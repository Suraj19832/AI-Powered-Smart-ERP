import { useState, type FormEvent } from 'react'
import { Alert, Button, Card, CardContent, Stack, TextField } from '@mui/material'
import { errorMessage, useMutate } from '../../app/api'
import { PageHeader } from '../../components/common'

export default function ProfilePage() {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const [mutate, { isLoading }] = useMutate()

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (next !== confirm) return setMsg({ ok: false, text: 'New passwords do not match.' })
    try {
      await mutate('/auth/change-password', 'POST', { currentPassword: current, newPassword: next })
      setMsg({ ok: true, text: 'Password changed.' }); setCurrent(''); setNext(''); setConfirm('')
    } catch (err) { setMsg({ ok: false, text: errorMessage(err) }) }
  }

  return (
    <>
      <PageHeader title="Change password" />
      <Card sx={{ maxWidth: 460 }}><CardContent>
        <Stack component="form" spacing={2} onSubmit={submit}>
          {msg && <Alert severity={msg.ok ? 'success' : 'error'}>{msg.text}</Alert>}
          <TextField type="password" label="Current password" value={current} onChange={(e) => setCurrent(e.target.value)} required />
          <TextField type="password" label="New password" value={next} onChange={(e) => setNext(e.target.value)} required helperText="At least 8 characters" />
          <TextField type="password" label="Confirm new password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
          <Button type="submit" variant="contained" disabled={isLoading}>Update password</Button>
        </Stack>
      </CardContent></Card>
    </>
  )
}
