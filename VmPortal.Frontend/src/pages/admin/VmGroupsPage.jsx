import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { createVmGroup, deleteVmGroup, getVmGroups, renameVmGroup } from '../../api/adminApi'
import { getErrorMessage } from '../../api/errors'

function VmGroupsPage() {
  const [groups, setGroups] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [newGroupName, setNewGroupName] = useState('')
  const [creating, setCreating] = useState(false)
  const [editingId, setEditingId] = useState(null)
  const [editingName, setEditingName] = useState('')

  const load = async () => {
    try {
      const response = await getVmGroups()
      setGroups(response.data)
      setError('')
    } catch (err) {
      setError(getErrorMessage(err, 'VM-Gruppen konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  const handleCreate = async (event) => {
    event.preventDefault()
    if (!newGroupName.trim()) return

    setCreating(true)
    setError('')
    try {
      await createVmGroup(newGroupName.trim())
      setNewGroupName('')
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'VM-Gruppe konnte nicht angelegt werden.'))
    } finally {
      setCreating(false)
    }
  }

  const startRename = (group) => {
    setEditingId(group.id)
    setEditingName(group.name)
  }

  const submitRename = async (group) => {
    if (!editingName.trim() || editingName === group.name) {
      setEditingId(null)
      return
    }

    setError('')
    try {
      await renameVmGroup(group.id, editingName.trim())
      setEditingId(null)
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'VM-Gruppe konnte nicht umbenannt werden.'))
    }
  }

  const handleDelete = async (group) => {
    if (!window.confirm(`VM-Gruppe "${group.name}" wirklich löschen? Ihre VMs werden dabei gruppenlos, nicht gelöscht.`))
      return

    setError('')
    try {
      await deleteVmGroup(group.id)
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'VM-Gruppe konnte nicht gelöscht werden.'))
    }
  }

  if (loading) return <p className="notice">Wird geladen…</p>

  return (
    <div>
      {error && <p className="notice error">{error}</p>}

      <form className="inline-form" onSubmit={handleCreate}>
        <div className="field">
          <label htmlFor="new-group-name">Neue VM-Gruppe</label>
          <input
            id="new-group-name"
            type="text"
            value={newGroupName}
            onChange={(e) => setNewGroupName(e.target.value)}
            placeholder="Name"
            required
          />
        </div>
        <button type="submit" disabled={creating}>
          {creating ? 'Wird angelegt…' : 'Gruppe anlegen'}
        </button>
      </form>

      {groups.length === 0 && <p className="notice">Noch keine VM-Gruppen vorhanden.</p>}

      <table className="admin-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Mitglieder</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {groups.map((group) => (
            <tr key={group.id}>
              <td>
                {editingId === group.id ? (
                  <input
                    type="text"
                    value={editingName}
                    onChange={(e) => setEditingName(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && submitRename(group)}
                    autoFocus
                  />
                ) : (
                  <Link to={`/admin/vm-groups/${group.id}`}>{group.name}</Link>
                )}
              </td>
              <td>{group.virtualMachineCount}</td>
              <td className="row-actions">
                {editingId === group.id ? (
                  <>
                    <button type="button" onClick={() => submitRename(group)}>
                      Speichern
                    </button>
                    <button type="button" className="secondary" onClick={() => setEditingId(null)}>
                      Abbrechen
                    </button>
                  </>
                ) : (
                  <>
                    <button type="button" className="secondary" onClick={() => startRename(group)}>
                      Umbenennen
                    </button>
                    <button type="button" className="secondary" onClick={() => handleDelete(group)}>
                      Löschen
                    </button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default VmGroupsPage
