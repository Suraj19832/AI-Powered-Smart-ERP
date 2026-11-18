import { useEffect, useState } from 'react'
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Switch, TextField,
} from '@mui/material'
import { errorMessage } from '../app/api'

export type FieldType = 'text' | 'email' | 'number' | 'select' | 'switch' | 'date' | 'textarea' | 'password' | 'multiselect'

export interface Field {
  name: string
  label: string
  type?: FieldType
  required?: boolean
  options?: { value: string | number; label: string }[]
  /** grid columns out of 2 */
  span?: 1 | 2
  helperText?: string
  min?: number
  max?: number
  disabled?: boolean
}

type Values = Record<string, any>

interface Props {
  open: boolean
  title: string
  fields: Field[]
  initial?: Values
  submitText?: string
  onClose: () => void
  onSubmit: (values: Values) => Promise<unknown>
}

/** Generic create/edit dialog generated from a field list; shows server-side validation errors inline. */
export function FormDialog({ open, title, fields, initial, submitText = 'Save', onClose, onSubmit }: Props) {
  const [values, setValues] = useState<Values>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [touched, setTouched] = useState(false)

  useEffect(() => {
    if (!open) return
    const v: Values = {}
    for (const f of fields) {
      const iv = initial?.[f.name]
      v[f.name] = iv ?? (f.type === 'switch' ? true : f.type === 'multiselect' ? [] : f.type === 'number' ? 0 : '')
      if (f.type === 'date' && typeof iv === 'string') v[f.name] = iv.slice(0, 10)
    }
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setValues(v); setError(null); setTouched(false)
  }, [open]) // eslint-disable-line react-hooks/exhaustive-deps

  const missing = fields.filter((f) => f.required && (values[f.name] === '' || values[f.name] === null || values[f.name] === undefined))

  const submit = async () => {
    setTouched(true)
    if (missing.length) return
    setBusy(true); setError(null)
    try {
      const out: Values = {}
      for (const f of fields) {
        const v = values[f.name]
        out[f.name] = f.type === 'number' ? Number(v) : v === '' ? null : v
      }
      await onSubmit(out)
      onClose()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const set = (name: string, v: unknown) => setValues((s) => ({ ...s, [name]: v }))

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
          {fields.map((f) => {
            const span = { gridColumn: { sm: f.span === 2 || f.type === 'textarea' ? 'span 2' : 'span 1' } }
            if (f.type === 'switch')
              return (
                <FormControlLabel key={f.name} sx={span} label={f.label}
                  control={<Switch checked={!!values[f.name]} onChange={(e) => set(f.name, e.target.checked)} />} />
              )
            const err = touched && missing.includes(f)
            return (
              <TextField
                key={f.name}
                sx={span}
                label={f.label}
                required={f.required}
                disabled={f.disabled}
                type={f.type === 'number' || f.type === 'email' || f.type === 'date' || f.type === 'password' ? f.type : 'text'}
                select={f.type === 'select' || f.type === 'multiselect'}
                multiline={f.type === 'textarea'}
                minRows={f.type === 'textarea' ? 3 : undefined}
                value={values[f.name] ?? (f.type === 'multiselect' ? [] : '')}
                onChange={(e) => set(f.name, e.target.value)}
                error={err}
                helperText={err ? 'Required' : f.helperText}
                slotProps={{
                  inputLabel: f.type === 'date' ? { shrink: true } : undefined,
                  htmlInput: f.type === 'number' ? { min: f.min, max: f.max, step: 'any' } : undefined,
                  select: f.type === 'multiselect' ? { multiple: true } : undefined,
                }}
              >
                {(f.type === 'select' || f.type === 'multiselect') &&
                  [...(f.type === 'select' && !f.required ? [{ value: '', label: '— None —' }] : []), ...(f.options ?? [])].map((o) => (
                    <MenuItem key={String(o.value)} value={o.value}>{o.label}</MenuItem>
                  ))}
              </TextField>
            )
          })}
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={busy}>{busy ? 'Saving…' : submitText}</Button>
      </DialogActions>
    </Dialog>
  )
}
