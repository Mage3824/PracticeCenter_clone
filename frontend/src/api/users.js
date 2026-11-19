const BASE = '/api'

function authHeader() {
  const token = sessionStorage.getItem('accessToken') || localStorage.getItem('accessToken')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function request(path, options = {}) {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json; charset=utf-8', ...authHeader(), ...(options.headers || {}) },
    ...options
  })
  const raw = res.status === 204 ? '' : await res.text()
  let body = null
  try { body = raw ? JSON.parse(raw) : null } catch { body = null }
  if (!res.ok) {
    const err = new Error(body?.error?.message || body?.detail || `HTTP ${res.status}`)
    err.code = body?.error?.code || body?.code || null
    err.status = res.status
    throw err
  }
  return body
}

// §10 строка 20: GET /api/users?name=&includeInactive=
export const getUsers = (name = '', includeInactive = true) => {
  const q = new URLSearchParams()
  if (name) q.set('name', name)
  q.set('includeInactive', String(includeInactive))
  return request(`/users?${q.toString()}`)
}

// §10 строка 22: POST /api/users (AdminOnly). Тело §11.1
export const createUser = ({ login, fullName, password, role, departmentId }) =>
  request('/users', { method: 'POST', body: JSON.stringify({ login, fullName, password, role, departmentId }) })

// §10 строка 23: PUT /api/users/{userId} (AdminOnly). Тело §11.2
export const updateUser = (userId, { fullName, role, departmentId, isActive }) =>
  request(`/users/${userId}`, { method: 'PUT', body: JSON.stringify({ fullName, role, departmentId, isActive }) })

// §10 строка 24: DELETE /api/users/{userId} (AdminOnly)
export const deleteUser = (userId) => request(`/users/${userId}`, { method: 'DELETE' })

// §10 строка 25: POST /api/users/{userId}/deactivate (AdminOnly, BR24)
export const deactivateUser = (userId) => request(`/users/${userId}/deactivate`, { method: 'POST' })

// §10 строка 26: POST /api/users/{userId}/unlock (AdminOnly, BR35)
export const unlockUser = (userId) => request(`/users/${userId}/unlock`, { method: 'POST' })

// §10 строка 27: GET /api/departments (ManagerOrAdmin)
export const getDepartments = () => request('/departments')