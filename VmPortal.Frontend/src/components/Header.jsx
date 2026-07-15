import { useNavigate } from 'react-router-dom'
import { logout } from '../api/vmApi'

function Header() {
  const navigate = useNavigate()

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
      <button className="secondary" onClick={handleLogout}>
        Abmelden
      </button>
    </header>
  )
}

export default Header
