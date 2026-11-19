<template>
  <div class="page">
    <header class="header">
      <div class="header-left">
        <span class="logo">Программный центр</span>
        <nav class="nav">
          <router-link to="/dashboard" class="nav-link">Мой день</router-link>
          <router-link to="/reports" class="nav-link">Мои отчёты</router-link>
          <router-link to="/manager" class="nav-link active">Дашборд руководителя</router-link>
          <router-link to="/team" class="nav-link" active-class="active">Команда</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/users" class="nav-link">Пользователи</router-link>
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
        <h1>Дашборд руководителя</h1>
        <div class="date-selector" @click="showDateMenu = !showDateMenu">
          <span class="calendar-icon">📅</span>
          <span>{{ selectedDateLabel }}</span>
          <span class="chevron">▾</span>
        </div>
      </div>

      <p v-if="apiError" role="alert" style="color:#b91c1c">{{ apiError }}</p>
      <p v-if="loading" style="color:#64748b">Загружаем актуальные данные…</p>
      <button class="btn-download" @click="refreshAttendance">Обновить сводку</button>
      <div class="notifications-card">
        <div class="notif-header">
          <div class="notif-title">
            Уведомления
            <span class="notif-badge">{{ notifications.length }}</span>
          </div>
          <a href="#" class="notif-link" @click.prevent="onConfigureLimits">
            Настроить лимиты
          </a>
        </div>

        <div v-if="notifications.length === 0" class="notif-empty">
          Нет активных уведомлений
        </div>

        <div v-for="n in notifications" :key="n.id" class="notif-item">
          <div class="notif-icon">
            <span class="notif-icon-circle">!</span>
          </div>
          <div class="notif-body">
            <div class="notif-text">{{ n.text }}</div>
            <div class="notif-sub">{{ n.sub }}</div>
          </div>

        </div>
      </div>

      <div class="team-card">
        <div class="team-header">
          <div class="team-title">
            Сотрудники отдела
            <span class="count-badge">{{ filteredEmployees.length }}</span>
          </div>
          <div class="team-actions">
            <div class="search-box">
              <span class="search-icon">🔍</span>
              <input
                v-model="searchQuery"
                type="text"
                placeholder="Найти сотрудника"
                class="search-input"
              />
            </div>
            <button class="btn-download" @click="onDownload">
              <span class="download-icon">⬇</span>
              Скачать отчёт
            </button>
          </div>
        </div>

        <div class="filters">
          <button
            class="filter-tab"
            :class="{ active: statusFilter === 'all' }"
            @click="statusFilter = 'all'"
          >
            Все сотрудники {{ employees.length }}
          </button>
          <button
            class="filter-tab"
            :class="{ active: statusFilter === 'working' }"
            @click="statusFilter = 'working'"
          >
            <span class="dot working"></span>
            Работают {{ workingCount }}
          </button>
          <button
            class="filter-tab"
            :class="{ active: statusFilter === 'onBreak' }"
            @click="statusFilter = 'onBreak'"
          >
            <span class="dot break"></span>
            На перерыве {{ breakCount }}
          </button>
          <button
            class="filter-tab"
            :class="{ active: statusFilter === 'absent' }"
            @click="statusFilter = 'absent'"
          >
            <span class="dot absent"></span>
            Отсутствуют {{ absentCount }}
          </button>
        </div>

        <table class="team-table">
          <thead>
            <tr>
              <th>Сотрудник</th>
              <th>Статус</th>
              <th>Начало дня</th>
              <th>Отработано</th>
              <th>Перерывы</th>
              <th>Действия</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="emp in filteredEmployees" :key="emp.id">
              <td>
                <div class="employee-cell">
                  <div class="employee-avatar">{{ emp.avatar }}</div>
                  <div class="employee-info">
                    <div class="employee-name">{{ emp.fullName }}</div>
                    <div class="employee-role">{{ emp.roleLabel }}</div>
                  </div>
                </div>
              </td>
              <td>
                <div class="status-cell" :class="emp.status">
                  <span class="status-dot"></span>
                  <div>
                    <div class="status-label">{{ emp.statusLabel }}</div>
                    <div v-if="emp.statusSub" class="status-sub">{{ emp.statusSub }}</div>
                  </div>
                </div>
              </td>
              <td class="time-cell">{{ emp.startTime || '—' }}</td>
              <td class="time-cell">{{ emp.workedText || '—' }}</td>
              <td>
                <div class="break-cell" :class="{ over: emp.breakOverLimit }">
                  {{ emp.breakText || '—' }}
                  <span v-if="emp.breakOverLimit" class="break-warning">
                    ⚠ {{ emp.breakWarning }}
                  </span>
                </div>
              </td>
              <td>
                <div class="actions-cell">
                  <button class="action-btn" disabled title="Отчёт: действие не реализовано в прототипе">📄</button>
                  <button class="action-btn" disabled title="Комментарий: действие не реализовано в прототипе">💬</button>
                  <button class="action-btn" disabled title="Отсутствие: действие не реализовано в прототипе">📅</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <div class="table-footer">
          <div class="footer-left">
            Показано {{ filteredEmployees.length }} из {{ employees.length }} сотрудников
          </div>
          <div class="footer-right">
            <span class="info-icon">ℹ</span>
            Дневной лимит перерывов — 4 ч
          </div>
        </div>
      </div>
    </main>

    <div v-if="showDateMenu" class="dropdown-menu" @click.self="showDateMenu = false">
      <div
        v-for="d in dateOptions"
        :key="d.value"
        class="dropdown-item"
        :class="{ active: selectedDate === d.value }"
        @click="selectedDate = d.value; showDateMenu = false"
      >
        {{ d.label }}
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useAuthStore } from '@/stores/auth.js'
import { attendanceSummary, localDate } from '@/api/worktime.js'

