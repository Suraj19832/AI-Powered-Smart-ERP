import dayjs from 'dayjs'

let locale = 'en-IN'
let currency = 'INR'
let moneyFmt = new Intl.NumberFormat(locale, { style: 'currency', currency, maximumFractionDigits: 0 })
let money2Fmt = new Intl.NumberFormat(locale, { style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2 })
let compactFmt = new Intl.NumberFormat(locale, { style: 'currency', currency, notation: 'compact', maximumFractionDigits: 1 })
let numFmt = new Intl.NumberFormat(locale, { maximumFractionDigits: 2 })

/** Called once the API settings are loaded so every formatter follows the company's currency/locale. */
export function configureFormat(l: string, c: string) {
  if (l === locale && c === currency) return
  locale = l
  currency = c
  moneyFmt = new Intl.NumberFormat(locale, { style: 'currency', currency, maximumFractionDigits: 0 })
  money2Fmt = new Intl.NumberFormat(locale, { style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2 })
  compactFmt = new Intl.NumberFormat(locale, { style: 'currency', currency, notation: 'compact', maximumFractionDigits: 1 })
  numFmt = new Intl.NumberFormat(locale, { maximumFractionDigits: 2 })
}

export const money = (v?: number | null) => moneyFmt.format(v ?? 0)
export const money2 = (v?: number | null) => money2Fmt.format(v ?? 0)
export const moneyCompact = (v?: number | null) => compactFmt.format(v ?? 0)
export const num = (v?: number | null) => numFmt.format(v ?? 0)
export const date = (v?: string | null) => (v ? dayjs(v).format('DD MMM YYYY') : '—')
export const dateTime = (v?: string | null) => (v ? dayjs(v).format('DD MMM YYYY, HH:mm') : '—')
export const monthLabel = (ym: string) => dayjs(ym + '-01').format('MMM YY')
export const pct = (v?: number | null) => (v === null || v === undefined ? '—' : `${v > 0 ? '+' : ''}${v.toFixed(1)}%`)

/** Splits PascalCase enum names for display: "PartiallyPaid" -> "Partially Paid". */
export const words = (s?: string | null) => (s ?? '').replace(/([a-z])([A-Z])/g, '$1 $2')

/** Downloads rows as a CSV file. */
export function downloadCsv(filename: string, rows: Record<string, unknown>[]) {
  if (rows.length === 0) return
  const headers = Object.keys(rows[0])
  const esc = (v: unknown) => {
    const s = v === null || v === undefined ? '' : String(v)
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
  }
  const csv = [headers.join(','), ...rows.map((r) => headers.map((h) => esc(r[h])).join(','))].join('\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

/** Today's date in the user's local time zone as yyyy-MM-dd (for date inputs). */
export const todayIso = () => dayjs().format('YYYY-MM-DD')

