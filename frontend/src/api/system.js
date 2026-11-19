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

// §10 строка 34: POST /api/period-closures/{id}/close (AdminOnly, BR23.1)
export const closePeriod = (periodClosureId) =>
  request(`/period-closures/${periodClosureId}/close`, { method: 'POST' })

// §10 строка 35: POST /api/period-closures/{id}/reopen (AdminOnly, BR23.2)
// Тело: { reason } — обязательно
export const reopenPeriod = (periodClosureId, reason) =>
  request(`/period-closures/${periodClosureId}/reopen`, {
    method: 'POST',
    body: JSON.stringify({ reason })
  })

// §10 строка 33: GET /api/audit-logs?entityId= (AdminOnly, BR29)
// Требует entityId — показывает историю конкретной сущности
export const getAuditLogs = (entityId) =>
  request(`/audit-logs?entityId=${entityId}`)