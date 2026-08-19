import { Navigate, Route, Routes } from 'react-router-dom'
import Login from './pages/Login'
import VmList from './pages/VmList'
import VmDetail from './pages/VmDetail'
import AdminLayout from './pages/admin/AdminLayout'
import RolesPage from './pages/admin/RolesPage'
import VmGroupsPage from './pages/admin/VmGroupsPage'
import VmGroupDetailPage from './pages/admin/VmGroupDetailPage'
import PermissionsPage from './pages/admin/PermissionsPage'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Login />} />
      <Route path="/vms" element={<VmList />} />
      <Route path="/vms/:id" element={<VmDetail />} />
      <Route path="/admin" element={<AdminLayout />}>
        <Route index element={<Navigate to="roles" replace />} />
        <Route path="roles" element={<RolesPage />} />
        <Route path="vm-groups" element={<VmGroupsPage />} />
        <Route path="vm-groups/:groupId" element={<VmGroupDetailPage />} />
        <Route path="permissions" element={<PermissionsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
