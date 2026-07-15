import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Header from '../components/Header'
import { getVms, statusLabel, VmStatus } from '../api/vmApi'

function VmList() {
  const [vms, setVms] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const navigate = useNavigate()

  useEffect(() => {
    const load = async () => {
      try {
        const response = await getVms()
        setVms(response.data)
      } catch (err) {
        // 401 wird zentral im Interceptor behandelt (Weiterleitung zum Login).
        if (err.response?.status !== 401) {
          setError('VMs konnten nicht geladen werden.')
        }
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [])

  return (
    <>
      <Header />
      <div className="container">
        <h2>Meine virtuellen Maschinen</h2>
        {loading && <p className="notice">Wird geladen…</p>}
        {error && <p className="notice error">{error}</p>}
        {!loading && !error && vms.length === 0 && (
          <p className="notice">Ihnen sind derzeit keine VMs zugewiesen.</p>
        )}
        <ul className="vm-list">
          {vms.map((vm) => {
            const isRunning = vm.status === VmStatus.Running
            return (
              <li
                key={vm.id}
                className={`vm-row ${isRunning ? 'running' : ''}`}
                onClick={() => navigate(`/vms/${encodeURIComponent(vm.id)}`)}
              >
                <span className="vm-name">{vm.name}</span>
                <span className={`status-badge ${isRunning ? 'running' : ''}`}>
                  {statusLabel(vm.status)}
                </span>
              </li>
            )
          })}
        </ul>
      </div>
    </>
  )
}

export default VmList
