import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { login } from '../api/vmApi'

function Login() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const navigate = useNavigate()

  const handleSubmit = async (event) => {
    event.preventDefault()
    setError('')
    setSubmitting(true)
    try {
      await login(username, password)
      navigate('/vms')
    } catch (err) {
      const message = err.response?.data?.message ?? 'Anmeldung fehlgeschlagen'
      setError(message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-wrapper">
      <form className="login-card" onSubmit={handleSubmit}>
        <h1>VmPortal</h1>
        {error && <div className="form-error">{error}</div>}
        <div className="field">
          <label htmlFor="username">Benutzername</label>
          <input
            id="username"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            autoFocus
            required
          />
        </div>
        <div className="field">
          <label htmlFor="password">Passwort</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </div>
        <button className="full-width" type="submit" disabled={submitting}>
          {submitting ? 'Anmeldung läuft…' : 'Anmelden'}
        </button>
      </form>
    </div>
  )
}

export default Login
