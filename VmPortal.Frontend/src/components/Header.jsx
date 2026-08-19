import { Link, useNavigate } from 'react-router-dom'
import { logout } from '../api/vmApi'
import { useIsFullAdmin } from '../hooks/useIsFullAdmin'

function Header() {
  const navigate = useNavigate()
  // Kein Backend-Claim clientseitig lesbar (httpOnly-Cookie) - der Link wird nur nach einem
  // erfolgreichen Probe-Request gegen einen bestehenden Admin-Endpunkt eingeblendet, siehe
  // useIsFullAdmin. Ein manueller Aufruf von /admin ist zusätzlich über AdminLayout
  // abgesichert, dieser Link ist nur eine UI-Bequemlichkeit, keine eigentliche Schranke.
  const { isFullAdmin } = useIsFullAdmin()

  const handleLogout = async () => {
    try {
      await logout()
    } finally {
      navigate('/')
    }
  }

  return (
    <header className="app-header">
      <h1>VmPortal</h1>
      <div className="header-actions">
        {isFullAdmin && (
          <Link to="/admin" className="header-admin-link">
            Administration
          </Link>
        )}
        <button className="secondary" onClick={handleLogout}>
          Abmelden
        </button>
      </div>
    </header>
  )
}

export default Header
