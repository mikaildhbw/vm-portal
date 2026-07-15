import { Navigate, Route, Routes } from 'react-router-dom'
import Login from './pages/Login'
import VmList from './pages/VmList'
import VmDetail from './pages/VmDetail'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Login />} />
      <Route path="/vms" element={<VmList />} />
      <Route path="/vms/:id" element={<VmDetail />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