const auth = useAuthStore()

const employees = ref([])
const apiError = ref('')
const loading = ref(false)
const notifications = computed(() => employees.value.filter(e => e.breakOverLimit).map(e => ({
  id: e.id, employeeId: e.id,
  text: `${e.fullName}: превышен лимит перерывов`,
  sub: `Сумма перерывов: ${e.breakText}`
})))

const dateOptions = [
  { value: 'today', label: 'Сегодня' },
  { value: 'yesterday', label: 'Вчера' }
]
function formatMinutes(value) {
  const minutes = Math.max(0, Number(value) || 0)
  return `${Math.floor(minutes / 60)} ч ${minutes % 60} мин`
}
async function refreshAttendance() {
  loading.value = true
  apiError.value = ''
  const day = new Date()
  if (selectedDate.value === 'yesterday') day.setDate(day.getDate() - 1)
  try {
    const rows = await attendanceSummary(localDate(day))
    employees.value = rows.map(r => ({
      id: r.employeeId,
      fullName: r.employeeName,
      roleLabel: r.role === 'Manager' ? 'Руководитель' : r.role === 'Admin' ? 'Администратор' : 'Сотрудник',
      avatar: r.employeeName.split(' ').map(x => x[0]).join('').slice(0, 2).toUpperCase(),
      status: r.status,
      statusLabel: ({ working: 'Работает', onBreak: 'На перерыве', absent: 'Отсутствует', finished: 'Завершил', notStarted: 'Не начал' })[r.status] || r.status,
      statusSub: r.absenceReason || '',
      startTime: r.startTime,
      workedText: r.workDayId ? formatMinutes(r.workedMinutes) : null,
      breakText: r.workDayId ? formatMinutes(r.breakMinutes) : null,
      breakOverLimit: Boolean(r.breakLimitExceeded),
      breakWarning: 'Более 4 часов',
    }))
  } catch (error) {
    apiError.value = error.message || 'Не удалось загрузить данные.'
    employees.value = []
  } finally { loading.value = false }
}
const selectedDate = ref('today')
const showDateMenu = ref(false)

const selectedDateLabel = computed(() => {
  return dateOptions.find(d => d.value === selectedDate.value)?.label || ''
})

