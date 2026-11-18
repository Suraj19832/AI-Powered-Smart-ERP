import { configureStore } from '@reduxjs/toolkit'
import { useDispatch, useSelector } from 'react-redux'
import authReducer from './authSlice'
import { api } from './api'

export const store = configureStore({
  reducer: {
    auth: authReducer,
    [api.reducerPath]: api.reducer,
  },
  middleware: (getDefault) => getDefault().concat(api.middleware),
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
export const useAppDispatch = useDispatch.withTypes<AppDispatch>()
export const useAppSelector = useSelector.withTypes<RootState>()

/** True if the signed-in user has any of the given roles. */
export function useHasRole(...roles: string[]) {
  const user = useAppSelector((s) => s.auth.user)
  return !!user && (roles.length === 0 || user.roles.some((r) => roles.includes(r)))
}
