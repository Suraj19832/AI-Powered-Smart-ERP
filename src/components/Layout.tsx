import { useEffect, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  AppBar, Avatar, Box, Chip, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, ListSubheader,
  Menu, MenuItem, Toolbar, Tooltip, Typography, useMediaQuery, useTheme,
} from '@mui/material'
import MenuIcon from '@mui/icons-material/Menu'
import DashboardIcon from '@mui/icons-material/SpaceDashboardOutlined'
import SmartToyIcon from '@mui/icons-material/SmartToyOutlined'
import InsightsIcon from '@mui/icons-material/InsightsOutlined'
import DocumentScannerIcon from '@mui/icons-material/DocumentScannerOutlined'
import PeopleIcon from '@mui/icons-material/PeopleAltOutlined'
import ReceiptLongIcon from '@mui/icons-material/ReceiptLongOutlined'
import RequestQuoteIcon from '@mui/icons-material/RequestQuoteOutlined'
import LocalShippingIcon from '@mui/icons-material/LocalShippingOutlined'
import ShoppingCartIcon from '@mui/icons-material/ShoppingCartOutlined'
import Inventory2Icon from '@mui/icons-material/Inventory2Outlined'
import CategoryIcon from '@mui/icons-material/CategoryOutlined'
import WarehouseIcon from '@mui/icons-material/WarehouseOutlined'
import SwapVertIcon from '@mui/icons-material/SwapVert'
import PaymentsIcon from '@mui/icons-material/PaymentsOutlined'
import AccountBalanceWalletIcon from '@mui/icons-material/AccountBalanceWalletOutlined'
import AssessmentIcon from '@mui/icons-material/AssessmentOutlined'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettingsOutlined'
import DarkModeIcon from '@mui/icons-material/DarkModeOutlined'
import LightModeIcon from '@mui/icons-material/LightModeOutlined'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import { useAppDispatch, useAppSelector } from '../app/store'
import { loggedOut } from '../app/authSlice'
import { api, useGet } from '../app/api'
import { SettingsContext, useColorMode } from './AppContext'
import { configureFormat } from '../utils/format'
import type { AppSettings } from '../types'
import { R } from '../roles'

const DRAWER = 248

interface NavItem { to: string; label: string; icon: ReactNode; roles?: string[] }
const NAV: { section: string; items: NavItem[] }[] = [
  { section: 'Overview', items: [{ to: '/', label: 'Dashboard', icon: <DashboardIcon /> }] },
  {
    section: 'AI',
    items: [
      { to: '/ai/assistant', label: 'AI Assistant', icon: <SmartToyIcon /> },
      { to: '/ai/insights', label: 'AI Insights', icon: <InsightsIcon /> },
      { to: '/ai/invoice-reader', label: 'Invoice Reader', icon: <DocumentScannerIcon />, roles: R.buy },
    ],
  },
  {
    section: 'Sales',
    items: [
      { to: '/customers', label: 'Customers', icon: <PeopleIcon /> },
      { to: '/sales-orders', label: 'Sales Orders', icon: <ShoppingCartIcon /> },
      { to: '/invoices', label: 'Invoices', icon: <ReceiptLongIcon /> },
    ],
  },
  {
    section: 'Purchasing',
    items: [
      { to: '/suppliers', label: 'Suppliers', icon: <LocalShippingIcon /> },
      { to: '/purchase-orders', label: 'Purchase Orders', icon: <RequestQuoteIcon /> },
    ],
  },
  {
    section: 'Inventory',
    items: [
      { to: '/products', label: 'Products', icon: <Inventory2Icon /> },
      { to: '/categories', label: 'Categories', icon: <CategoryIcon /> },
      { to: '/warehouses', label: 'Warehouses', icon: <WarehouseIcon /> },
      { to: '/stock', label: 'Stock Movements', icon: <SwapVertIcon /> },
    ],
  },
  {
    section: 'Finance',
    items: [
      { to: '/payments', label: 'Payments', icon: <PaymentsIcon />, roles: R.finance },
      { to: '/expenses', label: 'Expenses', icon: <AccountBalanceWalletIcon />, roles: R.finance },
      { to: '/reports', label: 'Reports', icon: <AssessmentIcon />, roles: R.reports },
    ],
  },
  { section: 'Administration', items: [{ to: '/users', label: 'Users & Roles', icon: <AdminPanelSettingsIcon />, roles: R.admin }] },
]