const searchQuery = ref('')
const statusFilter = ref('all')

const workingCount = computed(() => employees.value.filter(e => e.status === 'working').length)
const breakCount = computed(() => employees.value.filter(e => e.status === 'onBreak').length)
const absentCount = computed(() => employees.value.filter(e => e.status === 'absent').length)

const filteredEmployees = computed(() => {
  let list = employees.value
  if (statusFilter.value !== 'all') {
    list = list.filter(e => e.status === statusFilter.value)
  }
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(e =>
      e.fullName.toLowerCase().includes(q) ||
      e.roleLabel.toLowerCase().includes(q)
    )
  }
  return list
})

function onConfigureLimits() {
  alert('Лимит фиксирован бизнес-правилом BR9: четыре часа. Изменение лимита не входит в этот прототип.')
}
function onDownload() {
  const lines = [['Сотрудник','Статус','Начало','Отработано','Перерывы'],
    ...filteredEmployees.value.map(e => [e.fullName, e.statusLabel, e.startTime || '', e.workedText || '', e.breakText || ''])]
  const csv = '\ufeff' + lines.map(row => row.map(v => '"' + String(v).replace(/^([=+@-])/, "'$1").replaceAll('"','""') + '"').join(';')).join('\r\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = `attendance-${selectedDate.value}-${localDate()}.csv`
  link.click()
  URL.revokeObjectURL(url)
}
watch(selectedDate, refreshAttendance)
onMounted(refreshAttendance)

const avatarLetters = computed(() => {
  const name = auth.fullName || auth.login || 'U'
  return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
})

const roleLabel = computed(() => {
  const roles = { 'Employee': 'Разработчик', 'Manager': 'Руководитель отдела', 'Admin': 'Администратор' }
  return roles[auth.role] || 'Сотрудник'
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  background: #f8f9fa;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 40px;
  height: 64px;
  background: white;
  border-bottom: 1px solid #e5e7eb;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 40px;
}

.logo {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a1a;
}

.nav {
  display: flex;
  gap: 8px;
}

.nav-link {
  text-decoration: none;
  color: #6b7280;
  font-size: 14px;
  padding: 8px 16px;
  border-radius: 6px;
  transition: all 0.2s;
}

.nav-link:hover {
  color: #1a1a1a;
  background: #f3f4f6;
}

.nav-link.active {
  color: #2563eb;
  font-weight: 500;
  border-bottom: 2px solid #2563eb;
  border-radius: 0;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #dbeafe;
  color: #2563eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
}

.user-info {
  display: flex;
  flex-direction: column;
}

.user-name {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
}

.user-role {
  font-size: 12px;
  color: #6b7280;
}

.main-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 32px 40px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 28px;
  font-weight: 700;
  color: #1a1a1a;
  margin: 0;
}

.date-selector {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
  color: #1a1a1a;
  cursor: pointer;
}

.date-selector:hover {
  border-color: #2563eb;
}

.calendar-icon {
  font-size: 16px;
}

.chevron {
  font-size: 12px;
  color: #9ca3af;
}

.notifications-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 24px;
  margin-bottom: 24px;
}

.notif-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.notif-title {
  font-size: 18px;
  font-weight: 600;
  color: #1a1a1a;
  display: flex;
  align-items: center;
  gap: 8px;
}

.notif-badge {
  background: #ef4444;
  color: white;
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
}

.notif-link {
  font-size: 13px;
  color: #2563eb;
  text-decoration: none;
}

.notif-link:hover {
  text-decoration: underline;
}

.notif-empty {
  color: #6b7280;
  font-size: 14px;
  padding: 16px 0;
}

.notif-item {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 16px;
  background: #fef2f2;
  border-left: 3px solid #ef4444;
  border-radius: 8px;
  margin-top: 12px;
}

.notif-icon-circle {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: #fee2e2;
  color: #ef4444;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 14px;
}

.notif-body {
  flex: 1;
}

.notif-text {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
  margin-bottom: 4px;
}

