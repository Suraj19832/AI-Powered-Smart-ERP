import { createSlice, type PayloadAction } from '@reduxjs/toolkit'
import type { AuthResponse, UserInfo } from '../types'

// The refresh token and user profile persist across reloads; the short-lived access token is kept in memory
// only and re-acquired via /api/auth/refresh on the first 401.
const STORAGE_KEY = 'smarterp.auth'

interface AuthState { user: UserInfo | null; accessToken: string | null; refreshToken: string | null }

function load(): AuthState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) {
      const s = JSON.parse(raw) as { user: UserInfo; refreshToken: string }
      return { user: s.user, refreshToken: s.refreshToken, accessToken: null }
    }
  } catch { /* ignore corrupt storage */ }
  return { user: null, accessToken: null, refreshToken: null }
}

function persist(s: AuthState) {
  if (s.user && s.refreshToken) localStorage.setItem(STORAGE_KEY, JSON.stringify({ user: s.user, refreshToken: s.refreshToken }))
  else localStorage.removeItem(STORAGE_KEY)
}

const authSlice = createSlice({
  name: 'auth',
  initialState: load(),
  reducers: {
    credentialsReceived(state, action: PayloadAction<AuthResponse>) {
      state.user = action.payload.user
      state.accessToken = action.payload.accessToken
      state.refreshToken = action.payload.refreshToken
      persist(state)
    },
    loggedOut(state) {
      state.user = null
      state.accessToken = null
      state.refreshToken = null
      persist(state)
    },
  },
})

export const { credentialsReceived, loggedOut } = authSlice.actions
export default authSlice.reducer