export default function Layout() {
  const theme = useTheme()
  const desktop = useMediaQuery(theme.breakpoints.up('md'))
  const [open, setOpen] = useState(false)
  const location = useLocation()
  const user = useAppSelector((s) => s.auth.user)
  const { data: settings } = useGet<AppSettings>('/settings')

  useEffect(() => { if (settings) configureFormat(settings.locale, settings.currency) }, [settings])
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setOpen(false), [location.pathname])

  const visible = NAV.map((g) => ({ ...g, items: g.items.filter((i) => !i.roles || user?.roles.some((r) => i.roles!.includes(r))) }))
    .filter((g) => g.items.length > 0)

  const drawer = (
    <Box sx={{ overflowY: 'auto', pb: 2 }}>
      <Toolbar sx={{ gap: 1.2 }}>
        <Box sx={{ width: 34, height: 34, borderRadius: 2, display: 'grid', placeItems: 'center', bgcolor: 'primary.main', color: 'primary.contrastText' }}>
          <AutoAwesomeIcon fontSize="small" />
        </Box>
        <Box>
          <Typography sx={{ fontWeight: 800, lineHeight: 1.1 }}>SmartERP</Typography>
          <Typography variant="caption" color="text.secondary">AI Business Suite</Typography>
        </Box>
      </Toolbar>
      <Divider />
      {visible.map((g) => (
        <List key={g.section} dense subheader={<ListSubheader sx={{ lineHeight: '32px', bgcolor: 'transparent' }}>{g.section}</ListSubheader>}>
          {g.items.map((i) => (
            <ListItemButton
              key={i.to}
              component={NavLink}
              to={i.to}
              end={i.to === '/'}
              sx={{ mx: 1, borderRadius: 2, '&.active': { bgcolor: 'action.selected', color: 'primary.main', '& .MuiListItemIcon-root': { color: 'primary.main' } } }}
            >
              <ListItemIcon sx={{ minWidth: 36 }}>{i.icon}</ListItemIcon>
              <ListItemText primary={i.label} />
            </ListItemButton>
          ))}
        </List>
      ))}
    </Box>
  )

  return (
    <SettingsContext.Provider value={settings ?? { companyName: 'SmartERP', currency: 'INR', locale: 'en-IN', aiEnabled: false, aiModel: null }}>
      <Box sx={{ display: 'flex', minHeight: '100vh' }}>
        <AppBar position="fixed" color="inherit" sx={{ borderBottom: 1, borderColor: 'divider', ml: { md: `${DRAWER}px` }, width: { md: `calc(100% - ${DRAWER}px)` } }}>
          <Toolbar>
            {!desktop && <IconButton edge="start" onClick={() => setOpen(true)} sx={{ mr: 1 }} aria-label="Open navigation"><MenuIcon /></IconButton>}
            <Typography sx={{ fontWeight: 600, flexGrow: 1 }} noWrap>{settings?.companyName ?? 'SmartERP'}</Typography>
            {settings && (
              <Tooltip title={settings.aiEnabled ? `Connected to OpenAI (${settings.aiModel})` : 'No OpenAI key configured — AI features run in offline mode'}>
                <Chip size="small" icon={<AutoAwesomeIcon />} label={settings.aiEnabled ? 'AI online' : 'AI offline mode'} color={settings.aiEnabled ? 'success' : 'default'} variant="outlined" sx={{ mr: 1, display: { xs: 'none', sm: 'flex' } }} />
              </Tooltip>
            )}
            <ThemeToggle />
            <UserMenu />
          </Toolbar>
        </AppBar>
        <Box component="nav" sx={{ width: { md: DRAWER }, flexShrink: { md: 0 } }}>
          {desktop ? (
            <Drawer variant="permanent" open sx={{ '& .MuiDrawer-paper': { width: DRAWER, boxSizing: 'border-box' } }}>{drawer}</Drawer>
          ) : (
            <Drawer variant="temporary" open={open} onClose={() => setOpen(false)} sx={{ '& .MuiDrawer-paper': { width: DRAWER } }}>{drawer}</Drawer>
          )}
        </Box>
        <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 2, md: 3 }, mt: 8 }}>
          <Outlet />
        </Box>
      </Box>
    </SettingsContext.Provider>
  )
}

function ThemeToggle() {
  const { mode, toggle } = useColorMode()
  return (
    <Tooltip title={mode === 'light' ? 'Dark mode' : 'Light mode'}>
      <IconButton onClick={toggle} aria-label="Toggle colour mode">{mode === 'light' ? <DarkModeIcon /> : <LightModeIcon />}</IconButton>
    </Tooltip>
  )
}

function UserMenu() {
  const user = useAppSelector((s) => s.auth.user)
  const refreshToken = useAppSelector((s) => s.auth.refreshToken)
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  const logout = async () => {
    setAnchor(null)
    if (refreshToken) await fetch('/api/auth/logout', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken }) }).catch(() => {})
    dispatch(loggedOut())
    dispatch(api.util.resetApiState())
    navigate('/login')
  }

  return (
    <>
      <IconButton onClick={(e) => setAnchor(e.currentTarget)} aria-label="Account">
        <Avatar sx={{ width: 32, height: 32, bgcolor: 'primary.main', fontSize: 14 }}>{user?.fullName?.[0] ?? '?'}</Avatar>
      </IconButton>
      <Menu anchorEl={anchor} open={!!anchor} onClose={() => setAnchor(null)}>
        <Box sx={{ px: 2, py: 1 }}>
          <Typography sx={{ fontWeight: 600 }}>{user?.fullName}</Typography>
          <Typography variant="body2" color="text.secondary">{user?.email}</Typography>
          <Typography variant="caption" color="text.secondary">{user?.roles.join(', ')}</Typography>
        </Box>
        <Divider />
        <MenuItem onClick={() => { setAnchor(null); navigate('/profile') }}>Change password</MenuItem>
        <MenuItem onClick={logout}>Sign out</MenuItem>
      </Menu>
    </>
  )
}
