import { useEffect, useState } from 'react'
import { getServers } from '../api/adminApi'

// Das JWT liegt im httpOnly-Cookie und ist damit clientseitig nicht lesbar (bewusste
// Sicherheitsentscheidung, siehe CLAUDE.md) - es gibt also keinen Claim, den das Frontend
// direkt auslesen könnte, um FullAdmin-Zugriff zu erkennen. Es gibt auch keinen eigenen
// "wer bin ich"-Endpunkt. Stattdessen fragen wir einen bestehenden, günstigen
// Admin-only-Endpunkt ab (GET /api/admin/servers - kleinste vorhandene Admin-Liste): 200
// heißt FullAdmin, 403 (oder jeder andere Fehler) heißt nicht.
export function useIsFullAdmin() {
  const [state, setState] = useState({ loading: true, isFullAdmin: false })

  useEffect(() => {
    let cancelled = false

    getServers()
      .then(() => {
        if (!cancelled) setState({ loading: false, isFullAdmin: true })
      })
      .catch(() => {
        if (!cancelled) setState({ loading: false, isFullAdmin: false })
      })

    return () => {
      cancelled = true
    }
  }, [])

  return state
}
