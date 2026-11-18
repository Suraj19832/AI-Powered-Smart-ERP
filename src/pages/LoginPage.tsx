import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Alert, Box, Button, Card, CardContent, Chip, Stack, TextField, Typography } from '@mui/material'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import { errorMessage, useLoginMutation } from '../app/api'
import { credentialsReceived } from '../app/authSlice'
import { useAppDispatch, useAppSelector } from '../app/store'

const DEMO = [
  { user: 'admin', pwd: 'Admin@123', role: 'Admin' },
  { user: 'manager', pwd: 'Manager@123', role: 'Manager' },
  { user: 'sales', pwd: 'Sales@123', role: 'Sales' },
  { user: 'purchase', pwd: 'Purchase@123', role: 'Purchase' },
  { user: 'accounts', pwd: 'Accounts@123', role: 'Accountant' },
]

export default function LoginPage() {
  const [userName, setUserName] = useState('admin')
  const [password, setPassword] = useState('Admin@123')
  const [login, { isLoading, error }] = useLoginMutation()
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const location = useLocation()
  const user = useAppSelector((s) => s.auth.user)
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  if (user) return <Navigate to={from} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const res = await login({ userName, password }).unwrap()
      dispatch(credentialsReceived(res))
      navigate(from, { replace: true })
    } catch { /* shown below */ }
  }

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: 2, background: (t) => t.palette.mode === 'light' ? 'linear-gradient(135deg,#eef2ff 0%,#f8fafc 60%)' : 'linear-gradient(135deg,#111827 0%,#0b0b0c 60%)' }}>
      <Card sx={{ width: '100%', maxWidth: 420 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={1} sx={{ alignItems: 'center', mb: 3 }}>
            <Box sx={{ width: 48, height: 48, borderRadius: 3, display: 'grid', placeItems: 'center', bgcolor: 'primary.main', color: 'primary.contrastText' }}>
              <AutoAwesomeIcon />
            </Box>
            <Typography variant="h5">SmartERP</Typography>
            <Typography color="text.secondary" variant="body2">AI-powered business management</Typography>
          </Stack>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
          <Stack component="form" spacing={2} onSubmit={submit}>
            <TextField label="Username or email" value={userName} onChange={(e) => setUserName(e.target.value)} autoFocus required size="medium" />
            <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required size="medium" />
            <Button type="submit" variant="contained" size="large" disabled={isLoading}>{isLoading ? 'Signing in…' : 'Sign in'}</Button>
          </Stack>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 3, mb: 1 }}>Demo accounts (click to fill):</Typography>
          <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1 }}>
            {DEMO.map((d) => (
              <Chip key={d.user} label={d.role} size="small" variant={userName === d.user ? 'filled' : 'outlined'} onClick={() => { setUserName(d.user); setPassword(d.pwd) }} />
            ))}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  )
}
