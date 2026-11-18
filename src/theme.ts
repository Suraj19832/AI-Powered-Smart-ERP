import { createTheme, type PaletteMode } from '@mui/material/styles'

// Categorical chart palette (validated reference palette: fixed order, CVD-safe on adjacent pairs).
const SERIES_LIGHT = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948']
const SERIES_DARK = ['#3987e5', '#d95926', '#199e70', '#c98500', '#d55181', '#0ca30c', '#9085e9', '#e66767']

export const chartTokens = (mode: PaletteMode) => ({
  series: mode === 'light' ? SERIES_LIGHT : SERIES_DARK,
  grid: mode === 'light' ? '#e1e0d9' : '#2c2c2a',
  axis: mode === 'light' ? '#c3c2b7' : '#383835',
  muted: '#898781',
  text: mode === 'light' ? '#52514e' : '#c3c2b7',
  surface: mode === 'light' ? '#fcfcfb' : '#1a1a19',
})

export const buildTheme = (mode: PaletteMode) =>
  createTheme({
    palette: {
      mode,
      primary: { main: mode === 'light' ? '#2a5bd7' : '#6d93f0' },
      secondary: { main: '#6250d6' },
      background: mode === 'light' ? { default: '#f5f6f8', paper: '#ffffff' } : { default: '#0f1012', paper: '#1a1a19' },
    },
    shape: { borderRadius: 10 },
    typography: {
      fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
      h4: { fontWeight: 700 },
      h5: { fontWeight: 700 },
      h6: { fontWeight: 600 },
    },
    components: {
      MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { textTransform: 'none', fontWeight: 600 } } },
      MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { backgroundImage: 'none' } } },
      MuiCard: { styleOverrides: { root: { border: `1px solid ${mode === 'light' ? '#e6e8ec' : '#2c2c2a'}` } } },
      MuiTextField: { defaultProps: { size: 'small' } },
      MuiFormControl: { defaultProps: { size: 'small' } },
      MuiChip: { styleOverrides: { root: { fontWeight: 600 } } },
    },
  })
