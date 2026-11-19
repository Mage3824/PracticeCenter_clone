<template>
  <div class="page">
    <header class="header">
      <div class="header-left">
        <span class="logo">Программный центр</span>
        <nav class="nav">
          <router-link to="/dashboard" class="nav-link">Мой день</router-link>
          <router-link to="/reports" class="nav-link">Мои отчёты</router-link>
          <router-link to="/manager" class="nav-link">Дашборд руководителя</router-link>
          <router-link to="/team" class="nav-link">Команда</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/users" class="nav-link active">Пользователи</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/system" class="nav-link">Система</router-link>
        </nav>
      </div>
      <div class="header-right">
        <div class="avatar">{{ avatarLetters }}</div>
        <div class="user-info">
          <div class="user-name">{{ auth.fullName || auth.login }}</div>
          <div class="user-role">{{ roleLabel }}</div>
        </div>
      </div>
    </header>

    <main class="main-content">
      <div class="page-header">
        <h1>Пользователи</h1>
        <button class="btn-primary" @click="openCreateModal">
          <span>+</span> Добавить сотрудника
        </button>
      </div>

      <!-- Карточки статистики (считаются на фронте из массива users) -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-label">Всего пользователей</div>
          <div class="stat-value">{{ users.length }}</div>
          <div class="stat-sub">в {{ uniqueDepartmentsCount }} подразделениях</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Активные</div>
          <div class="stat-value">{{ activeCount }}</div>
          <div class="stat-sub">есть доступ к сервису</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Заблокированные</div>
          <div class="stat-value">{{ blockedCount }}</div>
          <div class="stat-sub">доступ приостановлен</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Администраторы</div>
          <div class="stat-value">{{ adminCount }}</div>
          <div class="stat-sub">полный доступ</div>
        </div>
      </div>

      <!-- Панель фильтров -->
      <div class="filters-panel">
        <div class="filter-group">
          <label class="filter-label">Поиск сотрудника</label>
          <div class="search-box">
            <span class="search-icon">🔍</span>
            <input v-model="filters.search" type="text" placeholder="Имя или логин" class="search-input" />
          </div>
        </div>
        <div class="filter-group">
          <label class="filter-label">Подразделение</label>
          <select v-model="filters.departmentId" class="filter-select">
            <option value="">Все подразделения</option>
            <option v-for="d in departments" :key="d.id" :value="d.id">{{ d.name }}</option>
          </select>
        </div>
        <div class="filter-group">
          <label class="filter-label">Роль</label>
          <select v-model="filters.role" class="filter-select">
            <option value="">Все роли</option>
            <option value="Admin">Админ</option>
            <option value="Manager">Руководитель</option>
            <option value="Employee">Сотрудник</option>
          </select>
        </div>
        <div class="filter-group">
          <label class="filter-label">Статус</label>
          <select v-model="filters.status" class="filter-select">
            <option value="">Все статусы</option>
            <option value="active">Активен</option>
            <option value="locked">Заблокирован</option>
            <option value="inactive">Деактивирован</option>
          </select>
        </div>
        <button class="reset-link" @click="resetFilters">Сбросить фильтры</button>
      </div>

      <!-- Таблица (колонки только из UserWire: fullName, login, role, departmentId, isActive, locked) -->
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>Сотрудник</th>
              <th>Подразделение</th>
              <th>Роль</th>
              <th>Статус</th>
              <th>Действия</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="u in pagedUsers" :key="u.id">
              <td>
                <div class="employee-cell">
                  <div class="employee-avatar">{{ initials(u.fullName) }}</div>
                  <div class="employee-info">
                    <div class="employee-name">{{ u.fullName }}</div>
                    <div class="employee-login">{{ u.login }}</div>
                  </div>
                </div>
              </td>
              <td>
                <div class="dept-name">{{ departmentName(u.departmentId) }}</div>
                <div v-if="departmentParentName(u.departmentId)" class="dept-sub">{{ departmentParentName(u.departmentId) }}</div>
              </td>
              <td><span class="role-badge" :class="roleClass(u.role)">{{ roleText(u.role) }}</span></td>
              <td>
                <span class="status-badge" :class="statusClass(u)">
                  <span class="status-dot"></span>{{ statusText(u) }}
                </span>
              </td>
              <td>
                <div class="actions-cell">
                  <button class="link-btn" @click="openEditModal(u)">Редактировать</button>
                  <button v-if="u.locked" class="link-btn" @click="onUnlock(u)">Разблокировать</button>
                  <button v-else-if="u.isActive" class="link-btn" @click="onDeactivate(u)">Заблокировать</button>
                  <button v-else class="link-btn" @click="onActivate(u)">Активировать</button>
                  <button class="link-btn danger" @click="onDelete(u)">Удалить</button>
                </div>
              </td>
            </tr>
            <tr v-if="pagedUsers.length === 0">
              <td colspan="5" class="empty-state">Пользователи не найдены</td>
            </tr>
          </tbody>
        </table>

        <div class="table-footer">
          <div>Показано {{ rangeStart }}–{{ rangeEnd }} из {{ filteredUsers.length }} пользователей</div>
          <div class="pager">
            <button class="pager-btn" :disabled="page === 1" @click="page--">‹ Назад</button>
            <button v-for="p in pageCount" :key="p" class="pager-num" :class="{ active: page === p }" @click="page = p">{{ p }}</button>
            <button class="pager-btn" :disabled="page === pageCount" @click="page++">Далее ›</button>
          </div>
        </div>
      </div>
    </main>

    <!-- Модалка создания/редактирования -->
    <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <div class="modal-header">
          <h3>{{ editingUser ? 'Редактирование сотрудника' : 'Добавление сотрудника' }}</h3>
          <button class="close-btn" @click="closeModal">✕</button>
        </div>
        <div class="modal-body">
          <div v-if="formError" class="form-error">{{ formError }}</div>

          <div class="form-group">
            <label>ФИО</label>
            <input v-model="form.fullName" type="text" class="form-input" placeholder="Петрова Елена" />
          </div>

          <div class="form-group">
            <label>Логин</label>
            <input v-model="form.login" type="text" class="form-input" :disabled="!!editingUser" placeholder="e.petrova" />
            <span v-if="editingUser" class="form-hint">Логин изменить нельзя (§11.2)</span>
          </div>

          <!-- Пароль только при создании (§11.1). В PUT его нет (BR34). -->
          <div class="form-group" v-if="!editingUser">
            <label>Пароль</label>
            <input v-model="form.password" type="password" class="form-input" placeholder="••••••••" />
          </div>

          <div class="form-group">
            <label>Роль</label>
            <select v-model="form.role" class="form-input">
              <option value="Employee">Сотрудник</option>
              <option value="Manager">Руководитель</option>
              <option value="Admin">Админ</option>
            </select>
          </div>

          <div class="form-group">
            <label>Подразделение</label>
            <select v-model="form.departmentId" class="form-input">
              <option value="" disabled>Выберите подразделение</option>
              <option v-for="d in departments" :key="d.id" :value="d.id">{{ d.name }}</option>
            </select>
          </div>

          <div class="form-group" v-if="editingUser">
            <label class="checkbox-label">
              <input type="checkbox" v-model="form.isActive" />
              Доступ к сервису (BR24)
            </label>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn-secondary" @click="closeModal">Отмена</button>
          <button class="btn-primary" :disabled="saving" @click="saveUser">
            {{ saving ? 'Сохранение…' : 'Сохранить' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth.js'
import {
  getUsers, createUser, updateUser, deleteUser,
  deactivateUser, unlockUser, getDepartments
} from '@/api/users.js'

const auth = useAuthStore()

// Реальные запросы включены; демомассивы ниже больше не используются.
const USE_MOCK = false

const mockUsers = [
  { id: 'u1', login: 'e.petrova', fullName: 'Елена Петрова', role: 'Admin',
    departmentId: 'd1', isActive: true, failedLoginAttempts: 0, lockedUntil: null, locked: false },
  { id: 'u2', login: 'm.kuznetsova', fullName: 'Мария Кузнецова', role: 'Manager',
    departmentId: 'd2', isActive: true, failedLoginAttempts: 0, lockedUntil: null, locked: false },
  { id: 'u3', login: 'o.novikova', fullName: 'Ольга Новикова', role: 'Employee',
    departmentId: 'd2', isActive: true, failedLoginAttempts: 10,
    lockedUntil: '2026-11-13T23:59:59', locked: true },
  { id: 'u4', login: 'd.volkov', fullName: 'Дмитрий Волков', role: 'Employee',
    departmentId: 'd3', isActive: true, failedLoginAttempts: 0, lockedUntil: null, locked: false },
  { id: 'u5', login: 'i.petrov', fullName: 'Иван Петров', role: 'Employee',
    departmentId: 'd3', isActive: false, failedLoginAttempts: 0, lockedUntil: null, locked: false }
]

const mockDepartments = [
  { id: 'd1', name: 'Администрация', parentId: null },
  { id: 'd2', name: 'Разработчик', parentId: null },
  { id: 'd3', name: 'Бэкенд - разработчик', parentId: 'd2' }
]

const users = ref([])
const departments = ref([])

onMounted(async () => {
  if (USE_MOCK) {
    users.value = mockUsers
    departments.value = mockDepartments
    return
  }
  try {
    ;[users.value, departments.value] = await Promise.all([getUsers('', true), getDepartments()])
  } catch (e) {
    alert('Не удалось загрузить пользователей: ' + (e.message || e))
  }
})

// === Фильтры ===
const filters = ref({ search: '', departmentId: '', role: '', status: '' })
const PAGE_SIZE = 3
const page = ref(1)

function resetFilters() {
  filters.value = { search: '', departmentId: '', role: '', status: '' }
  page.value = 1
}

const filteredUsers = computed(() => {
  let list = users.value
  const f = filters.value
  if (f.search.trim()) {
    const q = f.search.toLowerCase()
    list = list.filter(u => u.fullName.toLowerCase().includes(q) || u.login.toLowerCase().includes(q))
  }
  if (f.departmentId) list = list.filter(u => u.departmentId === f.departmentId)
  if (f.role) list = list.filter(u => u.role === f.role)
  if (f.status === 'active') list = list.filter(u => u.isActive && !u.locked)
  if (f.status === 'locked') list = list.filter(u => u.locked)
  if (f.status === 'inactive') list = list.filter(u => !u.isActive)
  return list
})

const pageCount = computed(() => Math.max(1, Math.ceil(filteredUsers.value.length / PAGE_SIZE)))
const pagedUsers = computed(() => {
  const start = (page.value - 1) * PAGE_SIZE
  return filteredUsers.value.slice(start, start + PAGE_SIZE)
})
const rangeStart = computed(() => filteredUsers.value.length === 0 ? 0 : (page.value - 1) * PAGE_SIZE + 1)
const rangeEnd = computed(() => Math.min(page.value * PAGE_SIZE, filteredUsers.value.length))

// === Статистика ===
const activeCount = computed(() => users.value.filter(u => u.isActive && !u.locked).length)
const blockedCount = computed(() => users.value.filter(u => u.locked || !u.isActive).length)
const adminCount = computed(() => users.value.filter(u => u.role === 'Admin').length)
const uniqueDepartmentsCount = computed(() => new Set(users.value.map(u => u.departmentId)).size)

// === Справочники (§4) ===
const ROLE_LABELS = { Admin: 'Админ', Manager: 'Руководитель', Employee: 'Сотрудник' }
function roleText(r) {
  if (!ROLE_LABELS[r]) throw new Error(`Неизвестный токен роли: ${r}`)
  return ROLE_LABELS[r]
}
const roleClass = r => ({ Admin: 'admin', Manager: 'manager', Employee: 'employee' }[r])

function statusClass(u) {
  if (u.locked) return 'locked'
  if (!u.isActive) return 'inactive'
  return 'active'
}
function statusText(u) {
  if (u.locked) return 'Заблокирован'
  if (!u.isActive) return 'Деактивирован'
  return 'Активен'
}

const departmentName = id => departments.value.find(d => d.id === id)?.name || '—'
const departmentParentName = id => {
  const d = departments.value.find(x => x.id === id)
  return d?.parentId ? departments.value.find(p => p.id === d.parentId)?.name || '' : ''
}
const initials = name => name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)

// === Модалка ===
const showModal = ref(false)
const editingUser = ref(null)
const saving = ref(false)
const formError = ref('')
const emptyForm = () => ({ fullName: '', login: '', password: '', role: 'Employee', departmentId: '', isActive: true })
const form = ref(emptyForm())

function openCreateModal() {
  editingUser.value = null
  form.value = emptyForm()
  formError.value = ''
  showModal.value = true
}
function openEditModal(u) {
  editingUser.value = u
  form.value = { fullName: u.fullName, login: u.login, password: '', role: u.role,
    departmentId: u.departmentId, isActive: u.isActive }
  formError.value = ''
  showModal.value = true
}
function closeModal() { showModal.value = false }

const ERROR_TEXTS = {
  USER_LOGIN_TAKEN: 'Этот логин уже занят',
  FIELD_MISSING: 'Заполните все обязательные поля',
  DEPARTMENT_ID_REQUIRED: 'Выберите подразделение',
  ROLE_INVALID: 'Недопустимая роль',
  BR20: 'Это действие запрещено для вашей собственной учётной записи'
}

async function saveUser() {
  formError.value = ''
  if (!form.value.fullName || !form.value.departmentId) {
    formError.value = 'Заполните ФИО и подразделение'
    return
  }
  if (!editingUser.value && !form.value.password) {
    formError.value = 'При создании пароль обязателен (§11.1)'
    return
  }
  saving.value = true
  try {
    if (USE_MOCK) {
      if (editingUser.value) Object.assign(editingUser.value, {
        fullName: form.value.fullName, role: form.value.role,
        departmentId: form.value.departmentId, isActive: form.value.isActive })
      else users.value.push({ id: 'u' + Date.now(), ...form.value,
        failedLoginAttempts: 0, lockedUntil: null, locked: false })
    } else if (editingUser.value) {
      const updated = await updateUser(editingUser.value.id, {
        fullName: form.value.fullName, role: form.value.role,
        departmentId: form.value.departmentId, isActive: form.value.isActive })
      Object.assign(editingUser.value, updated)
    } else {
      const created = await createUser({ login: form.value.login, fullName: form.value.fullName,
        password: form.value.password, role: form.value.role, departmentId: form.value.departmentId })
      users.value.push(created)
    }
    closeModal()
  } catch (e) {
    formError.value = ERROR_TEXTS[e.code] || e.message || 'Не удалось сохранить'
  } finally {
    saving.value = false
  }
}

async function onDeactivate(u) {
  if (!confirm(`Заблокировать доступ ${u.fullName}? История сохранится (BR24).`)) return
  try {
    if (USE_MOCK) u.isActive = false
    else Object.assign(u, await deactivateUser(u.id))
  } catch (e) { alert(ERROR_TEXTS[e.code] || e.message) }
}

async function onActivate(u) {
  if (!confirm(`Активировать ${u.fullName}?`)) return
  try {
    if (USE_MOCK) u.isActive = true
    else Object.assign(u, await updateUser(u.id, { isActive: true }))
  } catch (e) { alert(ERROR_TEXTS[e.code] || e.message) }
}

async function onUnlock(u) {
  if (!confirm(`Разблокировать ${u.fullName}? Счётчик попыток обнулится (BR35).`)) return
  try {
    if (USE_MOCK) { u.locked = false; u.failedLoginAttempts = 0; u.lockedUntil = null }
    else Object.assign(u, await unlockUser(u.id))
  } catch (e) { alert(ERROR_TEXTS[e.code] || e.message) }
}

async function onDelete(u) {
  if (!confirm(`Удалить ${u.fullName}? Это действие необратимо.`)) return
  try {
    if (USE_MOCK) users.value = users.value.filter(x => x.id !== u.id)
    else { await deleteUser(u.id); users.value = users.value.filter(x => x.id !== u.id) }
  } catch (e) { alert(ERROR_TEXTS[e.code] || e.message) }
}

const avatarLetters = computed(() => initials(auth.fullName || auth.login || 'U'))
const roleLabel = computed(() =>
  ({ Employee: 'Сотрудник', Manager: 'Руководитель отдела', Admin: 'Администратор' }[auth.role] || 'Сотрудник'))
</script>

<style scoped>
.page { min-height: 100vh; background: #f8f9fa; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
.header { display: flex; justify-content: space-between; align-items: center; padding: 0 40px; height: 64px; background: white; border-bottom: 1px solid #e5e7eb; }
.header-left { display: flex; align-items: center; gap: 40px; }
.logo { font-size: 16px; font-weight: 600; color: #1a1a1a; }
.nav { display: flex; gap: 8px; }
.nav-link { text-decoration: none; color: #6b7280; font-size: 14px; padding: 8px 16px; border-radius: 6px; transition: all 0.2s; }
.nav-link:hover { color: #1a1a1a; background: #f3f4f6; }
.nav-link.active { color: #2563eb; font-weight: 500; border-bottom: 2px solid #2563eb; border-radius: 0; }
.header-right { display: flex; align-items: center; gap: 12px; }
.avatar { width: 36px; height: 36px; border-radius: 50%; background: #dbeafe; color: #2563eb; display: flex; align-items: center; justify-content: center; font-size: 13px; font-weight: 600; }
.user-info { display: flex; flex-direction: column; }
.user-name { font-size: 14px; font-weight: 500; color: #1a1a1a; }
.user-role { font-size: 12px; color: #6b7280; }
.main-content { max-width: 1200px; margin: 0 auto; padding: 32px 40px; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.page-header h1 { font-size: 28px; font-weight: 700; color: #1a1a1a; margin: 0; }

.stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 24px; }
.stat-card { background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 20px 24px; }
.stat-label { font-size: 13px; color: #6b7280; margin-bottom: 8px; }
.stat-value { font-size: 28px; font-weight: 700; color: #1a1a1a; }
.stat-sub { font-size: 12px; color: #9ca3af; margin-top: 4px; }

.filters-panel { display: flex; gap: 24px; align-items: flex-end; background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 20px 24px; margin-bottom: 24px; flex-wrap: wrap; }
.filter-group { display: flex; flex-direction: column; gap: 8px; }
.filter-label { font-size: 13px; color: #4b5563; }
.filter-select { padding: 8px 12px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 13px; min-width: 180px; background: white; outline: none; color: #1a1a1a; }
.filter-select:focus { border-color: #2563eb; }
.reset-link { margin-left: auto; background: none; border: none; color: #2563eb; font-size: 13px; cursor: pointer; padding: 8px 0; }
.reset-link:hover { text-decoration: underline; }
.search-box { position: relative; }
.search-icon { position: absolute; left: 12px; top: 50%; transform: translateY(-50%); font-size: 14px; color: #9ca3af; }
.search-input { padding: 8px 14px 8px 36px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 13px; width: 260px; outline: none; }
.search-input:focus { border-color: #2563eb; }

.card { background: white; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; }
.data-table th { text-align: left; padding: 12px 24px; font-size: 12px; font-weight: 500; color: #6b7280; background: #f9fafb; border-bottom: 1px solid #e5e7eb; }
.data-table td { padding: 16px 24px; font-size: 14px; color: #1a1a1a; border-bottom: 1px solid #f3f4f6; vertical-align: middle; }
.employee-cell { display: flex; align-items: center; gap: 12px; }
.employee-avatar { width: 36px; height: 36px; border-radius: 50%; background: #dbeafe; color: #2563eb; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 600; }
.employee-name { font-weight: 500; }
.employee-login { font-size: 12px; color: #6b7280; font-family: monospace; }
.dept-name { font-size: 14px; }
.dept-sub { font-size: 12px; color: #9ca3af; }
.role-badge { display: inline-block; padding: 4px 10px; border-radius: 12px; font-size: 12px; font-weight: 500; }
.role-badge.admin { background: #fee2e2; color: #dc2626; }
.role-badge.manager { background: #dbeafe; color: #2563eb; }
.role-badge.employee { background: #f3f4f6; color: #4b5563; }
.status-badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 12px; font-size: 12px; font-weight: 500; }
.status-dot { width: 6px; height: 6px; border-radius: 50%; }
.status-badge.active { background: #dcfce7; color: #166534; }
.status-badge.active .status-dot { background: #10b981; }
.status-badge.locked { background: #fee2e2; color: #991b1b; }
.status-badge.locked .status-dot { background: #ef4444; }
.status-badge.inactive { background: #f3f4f6; color: #4b5563; }
.status-badge.inactive .status-dot { background: #9ca3af; }
.actions-cell { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; }
.link-btn { background: none; border: none; color: #2563eb; font-size: 13px; cursor: pointer; padding: 0; }
.link-btn:hover { text-decoration: underline; }
.link-btn.danger { color: #dc2626; }
.table-footer { display: flex; justify-content: space-between; align-items: center; padding: 16px 24px; font-size: 13px; color: #6b7280; }
.pager { display: flex; gap: 6px; align-items: center; }
.pager-btn, .pager-num { min-width: 32px; height: 32px; padding: 0 10px; border: 1px solid #e5e7eb; border-radius: 6px; background: white; font-size: 13px; color: #4b5563; cursor: pointer; }
.pager-btn:disabled { color: #d1d5db; cursor: not-allowed; }
.pager-num.active { background: #dbeafe; border-color: #2563eb; color: #2563eb; font-weight: 600; }
.empty-state { text-align: center; color: #6b7280; padding: 32px 0; }

.btn-primary { display: inline-flex; align-items: center; gap: 8px; padding: 8px 16px; background: #2563eb; color: white; border: none; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer; }
.btn-primary:hover { background: #1d4ed8; }
.btn-secondary { display: inline-flex; align-items: center; gap: 8px; padding: 8px 16px; background: white; color: #4b5563; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 14px; cursor: pointer; }
.btn-secondary:hover { border-color: #2563eb; color: #2563eb; }

.modal-overlay { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: white; border-radius: 12px; width: 100%; max-width: 500px; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1); }
.modal-header { display: flex; justify-content: space-between; align-items: center; padding: 20px 24px; border-bottom: 1px solid #e5e7eb; }
.modal-header h3 { margin: 0; font-size: 18px; font-weight: 600; }
.close-btn { background: none; border: none; font-size: 20px; color: #6b7280; cursor: pointer; }
.modal-body { padding: 24px; display: flex; flex-direction: column; gap: 16px; }
.form-group { display: flex; flex-direction: column; gap: 6px; }
.form-group label { font-size: 13px; font-weight: 500; color: #374151; }
.form-input { padding: 8px 12px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 14px; outline: none; }
.form-input:focus { border-color: #2563eb; }
.form-input:disabled { background: #f3f4f6; color: #9ca3af; cursor: not-allowed; }
.form-hint { font-size: 12px; color: #6b7280; }
.form-error { background: #fee2e2; color: #991b1b; padding: 10px 12px; border-radius: 6px; font-size: 13px; }
.checkbox-label { display: flex; align-items: center; gap: 8px; font-size: 13px; color: #4b5563; cursor: pointer; }
.modal-footer { display: flex; justify-content: flex-end; gap: 12px; padding: 16px 24px; border-top: 1px solid #e5e7eb; }
</style>