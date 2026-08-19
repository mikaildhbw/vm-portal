import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import Modal from '../../components/Modal'
import {
  addVmGroupMembers,
  discoverVms,
  getVmGroup,
  getVmGroupMembers,
  removeVmGroupMember,
} from '../../api/adminApi'
import { getErrorMessage } from '../../api/errors'

function AddVmsModal({ groupId, groupName, existingMemberKeys, onClose, onAdded }) {
  const [vms, setVms] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filterText, setFilterText] = useState('')
  const [hostFilter, setHostFilter] = useState('')
  const [selectedKeys, setSelectedKeys] = useState(() => new Set())
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    discoverVms()
      .then((response) => setVms(response.data))
      .catch((err) => setError(getErrorMessage(err, 'Hypervisor-Inventar konnte nicht geladen werden.')))
      .finally(() => setLoading(false))
  }, [])

  const hosts = useMemo(
    () => [...new Set(vms.map((vm) => vm.hostName))].sort((a, b) => a.localeCompare(b)),
    [vms],
  )

  // Bereits Mitglied dieser Gruppe: aus der Auswahl ausblenden (steht schon in der
  // Mitgliederliste dahinter). Filterung selbst ist eine reine, mit useMemo gecachte
  // Array-Operation auf bereits geladenen Daten - kein erneuter Request, kein schwerer
  // Re-Render pro Tastenanschlag.
  const selectableVms = useMemo(
    () => vms.filter((vm) => !existingMemberKeys.has(`${vm.hostName}::${vm.vmName}`.toLowerCase())),
    [vms, existingMemberKeys],
  )

  const filteredVms = useMemo(() => {
    const needle = filterText.trim().toLowerCase()
    return selectableVms.filter((vm) => {
      if (hostFilter && vm.hostName !== hostFilter) return false
      if (!needle) return true
      return vm.vmName.toLowerCase().includes(needle) || vm.hostName.toLowerCase().includes(needle)
    })
  }, [selectableVms, filterText, hostFilter])

  const toggleSelected = (vm) => {
    const key = `${vm.hostName}::${vm.vmName}`
    setSelectedKeys((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  const handleSubmit = async () => {
    const toAdd = filteredVms
      .filter((vm) => selectedKeys.has(`${vm.hostName}::${vm.vmName}`))
      .map((vm) => ({ hostName: vm.hostName, vmName: vm.vmName, vmGuid: vm.vmGuid }))

    if (toAdd.length === 0) return

    setSubmitting(true)
    setError('')
    try {
      await addVmGroupMembers(groupId, toAdd)
      onAdded()
    } catch (err) {
      setError(getErrorMessage(err, 'VMs konnten nicht hinzugefügt werden.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal title={`VMs zu "${groupName}" hinzufügen`} onClose={onClose}>
      {error && <p className="notice error">{error}</p>}
      {loading && <p className="notice">Hypervisor-Inventar wird geladen…</p>}

      {!loading && (
        <>
          <div className="inline-form modal-filters">
            <div className="field">
              <label htmlFor="vm-filter">Suche (Host oder Name)</label>
              <input
                id="vm-filter"
                type="text"
                value={filterText}
                onChange={(e) => setFilterText(e.target.value)}
                placeholder="z. B. HVP oder MHM-HYPERV4"
              />
            </div>
            <div className="field">
              <label htmlFor="host-filter">Host</label>
              <select id="host-filter" value={hostFilter} onChange={(e) => setHostFilter(e.target.value)}>
                <option value="">Alle Hosts</option>
                {hosts.map((host) => (
                  <option key={host} value={host}>
                    {host || '(kein Host / lokaler Modus)'}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <p className="notice">
            {filteredVms.length} von {selectableVms.length} verfügbaren VMs (bereits in dieser Gruppe
            enthaltene VMs werden hier nicht angezeigt).
          </p>

          <div className="table-scroll modal-table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  <th></th>
                  <th>Host</th>
                  <th>VM-Name</th>
                  <th>Status</th>
                  <th>Zuordnung</th>
                </tr>
              </thead>
              <tbody>
                {filteredVms.map((vm) => {
                  const key = `${vm.hostName}::${vm.vmName}`
                  const inOtherGroup = vm.existsInDb && vm.groupId !== null
                  return (
                    <tr key={key}>
                      <td>
                        <input
                          type="checkbox"
                          checked={selectedKeys.has(key)}
                          onChange={() => toggleSelected(vm)}
                        />
                      </td>
                      <td>{vm.hostName || '—'}</td>
                      <td>{vm.vmName}</td>
                      <td>{vm.status}</td>
                      <td>
                        {inOtherGroup ? (
                          <span className="badge badge-warning" title="Hinzufügen verschiebt die VM in diese Gruppe">
                            In „{vm.groupName}&ldquo; — wird verschoben
                          </span>
                        ) : vm.existsInDb ? (
                          <span className="badge badge-neutral">Bekannt, keiner Gruppe zugeordnet</span>
                        ) : (
                          <span className="badge badge-neutral">Neu (noch nicht in der DB)</span>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          <div className="modal-footer">
            <button type="button" onClick={handleSubmit} disabled={submitting || selectedKeys.size === 0}>
              {submitting ? 'Wird hinzugefügt…' : `Ausgewählte hinzufügen (${selectedKeys.size})`}
            </button>
          </div>
        </>
      )}
    </Modal>
  )
}

function VmGroupDetailPage() {
  const { groupId } = useParams()
  const [group, setGroup] = useState(null)
  const [members, setMembers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showAddModal, setShowAddModal] = useState(false)

  const load = async () => {
    setError('')
    try {
      const [groupResponse, membersResponse] = await Promise.all([
        getVmGroup(groupId),
        getVmGroupMembers(groupId),
      ])
      setGroup(groupResponse.data)
      setMembers(membersResponse.data)
    } catch (err) {
      setError(getErrorMessage(err, 'VM-Gruppe konnte nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId])

  const existingMemberKeys = useMemo(
    () => new Set(members.map((m) => `${m.hostName}::${m.vmName}`.toLowerCase())),
    [members],
  )

  const handleRemove = async (member) => {
    if (!window.confirm(`"${member.vmName}" (${member.hostName}) aus der Gruppe entfernen?`)) return

    setError('')
    try {
      await removeVmGroupMember(groupId, member.id)
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'VM konnte nicht entfernt werden.'))
    }
  }

  if (loading) return <p className="notice">Wird geladen…</p>
  if (error && !group) return <p className="notice error">{error}</p>

  return (
    <div>
      <p className="toolbar">
        <Link to="/admin/vm-groups">← Zurück zu VM-Gruppen</Link>
      </p>
      <h3>{group?.name}</h3>
      {error && <p className="notice error">{error}</p>}

      <div className="toolbar">
        <button type="button" onClick={() => setShowAddModal(true)}>
          VMs hinzufügen
        </button>
      </div>

      {members.length === 0 && <p className="notice">Diese Gruppe hat noch keine Mitglieder.</p>}

      <table className="admin-table">
        <thead>
          <tr>
            <th>Host</th>
            <th>VM-Name</th>
            <th>VM-GUID</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {members.map((member) => (
            <tr key={member.id}>
              <td>{member.hostName}</td>
              <td>{member.vmName}</td>
              <td className="muted-cell">{member.vmGuid ?? '—'}</td>
              <td className="row-actions">
                <button type="button" className="secondary" onClick={() => handleRemove(member)}>
                  Entfernen
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {showAddModal && (
        <AddVmsModal
          groupId={groupId}
          groupName={group?.name ?? ''}
          existingMemberKeys={existingMemberKeys}
          onClose={() => setShowAddModal(false)}
          onAdded={() => {
            setShowAddModal(false)
            load()
          }}
        />
      )}
    </div>
  )
}

export default VmGroupDetailPage
