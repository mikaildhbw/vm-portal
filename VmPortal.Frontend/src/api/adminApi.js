import client from './client'

// Rollen (Rolle x Aktion-Matrix)
export const getRoles = () => client.get('/admin/roles')

export const createRole = (name, level, actions, cloneFromRoleId) =>
  client.post('/admin/roles', { name, level, actions, cloneFromRoleId })

export const updateRoleActions = (id, actions) => client.put(`/admin/roles/${id}`, { actions })

export const deleteRole = (id) => client.delete(`/admin/roles/${id}`)

// VM-Gruppen
export const getVmGroups = () => client.get('/admin/vm-groups')

export const getVmGroup = (id) => client.get(`/admin/vm-groups/${id}`)

export const createVmGroup = (name) => client.post('/admin/vm-groups', { name })

export const renameVmGroup = (id, name) => client.put(`/admin/vm-groups/${id}`, { name })

export const deleteVmGroup = (id) => client.delete(`/admin/vm-groups/${id}`)

// VM-Gruppen-Mitgliedschaft
export const getVmGroupMembers = (groupId) => client.get(`/admin/vm-groups/${groupId}/members`)

export const addVmGroupMembers = (groupId, vms) =>
  client.post(`/admin/vm-groups/${groupId}/members`, { vms })

export const removeVmGroupMember = (groupId, memberId) =>
  client.delete(`/admin/vm-groups/${groupId}/members/${memberId}`)

// VM-Discovery (read-only Vorschau des Hypervisor-Inventars)
export const discoverVms = () => client.get('/admin/discover-vms')

// AD-Gruppensuche
export const searchAdGroups = (search) => client.get('/admin/ad-groups', { params: { search } })

// Zuordnungen (GroupPermissions: AD-Gruppe x VM-Gruppe x Rolle)
export const getPermissions = () => client.get('/admin/permissions')

export const createPermission = (vmGroupId, userGroupId, roleId) =>
  client.post('/admin/permissions', { vmGroupId, userGroupId, roleId })

export const deletePermission = (id) => client.delete(`/admin/permissions/${id}`)

// Hyper-V-Hosts - dient hier v. a. als leichtgewichtiger Probe-Endpunkt für den
// FullAdmin-Zugriffscheck im Frontend (siehe hooks/useIsFullAdmin.js), da das httpOnly-JWT
// clientseitig nicht lesbar ist und es keinen eigenen "wer bin ich"-Endpunkt gibt.
export const getServers = () => client.get('/admin/servers')
