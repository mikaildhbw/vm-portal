import axios from 'axios'

// Zentrale Axios-Instanz. withCredentials sorgt dafür, dass das httpOnly-JWT-Cookie
// bei jedem Request automatisch mitgeschickt wird — es wird bewusst nicht im
// Frontend-State oder localStorage gehalten.
const client = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

// Bei einer 401-Antwort ist die Session abgelaufen oder nicht vorhanden:
// zurück zur Login-Seite, ohne dass jede Komponente das selbst behandeln muss.
client.interceptors.response.use(
  (response) => response,
  (error) => {
    const isLoginRequest = error.config?.url?.includes('/auth/login')
    if (error.response?.status === 401 && !isLoginRequest) {
      if (window.location.pathname !== '/') {
        window.location.assign('/')
      }
    }
    return Promise.reject(error)
  },
)

export default client
