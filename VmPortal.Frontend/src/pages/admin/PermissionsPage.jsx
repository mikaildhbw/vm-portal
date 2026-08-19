import { useEffect, useMemo, useState } from 'react'
import AdGroupPicker from '../../components/AdGroupPicker'
import { createPermission, deletePermission, getPermissions, getRoles, getVmGroups } from '../../api/adminApi'
import { getErrorMessage } from '../../api/errors'

function PermissionsPage() {
  const [permissions, setPermissions] = useState([])
  const [vmGroups, setVmGroups] = useState([])
  const [roles, setRoles] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [selectedUserGroupId, setSelectedUserGroupId] = useState(null)
  const [selectedVmGroupId, setSelectedVmGroupId] = useState('')
  const [selectedRoleId, setSelectedRoleId] = useState('')
  const [saving, setSaving] = useState(false)

  const load = async () => {
    setError('')
    try {
      const [permissionsResponse, vmGroupsResponse, rolesResponse] = await Promise.all([
        getPermissions(),
        getVmGroups(),
        getRoles(),
      ])
      setPermissions(permissionsResponse.data)
      setVmGroups(vmGroupsResponse.data)
      setRoles(rolesResponse.data)
    } catch (err) {
      setError(getErrorMessage(err, 'Zuordnungen konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  // Es gibt keinen Endpunkt, der alle bekannten UserGroups (AD-Gruppen mit einer Id in der
  // Autorisierungs-DB) unabhängig von bestehenden Zuordnungen auflistet - die einzige
  // verlässliche Quelle sind die UserGroupId/-Name-Paare, die bereits in GroupPermissions
  // auftauchen. Siehe AdGroupPicker und Abschlussbericht.
  const knownUserGroups = useMemo(() => {
    const map = new Map()
    permissions.forEach((p) => map.set(p.userGroupId, { id: p.userGroupId, name: p.userGroupName }))
    return [...map.values()]
  }, [permissions])

  const canSubmit = selectedUserGroupId && selectedVmGroupId && selectedRoleId

  const handleCreate = async (event) => {
    event.preventDefault()
    if (!canSubmit) return

    setSaving(true)
    setError('')
    try {
      await createPermission(Number(selectedVmGroupId), Number(selectedUserGroupId), Number(selectedRoleId))
      setSelectedUserGroupId(null)
      setSelectedVmGroupId('')
      setSelectedRoleId('')
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'Zuordnung konnte nicht angelegt werden.'))
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (permission) => {
    if (
      !window.confirm(
        `Zuordnung "${permission.userGroupName}" × "${permission.vmGroupName}" × "${permission.roleName}" löschen?`,
      )
    )
      return

    setError('')
    try {
      await deletePermission(permission.id)
      await load()
    } catch (err) {
      setError(getErrorMessage(err, 'Zuordnung konnte nicht gelöscht werden.'))
    }
  }

  if (loading) return <p className="notice">Wird geladen…</p>

  return (
    <div>
      {error && <p className="notice error">{error}</p>}

      <form className="inline-form" onSubmit={handleCreate}>
        <div className="field">
          <label>AD-Gruppe</label>
          <AdGroupPicker
            knownUserGroups={knownUserGroups}
            selectedUserGroupId={selectedUserGroupId}
            onSelect={setSelectedUserGroupId}
          />
        </div>
        <div className="field">
          <label htmlFor="vm-group-select">VM-Gruppe</label>
          <select
            id="vm-group-select"
            value={selectedVmGroupId}
            onChange={(e) => setSelectedVmGroupId(e.target.value)}
          >
            <option value="">— wählen —</option>
            {vmGroups.map((g) => (
              <option key={g.id} value={g.id}>
                {g.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="role-select">Rolle</label>
          <select id="role-select" value={selectedRoleId} onChange={(e) => setSelectedRoleId(e.target.value)}>
            <option value="">— wählen —</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </div>
        <button type="submit" disabled={!canSubmit || saving}>
          {saving ? 'Wird gespeichert…' : 'Zuordnung anlegen'}
        </button>
      </form>

      {permissions.length === 0 && <p className="notice">Noch keine Zuordnungen vorhanden.</p>}

      <table className="admin-table">
        <thead>
          <tr>
            <th>AD-Gruppe</th>
            <th>VM-Gruppe</th>
            <th>Rolle</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {permissions.map((p) => (
            <tr key={p.id}>
              <td>{p.userGroupName}</td>
              <td>{p.vmGroupName}</td>
              <td>{p.roleName}</td>
              <td className="row-actions">
                <button type="button" className="secondary" onClick={() => handleDelete(p)}>
                  Löschen
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default PermissionsPage
