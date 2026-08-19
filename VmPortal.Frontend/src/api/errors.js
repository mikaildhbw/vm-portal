// Gemeinsame Fehlerauswertung für die Admin-Seiten: Backend liefert bei Fehlern meist
// { message } (siehe Admin-Controller), sonst fällt dies auf eine statuscode-basierte
// generische Meldung zurück - der Nutzer sieht nie einen rohen Axios-/Konsolenfehler.
export function getErrorMessage(err, fallback) {
  const status = err?.response?.status
  const backendMessage = err?.response?.data?.message

  if (backendMessage) return backendMessage
  if (status === 403) return 'Kein Zugriff (403) - Berechtigung fehlt oder die Sitzung ist abgelaufen.'
  if (status === 404) return 'Nicht gefunden (404).'
  if (status === 502) return 'Verbindungsfehler zu AD oder Hypervisor (502).'
  if (status === 400) return 'Ungültige Anfrage (400).'

  return fallback ?? 'Ein unerwarteter Fehler ist aufgetreten.'
}