.notif-sub {
  font-size: 13px;
  color: #6b7280;
}

.notif-action {
  font-size: 13px;
  color: #2563eb;
  text-decoration: none;
  white-space: nowrap;
}

.notif-action:hover {
  text-decoration: underline;
}

.team-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  overflow: hidden;
}

.team-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid #e5e7eb;
}

.team-title {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a1a;
  display: flex;
  align-items: center;
  gap: 8px;
}

.count-badge {
  background: #f3f4f6;
  color: #6b7280;
  font-size: 12px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 10px;
}

.team-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.search-box {
  position: relative;
}

.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 14px;
  color: #9ca3af;
}

.search-input {
  padding: 8px 14px 8px 36px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 13px;
  width: 220px;
  outline: none;
  transition: border-color 0.2s;
}

.search-input:focus {
  border-color: #2563eb;
}

.btn-download {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 13px;
  color: #4b5563;
  cursor: pointer;
}

.btn-download:hover {
  border-color: #2563eb;
  color: #2563eb;
}

.filters {
  display: flex;
  gap: 8px;
  padding: 16px 24px;
  border-bottom: 1px solid #f3f4f6;
}

.filter-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 16px;
  font-size: 13px;
  color: #4b5563;
  cursor: pointer;
  transition: all 0.2s;
}

.filter-tab:hover {
  border-color: #2563eb;
  color: #2563eb;
}

.filter-tab.active {
  background: #eff6ff;
  border-color: #2563eb;
  color: #2563eb;
}

.dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.dot.working { background: #10b981; }
.dot.break { background: #f59e0b; }
.dot.absent { background: #9ca3af; }

.team-table {
  width: 100%;
  border-collapse: collapse;
}

.team-table th {
  text-align: left;
  padding: 12px 24px;
  font-size: 12px;
  font-weight: 500;
  color: #6b7280;
  background: #f9fafb;
  border-bottom: 1px solid #e5e7eb;
}

.team-table td {
  padding: 16px 24px;
  font-size: 14px;
  color: #1a1a1a;
  border-bottom: 1px solid #f3f4f6;
  vertical-align: middle;
}

.employee-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.employee-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #e5e7eb;
  color: #4b5563;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 600;
}

.employee-info {
  display: flex;
  flex-direction: column;
}

.employee-name {
  font-weight: 500;
  color: #1a1a1a;
}

.employee-role {
  font-size: 12px;
  color: #6b7280;
}

.status-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-cell.working .status-dot { background: #10b981; }
.status-cell.onBreak .status-dot { background: #f59e0b; }
.status-cell.absent .status-dot { background: #9ca3af; }

.status-label {
  font-weight: 500;
  color: #1a1a1a;
}

.status-sub {
  font-size: 12px;
  color: #6b7280;
}

.time-cell {
  font-variant-numeric: tabular-nums;
  color: #4b5563;
}

.break-cell {
  font-variant-numeric: tabular-nums;
  color: #4b5563;
}

.break-cell.over {
  color: #ef4444;
  font-weight: 500;
}

.break-warning {
  display: block;
  font-size: 11px;
  font-weight: 400;
  margin-top: 2px;
}

.actions-cell {
  display: flex;
  gap: 4px;
}

.action-btn {
  width: 32px;
  height: 32px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.action-btn:hover {
  border-color: #2563eb;
  background: #eff6ff;
}

.table-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  font-size: 13px;
  color: #6b7280;
}

.footer-right {
  display: flex;
  align-items: center;
  gap: 6px;
}

.info-icon {
  color: #9ca3af;
}

.dropdown-menu {
  position: fixed;
  top: 100px;
  right: 40px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  z-index: 100;
  min-width: 220px;
}

.dropdown-item {
  padding: 10px 16px;
  font-size: 14px;
  color: #4b5563;
  cursor: pointer;
}

.dropdown-item:hover {
  background: #f3f4f6;
}

.dropdown-item.active {
  color: #2563eb;
  font-weight: 500;
}
</style>