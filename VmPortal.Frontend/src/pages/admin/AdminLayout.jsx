import { NavLink, Navigate, Outlet } from 'react-router-dom'
import Header from '../../components/Header'
import { useIsFullAdmin } from '../../hooks/useIsFullAdmin'

const navLinkClass = ({ isActive }) => `admin-nav-link${isActive ? ' active' : ''}`

// Zugriffs-Gate für alle /admin/*-Routen: prüft einmalig beim Betreten (nicht erst beim
// ersten fehlschlagenden Request), ob der Nutzer FullAdmin ist, und leitet Nicht-Admins
// sofort zur normalen VM-Übersicht um, statt eine leere/kaputte Seite zu zeigen.
function AdminLayout() {
  const { loading, isFullAdmin } = useIsFullAdmin()

  if (loading) {
    return (
      <>
        <Header />
        <div className="container">
          <p className="notice">Zugriff wird geprüft…</p>
        </div>
      </>
    )
  }

  if (!isFullAdmin) {
    return <Navigate to="/vms" replace />
  }

  return (
    <>
      <Header />
      <div className="container admin-container">
        <h2>Administration</h2>
        <nav className="admin-nav">
          <NavLink to="/admin/roles" className={navLinkClass}>
            Rollen
          </NavLink>
          <NavLink to="/admin/vm-groups" className={navLinkClass}>
            VM-Gruppen
          </NavLink>
          <NavLink to="/admin/permissions" className={navLinkClass}>
            Zuordnungen
          </NavLink>
        </nav>
        <Outlet />
      </div>
    </>
  )
}

export default AdminLayout
