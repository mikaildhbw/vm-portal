import client from './client'

export const VmStatus = {
  Running: 0,
  Stopped: 1,
  Paused: 2,
  Unknown: 3,
}

export const statusLabel = (status) => {
  switch (status) {
    case VmStatus.Running:
      return 'Läuft'
    case VmStatus.Stopped:
      return 'Gestoppt'
    case VmStatus.Paused:
      return 'Pausiert'
    default:
      return 'Unbekannt'
  }
}

export const login = (username, password) =>
  client.post('/auth/login', { username, password })

export const logout = () => client.post('/auth/logout')

export const getVms = () => client.get('/vm')

export const startVm = (name) => client.post(`/vm/${encodeURIComponent(name)}/start`)

export const stopVm = (name) => client.post(`/vm/${encodeURIComponent(name)}/stop`)

export const resetVm = (name) => client.post(`/vm/${encodeURIComponent(name)}/reset`)

export const createSnapshot = (name, snapshotName) =>
  client.post(`/vm/${encodeURIComponent(name)}/snapshot`, JSON.stringify(snapshotName), {
    headers: { 'Content-Type': 'application/json' },
  })
