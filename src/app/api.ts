import { createApi, fetchBaseQuery, type BaseQueryFn, type FetchArgs, type FetchBaseQueryError } from '@reduxjs/toolkit/query/react'
import type { AuthResponse, ChatReply, ChatTurn, AiReport, ExtractedInvoice } from '../types'
import { credentialsReceived, loggedOut } from './authSlice'

type RootLike = { auth: { accessToken: string | null; refreshToken: string | null } }

const rawBaseQuery = fetchBaseQuery({
  baseUrl: '/api',
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as RootLike).auth.accessToken
    if (token) headers.set('Authorization', `Bearer ${token}`)
    return headers
  },
})

// Refresh tokens are rotated on every use, so concurrent 401s must share ONE refresh call —
// otherwise the second refresh would present an already-revoked token and trigger reuse detection.
let refreshing: Promise<boolean> | null = null

const baseQueryWithReauth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (args, api, extra) => {
  let result = await rawBaseQuery(args, api, extra)
  const url = typeof args === 'string' ? args : args.url
  if (result.error?.status === 401 && !url.startsWith('/auth/')) {
    const refreshToken = (api.getState() as RootLike).auth.refreshToken
    if (!refreshToken) {
      api.dispatch(loggedOut())
      return result
    }
    refreshing ??= (async () => {
      const r = await rawBaseQuery({ url: '/auth/refresh', method: 'POST', body: { refreshToken } }, api, extra)
      if (r.data) api.dispatch(credentialsReceived(r.data as AuthResponse))
      else api.dispatch(loggedOut())
      return !!r.data
    })().finally(() => { refreshing = null })
    if (await refreshing) result = await rawBaseQuery(args, api, extra)
  }
  return result
}

export type Params = Record<string, string | number | boolean | null | undefined>

const clean = (p?: Params) =>
  p ? Object.fromEntries(Object.entries(p).filter(([, v]) => v !== undefined && v !== null && v !== '')) : undefined

export const api = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithReauth,
  tagTypes: ['Data'],
  endpoints: (b) => ({
    /** Generic GET. Every query is tagged 'Data' and every write invalidates it, so screens stay consistent
     *  after transactions that touch several modules (e.g. confirming an order updates stock, invoices, dashboard). */
    get: b.query<unknown, { url: string; params?: Params }>({
      query: ({ url, params }) => ({ url, params: clean(params) }),
      providesTags: ['Data'],
    }),
    mutate: b.mutation<unknown, { url: string; method?: 'POST' | 'PUT' | 'DELETE'; body?: unknown }>({
      query: ({ url, method = 'POST', body }) => ({ url, method, body }),
      invalidatesTags: ['Data'],
    }),
    login: b.mutation<AuthResponse, { userName: string; password: string }>({
      query: (body) => ({ url: '/auth/login', method: 'POST', body }),
    }),
    chat: b.mutation<ChatReply, { messages: ChatTurn[] }>({
      query: (body) => ({ url: '/ai/chat', method: 'POST', body }),
    }),
    aiReport: b.mutation<AiReport, { type: string; month?: string }>({
      query: (body) => ({ url: '/ai/reports', method: 'POST', body }),
    }),
    readInvoice: b.mutation<ExtractedInvoice, File>({
      query: (file) => {
        const form = new FormData()
        form.append('file', file)
        return { url: '/ai/invoice-reader', method: 'POST', body: form }
      },
    }),
  }),
})

export const { useLoginMutation, useChatMutation, useAiReportMutation, useReadInvoiceMutation } = api

/** Typed wrapper around the generic GET query. */
export function useGet<T>(url: string | null, params?: Params) {
  const r = api.useGetQuery({ url: url ?? '', params }, { skip: !url })
  return r as Omit<typeof r, 'data'> & { data?: T }
}

export function useLazyGet<T>() {
  const [trigger, r] = api.useLazyGetQuery()
  return [(url: string, params?: Params) => trigger({ url, params }).unwrap() as Promise<T>, r as Omit<typeof r, 'data'> & { data?: T }] as const
}

/** Returns a promise-based writer: `await mutate('/products', 'POST', body)`. */
export function useMutate() {
  const [trigger, state] = api.useMutateMutation()
  const run = <T = unknown>(url: string, method: 'POST' | 'PUT' | 'DELETE' = 'POST', body?: unknown) =>
    trigger({ url, method, body }).unwrap() as Promise<T>
  return [run, state] as const
}

/** Extracts a human-readable message from an RTK Query / ProblemDetails error. */
export function errorMessage(err: unknown): string {
  const e = err as { status?: number | string; data?: { detail?: string; title?: string; errors?: Record<string, string[]> } }
  if (!e) return 'Unknown error'
  if (e.status === 429) return 'Too many requests — please wait a moment and try again.'
  if (e.status === 403) return 'You do not have permission to perform this action.'
  if (e.status === 'FETCH_ERROR') return 'Cannot reach the API. Is the backend running on http://localhost:5080?'
  if (e.data?.errors) return Object.values(e.data.errors).flat().join(' ')
  return e.data?.detail ?? e.data?.title ?? (typeof e.status === 'number' ? `Request failed (${e.status})` : 'Request failed')
}
