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
          <router-link v-if="auth.role === 'Admin'" to="/users" class="nav-link">Пользователи</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/system" class="nav-link active">Система</router-link>
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
      <h1>Система</h1>

      <!-- === Расчетный период === -->
      <div class="card">
        <div class="card-header">
          <h2>Расчетный период</h2>
          <select v-model="selectedPeriodMonth" class="period-select">
            <option v-for="p in periodOptions" :key="p.value" :value="p.value">{{ p.label }}</option>
          </select>
        </div>

        <div v-if="currentPeriod" class="period-info">
          <div class="info-row">
            <div class="info-item">
              <div class="info-label">Даты периода</div>
              <div class="info-value">{{ periodDates }}</div>
            </div>
            <div class="info-item">
              <div class="info-label">Статус</div>
              <div class="info-value">
                <span class="status-dot" :class="currentPeriod.isClosed ? 'closed' : 'open'"></span>
                {{ currentPeriod.isClosed ? 'Закрыт' : 'Открыт' }}
              </div>
            </div>
            <div class="info-item">
              <div class="info-label">Последнее изменение</div>
              <div class="info-value">{{ currentPeriod.lastModified }}</div>
            </div>
            <div class="info-item">
              <div class="info-label">Изменил</div>
              <div class="info-value">{{ currentPeriod.modifiedBy }}</div>
            </div>
          </div>

          <div class="period-actions">
            <div class="period-hint">
              <div>Закрытие периода заблокирует редактирование отчётов.</div>
              <div>Вскрытие вернёт доступ к изменениям. Все действия записываются в журнал.</div>
            </div>
            <div class="period-buttons">
              <button v-if="!currentPeriod.isClosed" class="btn-primary" @click="onClosePeriod">
                🔒 Закрыть период
              </button>
              <button v-else class="btn-warning" @click="onReopenPeriod">
                ⚠ Вскрыть период
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- === Журнал событий === -->
      <div class="card">
        <div class="card-header">
          <h2>Журнал событий</h2>
          <div class="journal-filters">
            <div class="search-box">
              <span class="search-icon">🔍</span>
              <input v-model="journalSearch" type="text" placeholder="Найти событие" class="search-input" />
            </div>
            <select v-model="journalMonth" class="filter-select">
              <option v-for="m in monthOptions" :key="m.value" :value="m.value">{{ m.label }}</option>
            </select>
            <button class="btn-secondary" @click="onExportJournal">
              ⬇ Выгрузить
            </button>
          </div>
        </div>

        <table class="data-table">
          <thead>
            <tr>
              <th>Дата</th>
              <th>Пользователь</th>
              <th>Действие</th>
              <th>Детали</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="event in filteredJournal" :key="event.id">
              <td>
                <div class="event-date">{{ event.date }}</div>
                <div class="event-time">{{ event.time }}</div>
              </td>
              <td>
                <div class="event-user">{{ event.user }}</div>
                <div class="event-role">{{ event.role }}</div>
              </td>
              <td>{{ event.action }}</td>
              <td>
                <div class="event-detail-main">{{ event.detailMain }}</div>
                <div class="event-detail-sub">{{ event.detailSub }}</div>
              </td>
            </tr>
            <tr v-if="filteredJournal.length === 0">
              <td colspan="4" class="empty-state">События не найдены</td>
            </tr>
          </tbody>
        </table>

        <div class="table-footer">
          <div>Показано {{ filteredJournal.length }} из {{ journalEvents.length }} событий</div>
        </div>
      </div>
    </main>

    <!-- Модалка для вскрытия периода (требует причину) -->
    <div v-if="showReopenModal" class="modal-overlay" @click.self="showReopenModal = false">
      <div class="modal">
        <div class="modal-header">
          <h3>Вскрытие расчетного периода</h3>
          <button class="close-btn" @click="showReopenModal = false">✕</button>
        </div>
        <div class="modal-body">
          <div v-if="reopenError" class="form-error">{{ reopenError }}</div>
          <div class="form-group">
            <label>Причина / номер акта (обязательно по BR23.2)</label>
            <input v-model="reopenReason" type="text" class="form-input" placeholder="Act #12 dated 2026-11-13" />
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn-secondary" @click="showReopenModal = false">Отмена</button>
          <button class="btn-warning" :disabled="!reopenReason.trim()" @click="confirmReopen">
            Вскрыть период
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth.js'
import { closePeriod, reopenPeriod } from '@/api/system.js'

const auth = useAuthStore()

// === MOCK-данные (пока бэкенд не поднят) ===
const USE_MOCK = true

const mockPeriods = [
  {
    id: 'period-1',
    year: 2026,
    month: 10,
    isClosed: false,
    closedAt: null,
    closedBy: null,
    reopenReason: null,
    reopenedByUserId: null,
    lastModified: '22.10.2026, 10:42',
    modifiedBy: 'Олег Попов'
  }
]

