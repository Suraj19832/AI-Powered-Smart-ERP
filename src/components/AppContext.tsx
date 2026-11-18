import { createContext, useContext } from 'react'
import type { PaletteMode } from '@mui/material'
import type { AppSettings } from '../types'

export const ColorModeContext = createContext<{ mode: PaletteMode; toggle: () => void }>({ mode: 'light', toggle: () => {} })
export const useColorMode = () => useContext(ColorModeContext)

export const SettingsContext = createContext<AppSettings>({
  companyName: 'SmartERP', currency: 'INR', locale: 'en-IN', aiEnabled: false, aiModel: null,
})
export const useSettings = () => useContext(SettingsContext)
