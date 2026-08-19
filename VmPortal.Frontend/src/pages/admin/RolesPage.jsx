import { useEffect, useMemo, useState } from 'react'
import { createRole, deleteRole, getRoles, updateRoleActions } from '../../api/adminApi'
import { getErrorMessage } from '../../api/errors'

function RolesPage() {
  const [roles, setRoles] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [newRoleName, setNewRoleName] = useState('')
  const [cloneFromRoleId, setCloneFromRoleId] = useState('')
  const [creating, setCreating] = useState(false)

  const load = async () => {
    try {
      const response = await getRoles()
      setRoles(response.data)
      setError('')
    } catch (err) {
      setError(getErrorMessage(err, 'Rollen konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  // Der Aktionskatalog wird bewusst nicht über einen eigenen Endpunkt geholt (den gibt es
  // nicht) - FullAdmin ist per RolePermissions.IsAllowed für ALLE Aktionen erlaubt und als
  // System-Rolle nicht editierbar, ihre Actions-Liste ist also verlässlich die vollständige
  // Spaltenliste der Matrix.
  const allActions = useMemo(() => {
    const fullAdmin = roles.find((r) => r.isSystemRole && r.name === 'FullAdmin')
    return fullAdmin?.actions ?? []
  }, [roles])

  const toggleAction = async (role, action) => {
    if (role.isSystemRole) return

    const hasAction = role.actions.includes(action)
    const newActions = hasAction ? role.actions.filter((a) => a !== action) : [...role.actions, action]

    setRoles((prev) => prev.map((r) => (r.id === role.id ? { ...r, actions: newActions } : r)))
    setError('')

    try {
      await updateRoleActions(role.id, newActions)
    } catch (err) {
      // Fehlgeschlagen: lokalen Stand zurückrollen, damit die Checkbox nicht einen Zustand
      // zeigt, der auf dem Server nicht existiert.
      setRoles((prev) => prev.map((r) => (r.id === role.id ? { ...r, actions: role.actions } : r)))
      setError(getErrorMessage(err, 'Aktion konnte nicht gespeichert werden.'))
    }
  }

  const handleCreate = async (event) => {
    event.preventDefault()
    if (!newRoleName.trim()) return

    setCreating(true)
    setError('')
    try {
      const level = roles.length > 0 ? Math.max(...roles.map((r) => r.level)) + 1 : 0
      await createRole(newRoleName.trim(), level, null, cloneFromRoleId || null)
      setNewRoleName('')
      setCloneFromRoleId('')
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'Rolle konnte nicht angelegt werden.'))
    } finally {
      setCreating(false)
    }
  }

  const handleDelete = async (role) => {
    if (!window.confirm(`Custom-Rolle "${role.name}" wirklich löschen?`)) return

    setError('')
    try {
      await deleteRole(role.id)
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'Rolle konnte nicht gelöscht werden.'))
    }
  }

  if (loading) return <p className="notice">Wird geladen…</p>

  return (
    <div>
      {error && <p className="notice error">{error}</p>}

      <form className="inline-form" onSubmit={handleCreate}>
        <div className="field">
          <label htmlFor="new-role-name">Neue Custom-Rolle</label>
          <input
            id="new-role-name"
            type="text"
            value={newRoleName}
            onChange={(e) => setNewRoleName(e.target.value)}
            placeholder="Name"
            required
          />
        </div>
        <div className="field">
          <label htmlFor="clone-from">Rechte klonen von (optional)</label>
          <select
            id="clone-from"
            value={cloneFromRoleId}
            onChange={(e) => setCloneFromRoleId(e.target.value)}
          >
            <option value="">— keine —</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </div>
        <button type="submit" disabled={creating}>
          {creating ? 'Wird angelegt…' : 'Rolle anlegen'}
        </button>
      </form>

      <div className="table-scroll">
        <table className="matrix-table">
          <thead>
            <tr>
              <th className="matrix-role-col">Rolle</th>
              {allActions.map((action) => (
                <th key={action} className="matrix-action-col">
                  {action}
                </th>
              ))}
              <th></th>
            </tr>
          </thead>
          <tbody>
            {roles.map((role) => (
              <tr key={role.id}>
                <td className="matrix-role-col">
                  {role.name}
                  {role.isSystemRole && <span className="badge badge-neutral">System</span>}
                </td>
                {allActions.map((action) => (
                  <td key={action} className="matrix-action-col">
                    <input
                      type="checkbox"
                      checked={role.actions.includes(action)}
                      disabled={role.isSystemRole}
                      onChange={() => toggleAction(role, action)}
                    />
                  </td>
                ))}
                <td>
                  {!role.isSystemRole && (
                    <button type="button" className="secondary" onClick={() => handleDelete(role)}>
                      Löschen
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

export default RolesPage