const mockJournal = [
  {
    id: '1',
    date: '20.10.2026',
    time: '09:10:46',
    user: 'Олег Попов',
    role: 'Администратор',
    action: 'Создание пользователя',
    detailMain: 'Мария Соколова - Разработчик',
    detailSub: 'Назначена роль «Сотрудник»'
  },
  {
    id: '2',
    date: '01.10.2026',
    time: '09:00:00',
    user: 'Система',
    role: 'Автоматическое действие',
    action: 'Открытие периода',
    detailMain: 'Октябрь 2026 · 01.10.2026 — 31.10.2026',
    detailSub: 'Создан новый расчетный период'
  },
  {
    id: '3',
    date: '01.10.2026',
    time: '08:55:12',
    user: 'Олег Попов',
    role: 'Администратор',
    action: 'Закрытие периода',
    detailMain: 'Сентябрь 2026 · Редактирование заблокировано',
    detailSub: 'Расчетный период завершён'
  }
]

const periods = ref([])
const journalEvents = ref([])

onMounted(() => {
  if (USE_MOCK) {
    periods.value = mockPeriods
    journalEvents.value = mockJournal
  }
  // TODO: когда бэкенд будет готов, добавить GET-эндпоинты для периодов и журнала
})

// === Расчетный период ===
const selectedPeriodMonth = ref('2026-10')
const periodOptions = [
  { value: '2026-10', label: 'Октябрь 2026' },
  { value: '2026-09', label: 'Сентябрь 2026' },
  { value: '2026-08', label: 'Август 2026' }
]

const currentPeriod = computed(() => {
  const [year, month] = selectedPeriodMonth.value.split('-').map(Number)
  return periods.value.find(p => p.year === year && p.month === month)
})

const periodDates = computed(() => {
  if (!currentPeriod.value) return '—'
  const { year, month } = currentPeriod.value
  const lastDay = new Date(year, month, 0).getDate()
  return `01.${String(month).padStart(2, '0')}.${year} — ${String(lastDay).padStart(2, '0')}.${String(month).padStart(2, '0')}.${year}`
})

async function onClosePeriod() {
  if (!currentPeriod.value) return
  if (!confirm(`Закрыть период ${periodOptions.find(p => p.value === selectedPeriodMonth.value)?.label}? Редактирование отчётов будет заблокировано (BR23.1).`)) return
  
  try {
    if (USE_MOCK) {
      currentPeriod.value.isClosed = true
      currentPeriod.value.closedAt = new Date().toISOString()
      currentPeriod.value.lastModified = new Date().toLocaleString('ru-RU')
      currentPeriod.value.modifiedBy = auth.fullName || auth.login
    } else {
      await closePeriod(currentPeriod.value.id)
    }
  } catch (e) {
    alert(e.message || 'Не удалось закрыть период')
  }
}

const showReopenModal = ref(false)
const reopenReason = ref('')
const reopenError = ref('')

function onReopenPeriod() {
  if (!currentPeriod.value) return
  reopenReason.value = ''
  reopenError.value = ''
  showReopenModal.value = true
}

async function confirmReopen() {
  if (!reopenReason.value.trim()) {
    reopenError.value = 'Причина обязательна (BR23.2)'
    return
  }
  if (!confirm(`Вскрыть период? Это действие будет записано в журнал как "Экстренная корректировка" (BR23.2).`)) return
  
  try {
    if (USE_MOCK) {
      currentPeriod.value.isClosed = false
      currentPeriod.value.reopenReason = reopenReason.value
      currentPeriod.value.lastModified = new Date().toLocaleString('ru-RU')
      currentPeriod.value.modifiedBy = auth.fullName || auth.login
    } else {
      await reopenPeriod(currentPeriod.value.id, reopenReason.value)
    }
    showReopenModal.value = false
  } catch (e) {
    reopenError.value = e.message || 'Не удалось вскрыть период'
  }
}

// === Журнал событий ===
const journalSearch = ref('')
const journalMonth = ref('2026-10')
const monthOptions = [
  { value: '2026-10', label: 'Октябрь 2026' },
  { value: '2026-09', label: 'Сентябрь 2026' },
  { value: '2026-08', label: 'Август 2026' }
]

const filteredJournal = computed(() => {
  let list = journalEvents.value
  if (journalSearch.value.trim()) {
    const q = journalSearch.value.toLowerCase()
    list = list.filter(e =>
      e.user.toLowerCase().includes(q) ||
      e.action.toLowerCase().includes(q) ||
      e.detailMain.toLowerCase().includes(q)
    )
  }
  // TODO: фильтрация по месяцу, когда будет реальный API
  return list
})

