import { useEffect, useState, type ReactNode } from 'react'
import { Box, Card, InputAdornment, Stack, TextField } from '@mui/material'
import SearchIcon from '@mui/icons-material/Search'
import { DataGrid, type GridColDef, type GridPaginationModel, type GridRowParams, type GridSortModel, type GridValidRowModel } from '@mui/x-data-grid'
import { useGet, type Params } from '../app/api'
import type { Paged } from '../types'
import { ErrorAlert } from './common'

interface Props<T extends GridValidRowModel> {
  url: string
  columns: GridColDef<T>[]
  params?: Params
  filters?: ReactNode
  searchPlaceholder?: string
  onRowClick?: (row: T) => void
  initialSort?: GridSortModel
  height?: number | string
}

/** DataGrid with server-side paging, sorting and debounced search against a PagedResult<T> endpoint. */
export function ServerGrid<T extends GridValidRowModel & { id: number }>({
  url, columns, params, filters, searchPlaceholder = 'Search…', onRowClick, initialSort = [], height = 'calc(100vh - 290px)',
}: Props<T>) {
  const [pagination, setPagination] = useState<GridPaginationModel>({ page: 0, pageSize: 25 })
  const [sort, setSort] = useState<GridSortModel>(initialSort)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(searchInput)
      setPagination((p) => ({ ...p, page: 0 }))
    }, 350)
    return () => clearTimeout(t)
  }, [searchInput])

  const { data, isFetching, error } = useGet<Paged<T>>(url, {
    page: pagination.page + 1,
    pageSize: pagination.pageSize,
    search,
    sortBy: sort[0]?.field,
    desc: sort[0]?.sort === 'desc',
    ...params,
  })

  return (
    <Card>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ p: 2, alignItems: { md: 'center' }, flexWrap: 'wrap', gap: 1.5 }}>
        <TextField
          placeholder={searchPlaceholder}
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          sx={{ minWidth: 260 }}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }}
        />
        {filters}
      </Stack>
      <Box sx={{ px: 2 }}><ErrorAlert error={error} /></Box>
      <Box sx={{ height, minHeight: 420 }}>
        <DataGrid
          rows={data?.items ?? []}
          columns={columns}
          rowCount={data?.totalCount ?? 0}
          loading={isFetching}
          paginationMode="server"
          sortingMode="server"
          paginationModel={pagination}
          onPaginationModelChange={setPagination}
          sortModel={sort}
          onSortModelChange={(m) => { setSort(m); setPagination((p) => ({ ...p, page: 0 })) }}
          pageSizeOptions={[10, 25, 50, 100]}
          disableRowSelectionOnClick
          disableColumnMenu
          onRowClick={onRowClick ? (p: GridRowParams<T>) => onRowClick(p.row) : undefined}
          sx={{ border: 0, '& .MuiDataGrid-row': { cursor: onRowClick ? 'pointer' : 'default' } }}
        />
      </Box>
    </Card>
  )
}
