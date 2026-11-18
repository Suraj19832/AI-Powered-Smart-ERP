import { StrictMode, useMemo, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { BrowserRouter } from 'react-router-dom'
import { CssBaseline, ThemeProvider, type PaletteMode } from '@mui/material'
import { store } from './app/store'
import { buildTheme } from './theme'
import { ColorModeContext } from './components/AppContext'
import App from './App'

function Root() {
  const [mode, setMode] = useState<PaletteMode>(() => {
    try {
      const saved = localStorage.getItem('smarterp.theme')
      if (saved === 'light' || saved === 'dark') return saved
    } catch { /* storage unavailable */ }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  })
  const theme = useMemo(() => buildTheme(mode), [mode])
  const ctx = useMemo(() => ({
    mode,
    toggle: () => setMode((m) => {
      const next = m === 'light' ? 'dark' : 'light'
      try { localStorage.setItem('smarterp.theme', next) } catch { /* ignore */ }
      return next
    }),
  }), [mode])

  return (
    <ColorModeContext.Provider value={ctx}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <App />
      </ThemeProvider>
    </ColorModeContext.Provider>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <BrowserRouter>
        <Root />
      </BrowserRouter>
    </Provider>
  </StrictMode>,
)
