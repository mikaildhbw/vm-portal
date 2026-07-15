import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import Header from '../components/Header'
import {
  createSnapshot,
  getVms,
  resetVm,
  startVm,
  statusLabel,
  stopVm,
  VmStatus,
} from '../api/vmApi'

const POLL_INTERVAL_MS = 5000

function VmDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [vm, setVm] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [snapshotName, setSnapshotName] = useState('')
  const [message, setMessage] = useState('')

  const loadVm = useCallback(async () => {
    try {
      const response = await getVms()
      const found = response.data.find((item) => item.id === id)
      if (!found) {
        setError('VM nicht gefunden oder kein Zugriff.')
      } else {
        setVm(found)
        setError('')
      }
    } catch (err) {
      if (err.response?.status !== 401) {
        setError('VM konnte nicht geladen werden.')
      }
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    loadVm()
    const timer = setInterval(loadVm, POLL_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [loadVm])

  const runAction = async (action, label) => {
    setBusy(true)
    setMessage('')
    setError('')
    try {
      await action(id)
      setMessage(`${label} ausgelöst.`)
      await loadVm()
    } catch (err) {
      const detail = err.response?.data?.message ?? 'Aktion fehlgeschlagen.'
      setError(detail)
    } finally {
      setBusy(false)
    }
  }

  const handleSnapshot = async () => {
    if (!snapshotName.trim()) return
    setBusy(true)
    setMessage('')
    setError('')
    try {
      await createSnapshot(id, snapshotName.trim())
      setMessage(`Snapshot „${snapshotName.trim()}" erstellt.`)
      setSnapshotName('')
    } catch (err) {
      const detail = err.response?.data?.message ?? 'Snapshot fehlgeschlagen.'
      setError(detail)
    } finally {
      setBusy(false)
    }
  }

  const isRunning = vm?.status === VmStatus.Running
  const isStopped = vm?.status === VmStatus.Stopped

  return (
    <>
      <Header />
      <div className="container">
        <div className="toolbar">
          <button className="secondary" onClick={() => navigate('/vms')}>
            ← Zurück zur Übersicht
          </button>
        </div>

        {loading && <p className="notice">Wird geladen…</p>}
        {error && <p className="notice error">{error}</p>}

        {vm && (
          <div className="detail-card">
            <h2>{vm.name}</h2>
            <p className="detail-meta">
              Status:{' '}
              <span className={`status-badge ${isRunning ? 'running' : ''}`}>
                {statusLabel(vm.status)}
              </span>
            </p>

            {message && <div className="form-error" style={{ borderColor: '#009999', color: '#007a7a', backgroundColor: 'rgba(0,153,153,0.08)' }}>{message}</div>}

            <div className="action-row">
              <button disabled={busy || isRunning} onClick={() => runAction(startVm, 'Start')}>
                Start
              </button>
              <button disabled={busy || isStopped} onClick={() => runAction(stopVm, 'Stopp')}>
                Stopp
              </button>
              <button disabled={busy || isStopped} onClick={() => runAction(resetVm, 'Neustart')}>
                Neustart
              </button>
            </div>

            <div className="snapshot-row">
              <div className="field">
                <label htmlFor="snapshot">Snapshot-Name</label>
                <input
                  id="snapshot"
                  type="text"
                  value={snapshotName}
                  onChange={(e) => setSnapshotName(e.target.value)}
                  placeholder="z. B. vor-update"
                />
              </div>
              <button disabled={busy || !snapshotName.trim()} onClick={handleSnapshot}>
                Snapshot erstellen
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  )
}

export default VmDetail
