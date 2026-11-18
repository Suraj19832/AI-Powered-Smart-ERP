import { lazy, Suspense, type ReactNode } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Alert } from '@mui/material'
import Layout from './components/Layout'
import { Loading } from './components/common'
import { useAppSelector } from './app/store'
import { R } from './roles'
import LoginPage from './pages/LoginPage'

const DashboardPage = lazy(() => import('./pages/DashboardPage'))
const AssistantPage = lazy(() => import('./pages/ai/AssistantPage'))
const InsightsPage = lazy(() => import('./pages/ai/InsightsPage'))
const InvoiceReaderPage = lazy(() => import('./pages/ai/InvoiceReaderPage'))
const CustomersPage = lazy(() => import('./pages/parties/CustomersPage'))
const SuppliersPage = lazy(() => import('./pages/parties/SuppliersPage'))
const PartyDetailPage = lazy(() => import('./pages/parties/PartyDetailPage'))
const ProductsPage = lazy(() => import('./pages/inventory/ProductsPage'))
const CategoriesPage = lazy(() => import('./pages/inventory/CategoriesPage'))
const WarehousesPage = lazy(() => import('./pages/inventory/WarehousesPage'))
const StockPage = lazy(() => import('./pages/inventory/StockPage'))
const SalesOrdersPage = lazy(() => import('./pages/orders/SalesOrdersPage'))
const PurchaseOrdersPage = lazy(() => import('./pages/orders/PurchaseOrdersPage'))
const OrderFormPage = lazy(() => import('./pages/orders/OrderFormPage'))
const SalesOrderDetailPage = lazy(() => import('./pages/orders/SalesOrderDetailPage'))
const PurchaseOrderDetailPage = lazy(() => import('./pages/orders/PurchaseOrderDetailPage'))
const InvoicesPage = lazy(() => import('./pages/finance/InvoicesPage'))
const InvoiceDetailPage = lazy(() => import('./pages/finance/InvoiceDetailPage'))
const PaymentsPage = lazy(() => import('./pages/finance/PaymentsPage'))
const ExpensesPage = lazy(() => import('./pages/finance/ExpensesPage'))
const ReportsPage = lazy(() => import('./pages/ReportsPage'))
const UsersPage = lazy(() => import('./pages/admin/UsersPage'))
const ProfilePage = lazy(() => import('./pages/admin/ProfilePage'))

function RequireAuth({ children }: { children: ReactNode }) {
  const user = useAppSelector((s) => s.auth.user)
  const location = useLocation()
  return user ? children : <Navigate to="/login" replace state={{ from: location.pathname }} />
}

function RequireRole({ roles, children }: { roles: string[]; children: ReactNode }) {
  const user = useAppSelector((s) => s.auth.user)
  return user?.roles.some((r) => roles.includes(r)) ? children : <Alert severity="warning">You don’t have access to this page.</Alert>
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth><Suspense fallback={<Loading />}><Layout /></Suspense></RequireAuth>}>
        <Route index element={<Page><DashboardPage /></Page>} />
        <Route path="ai/assistant" element={<Page><AssistantPage /></Page>} />
        <Route path="ai/insights" element={<Page><InsightsPage /></Page>} />
        <Route path="ai/invoice-reader" element={<Page roles={R.buy}><InvoiceReaderPage /></Page>} />
        <Route path="customers" element={<Page><CustomersPage /></Page>} />
        <Route path="customers/:id" element={<Page><PartyDetailPage kind="customer" /></Page>} />
        <Route path="suppliers" element={<Page><SuppliersPage /></Page>} />
        <Route path="suppliers/:id" element={<Page><PartyDetailPage kind="supplier" /></Page>} />
        <Route path="products" element={<Page><ProductsPage /></Page>} />
        <Route path="categories" element={<Page><CategoriesPage /></Page>} />
        <Route path="warehouses" element={<Page><WarehousesPage /></Page>} />
        <Route path="stock" element={<Page><StockPage /></Page>} />
        <Route path="sales-orders" element={<Page><SalesOrdersPage /></Page>} />
        <Route path="sales-orders/new" element={<Page roles={R.sell}><OrderFormPage kind="sales" /></Page>} />
        <Route path="sales-orders/:id" element={<Page><SalesOrderDetailPage /></Page>} />
        <Route path="sales-orders/:id/edit" element={<Page roles={R.sell}><OrderFormPage kind="sales" /></Page>} />
        <Route path="purchase-orders" element={<Page><PurchaseOrdersPage /></Page>} />
        <Route path="purchase-orders/new" element={<Page roles={R.buy}><OrderFormPage kind="purchase" /></Page>} />
        <Route path="purchase-orders/:id" element={<Page><PurchaseOrderDetailPage /></Page>} />
        <Route path="purchase-orders/:id/edit" element={<Page roles={R.buy}><OrderFormPage kind="purchase" /></Page>} />
        <Route path="invoices" element={<Page><InvoicesPage /></Page>} />
        <Route path="invoices/:id" element={<Page><InvoiceDetailPage /></Page>} />
        <Route path="payments" element={<Page roles={R.finance}><PaymentsPage /></Page>} />
        <Route path="expenses" element={<Page roles={R.finance}><ExpensesPage /></Page>} />
        <Route path="reports" element={<Page roles={R.reports}><ReportsPage /></Page>} />
        <Route path="users" element={<Page roles={R.admin}><UsersPage /></Page>} />
        <Route path="profile" element={<Page><ProfilePage /></Page>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

function Page({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const content = <Suspense fallback={<Loading />}>{children}</Suspense>
  return roles ? <RequireRole roles={roles}>{content}</RequireRole> : content
}