function onExportJournal() {
  alert('Экспорт журнала событий (пока заглушка)')
}

// === Хедер ===
const avatarLetters = computed(() => {
  const name = auth.fullName || auth.login || 'U'
  return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
})

const roleLabel = computed(() => {
  const roles = { 'Employee': 'Сотрудник', 'Manager': 'Руководитель отдела', 'Admin': 'Администратор' }
  return roles[auth.role] || 'Сотрудник'
})
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
.main-content h1 { font-size: 28px; font-weight: 700; color: #1a1a1a; margin: 0 0 24px 0; }

.card { background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 24px; margin-bottom: 24px; }
.card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
.card-header h2 { font-size: 18px; font-weight: 600; color: #1a1a1a; margin: 0; }

.period-select { padding: 8px 12px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 13px; background: white; outline: none; }
.period-select:focus { border-color: #2563eb; }

.period-info { display: flex; flex-direction: column; gap: 20px; }
.info-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 24px; padding: 16px; background: #f9fafb; border-radius: 8px; }
.info-item { display: flex; flex-direction: column; gap: 6px; }
.info-label { font-size: 12px; color: #6b7280; }
.info-value { font-size: 14px; color: #1a1a1a; font-weight: 500; display: flex; align-items: center; gap: 6px; }
.status-dot { width: 8px; height: 8px; border-radius: 50%; }
.status-dot.open { background: #10b981; }
.status-dot.closed { background: #ef4444; }

.period-actions { display: flex; justify-content: space-between; align-items: flex-end; gap: 24px; }
.period-hint { font-size: 13px; color: #6b7280; line-height: 1.5; }
.period-buttons { display: flex; gap: 12px; }

.btn-primary { display: inline-flex; align-items: center; gap: 8px; padding: 8px 16px; background: #2563eb; color: white; border: none; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer; }
.btn-primary:hover { background: #1d4ed8; }
.btn-warning { display: inline-flex; align-items: center; gap: 8px; padding: 8px 16px; background: #f59e0b; color: white; border: none; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer; }
.btn-warning:hover { background: #d97706; }
.btn-secondary { display: inline-flex; align-items: center; gap: 8px; padding: 8px 16px; background: white; color: #4b5563; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 14px; cursor: pointer; }
.btn-secondary:hover { border-color: #2563eb; color: #2563eb; }

.journal-filters { display: flex; gap: 12px; align-items: center; }
.search-box { position: relative; }
.search-icon { position: absolute; left: 12px; top: 50%; transform: translateY(-50%); font-size: 14px; color: #9ca3af; }
.search-input { padding: 8px 14px 8px 36px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 13px; width: 200px; outline: none; }
.search-input:focus { border-color: #2563eb; }
.filter-select { padding: 8px 12px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 13px; background: white; outline: none; }
.filter-select:focus { border-color: #2563eb; }

.data-table { width: 100%; border-collapse: collapse; }
.data-table th { text-align: left; padding: 12px 16px; font-size: 12px; font-weight: 500; color: #6b7280; background: #f9fafb; border-bottom: 1px solid #e5e7eb; }
.data-table td { padding: 16px; font-size: 14px; color: #1a1a1a; border-bottom: 1px solid #f3f4f6; vertical-align: top; }
.event-date { font-weight: 500; }
.event-time { font-size: 12px; color: #6b7280; }
.event-user { font-weight: 500; }
.event-role { font-size: 12px; color: #6b7280; }
.event-detail-main { font-size: 14px; }
.event-detail-sub { font-size: 12px; color: #6b7280; margin-top: 4px; }
.empty-state { text-align: center; color: #6b7280; padding: 32px 0; }
.table-footer { padding: 16px 0; font-size: 13px; color: #6b7280; }

.modal-overlay { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: white; border-radius: 12px; width: 100%; max-width: 500px; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1); }
.modal-header { display: flex; justify-content: space-between; align-items: center; padding: 20px 24px; border-bottom: 1px solid #e5e7eb; }
.modal-header h3 { margin: 0; font-size: 18px; font-weight: 600; }
.close-btn { background: none; border: none; font-size: 20px; color: #6b7280; cursor: pointer; }
.modal-body { padding: 24px; }
.form-group { display: flex; flex-direction: column; gap: 6px; }
.form-group label { font-size: 13px; font-weight: 500; color: #374151; }
.form-input { padding: 8px 12px; border: 1px solid #e5e7eb; border-radius: 6px; font-size: 14px; outline: none; }
.form-input:focus { border-color: #2563eb; }
.form-error { background: #fee2e2; color: #991b1b; padding: 10px 12px; border-radius: 6px; font-size: 13px; margin-bottom: 16px; }
.modal-footer { display: flex; justify-content: flex-end; gap: 12px; padding: 16px 24px; border-top: 1px solid #e5e7eb; }
</style>