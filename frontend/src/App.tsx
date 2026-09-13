import type { ReactNode } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { PulseDots } from './ui/feedback'
import { useRoles } from './auth/useRoles'
import { AuthScreen } from './pages/AuthScreen'
import { Forbidden } from './pages/Forbidden'
import { Dashboard } from './pages/Dashboard'
import { ProjectDetail } from './pages/ProjectDetail'
import { Catalog } from './pages/Catalog'
import { ECatalog } from './pages/ECatalog'
import { ECatalogProduct } from './pages/ECatalogProduct'
import { ECatalogCompare } from './pages/ECatalogCompare'
import { Retail } from './pages/Retail'
import { SpecificationsList } from './pages/SpecificationsList'
import { SpecificationDetail } from './pages/SpecificationDetail'
import { QuoteCompare } from './pages/QuoteCompare'
import { KPDetail } from './pages/KPDetail'
import { MatchResults } from './pages/MatchResults'
import { OrdersList } from './pages/OrdersList'
import { OrderDetail } from './pages/OrderDetail'
import { Prices } from './pages/Prices'
import { Suppliers } from './pages/Suppliers'
import { Discounts } from './pages/Discounts'
import { Audit } from './pages/Audit'
import { Approval } from './pages/mock/Approval'
import { Invoice } from './pages/mock/Invoice'
import { Warehouse } from './pages/mock/Warehouse'
import { Manufacturers } from './pages/mock/Manufacturers'
import { Users } from './pages/mock/Users'
import { Analytics } from './pages/mock/Analytics'
import { Settings } from './pages/mock/Settings'
import { Placeholder } from './pages/Placeholder'

function Splash({ children }: { children: ReactNode }) {
  return (
    <div className="app-frame" style={{
      height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'var(--sidebar-bg)', color: 'var(--accent)', fontSize: 14,
    }}>
      {children}
    </div>
  )
}

function Guard({ children }: { children: ReactNode }) {
  const { pathname } = useLocation()
  const { canAccess } = useRoles()
  if (!canAccess(pathname)) return <Forbidden path={pathname} />
  return children
}

function RoleHome() {
  const { home } = useRoles()
  if (home !== '/') return <Navigate to={home} replace />
  return <Dashboard />
}

export default function App() {
  const auth = useAuth()

  if (auth.isLoading) return <Splash><PulseDots invert label="Открываем сессию" style={{ color: 'var(--accent)' }} /></Splash>
  if (!auth.isAuthenticated) return <AuthScreen />

  return (
    <Routes>
      <Route path="/" element={<RoleHome />} />
      <Route path="/projects/:id" element={<Guard><ProjectDetail /></Guard>} />
      <Route path="/ecatalog" element={<Guard><ECatalog /></Guard>} />
      <Route path="/ecatalog/compare" element={<Guard><ECatalogCompare /></Guard>} />
      <Route path="/ecatalog/:id" element={<Guard><ECatalogProduct /></Guard>} />
      <Route path="/retail" element={<Guard><Retail /></Guard>} />
      <Route path="/catalog" element={<Guard><Catalog /></Guard>} />
      <Route path="/specifications" element={<Guard><SpecificationsList /></Guard>} />
      <Route path="/specifications/:id" element={<Guard><SpecificationDetail /></Guard>} />
      <Route path="/specifications/:id/match" element={<Guard><MatchResults /></Guard>} />
      <Route path="/specifications/:id/quote" element={<Guard><QuoteCompare /></Guard>} />
      <Route path="/specifications/:id/quote/detail" element={<Guard><KPDetail /></Guard>} />
      <Route path="/prices" element={<Guard><Prices /></Guard>} />
      <Route path="/orders" element={<Guard><OrdersList /></Guard>} />
      <Route path="/orders/:id" element={<Guard><OrderDetail /></Guard>} />
      <Route path="/suppliers" element={<Guard><Suppliers /></Guard>} />
      <Route path="/discounts" element={<Guard><Discounts /></Guard>} />
      <Route path="/manufacturers" element={<Guard><Manufacturers /></Guard>} />
      <Route path="/approval" element={<Guard><Approval /></Guard>} />
      <Route path="/invoice" element={<Guard><Invoice /></Guard>} />
      <Route path="/warehouse" element={<Guard><Warehouse /></Guard>} />
      <Route path="/analytics" element={<Guard><Analytics /></Guard>} />
      <Route path="/audit" element={<Guard><Audit /></Guard>} />
      <Route path="/users" element={<Guard><Users /></Guard>} />
      <Route path="/settings" element={<Guard><Settings /></Guard>} />
      <Route path="*" element={<Placeholder title="Страница не найдена" breadcrumb={['404']} />} />
    </Routes>
  )
}
