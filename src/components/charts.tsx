import { Box, Paper, Stack, Typography, useTheme } from '@mui/material'
import type { TooltipContentProps } from 'recharts'
import { chartTokens } from '../theme'

export const useChart = () => chartTokens(useTheme().palette.mode)

/** Shared tooltip: values in text ink, series identity via a small colour swatch. */
export function ChartTooltip({ active, payload, label, format, labelFormat }: Partial<TooltipContentProps<number, string>> & {
  format: (v: number) => string; labelFormat?: (l: string) => string
}) {
  if (!active || !payload?.length) return null
  return (
    <Paper sx={{ px: 1.5, py: 1, border: 1, borderColor: 'divider', boxShadow: 3 }}>
      <Typography variant="caption" color="text.secondary">{labelFormat ? labelFormat(String(label)) : label}</Typography>
      {payload.filter((p) => p.value !== null && p.value !== undefined).map((p) => (
        <Stack key={String(p.dataKey)} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <Box sx={{ width: 10, height: 10, borderRadius: 0.5, bgcolor: p.color }} />
          <Typography variant="body2" color="text.secondary">{p.name}</Typography>
          <Typography variant="body2" sx={{ fontWeight: 600, ml: 'auto', pl: 2, fontVariantNumeric: 'tabular-nums' }}>{format(Number(p.value))}</Typography>
        </Stack>
      ))}
    </Paper>
  )
}

export function Legend({ items }: { items: { label: string; color: string; dashed?: boolean }[] }) {
  return (
    <Stack direction="row" spacing={2} sx={{ flexWrap: 'wrap', mb: 1 }}>
      {items.map((i) => (
        <Stack key={i.label} direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
          <Box sx={{ width: 14, height: i.dashed ? 0 : 3, borderTop: i.dashed ? `2px dashed ${i.color}` : 'none', bgcolor: i.dashed ? 'transparent' : i.color, borderRadius: 2 }} />
          <Typography variant="caption" color="text.secondary">{i.label}</Typography>
        </Stack>
      ))}
    </Stack>
  )
}
