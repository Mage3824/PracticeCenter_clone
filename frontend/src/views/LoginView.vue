<template>
  <div class="page">
    <!-- ШАПКА -->
    <header class="header">
      <div class="header-left">Программный центр</div>
      <div class="header-right">
        <span class="help-icon">ⓘ</span>
        <span>Нужна помощь?</span>
      </div>
    </header>

    <!-- ЦЕНТР СТРАНИЦЫ -->
    <main class="main-content">
      <div class="login-card">
        <h1 class="title">С возвращением</h1>
        <p class="subtitle">Войдите в свое рабочее пространство</p>

        <form @submit.prevent="handleLogin">
          <!-- Поле логина -->
          <div class="form-group">
            <label class="field-label">Email/Имя пользователя</label>
            <input
              v-model="login"
              type="text"
              placeholder="Введите свой адрес электронной почты или имя пользователя"
              :disabled="isLocked || loading"
              required
              class="field-input"
            />
          </div>

          <!-- Поле пароля с иконкой глаза -->
          <div class="form-group">
            <label class="field-label">Пароль</label>
            <div class="password-wrapper">
              <input
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                placeholder="Введите свой пароль"
                :disabled="isLocked || loading"
                required
                class="field-input password-input"
              />
              <button
                type="button"
                class="eye-button"
                @click="showPassword = !showPassword"
                :disabled="isLocked || loading"
                tabindex="-1"
              >
                {{ showPassword ? '🙈' : '👁' }}
              </button>
            </div>
          </div>

          <!-- Запомнить меня + Забыли пароль -->
          <div class="form-options">
            <label class="checkbox-label">
              <input type="checkbox" v-model="rememberMe" />
              <span>Запомнить меня</span>
            </label>
            <a href="#" class="forgot-link" @click.prevent="onForgotPassword">
              Забыли пароль
            </a>
          </div>

          <!-- Заглушка CAPTCHA (BR36) -->
          <div v-if="requiresCaptcha" class="captcha-block">
            <div class="captcha-box">
              <input type="checkbox" disabled />
              <span>Я не робот</span>
              <div class="captcha-logo">
                <span class="captcha-icon">🔄</span>
                <div>
                  <div class="captcha-brand">reCAPTCHA</div>
                  <div class="captcha-links">
                    <span>Конфиденциальность</span> · <span>Условия использования</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Блокировка с обратным отсчётом (BR35) -->
          <div v-if="isLocked && countdown > 0" class="lock-message">
            🔒 Аккаунт заблокирован. Попробуйте через {{ countdownText }}
          </div>

          <!-- Кнопка "Войти" -->
          <button
            type="submit"
            :disabled="loading || isLocked || requiresCaptcha"
            class="submit-button"
          >
            {{ loading ? 'Входим...' : 'Войти' }}
          </button>
        </form>

        <!-- Сообщение об ошибке -->
        <p v-if="errorMessage" class="error-message">{{ errorMessage }}</p>
      </div>
    </main>
  </div>
</template>

<script setup>
import { ref, computed, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth.js'

const router = useRouter()
const auth = useAuthStore()

// Переменные формы
const login = ref('')
const password = ref('')
const showPassword = ref(false)
const rememberMe = ref(false)
const loading = ref(false)
const errorMessage = ref('')

// Переменные блокировки и CAPTCHA
const requiresCaptcha = ref(false)
const isLocked = ref(false)
const lockedUntilTime = ref(null)
const now = ref(Date.now())
let timer = null

// Обратный отсчёт
const countdown = computed(() => {
  if (!lockedUntilTime.value) return 0
  const diff = new Date(lockedUntilTime.value).getTime() - now.value
  return Math.max(0, Math.floor(diff / 1000))
})

const countdownText = computed(() => {
  const m = Math.floor(countdown.value / 60)
  const s = countdown.value % 60
  return `${m}:${s.toString().padStart(2, '0')}`
})

// Таймер разблокировки
function startLockTimer() {
  if (timer) clearInterval(timer)
  timer = setInterval(() => {
    now.value = Date.now()
    if (countdown.value <= 0) {
      clearInterval(timer)
      isLocked.value = false
      lockedUntilTime.value = null
    }
  }, 1000)
}

// Заглушка для "Забыли пароль"
function onForgotPassword() {
  alert('Функция восстановления пароля будет добавлена на Этапе 2')
}

// Главная функция входа
async function handleLogin() {
  errorMessage.value = ''
  loading.value = true

  const result = await auth.tryLogin(login.value, password.value, rememberMe.value)

  if (result.success) {
    router.push('/dashboard')
  } else {
    errorMessage.value = result.message || 'Неверный логин или пароль'
    requiresCaptcha.value = Boolean(result.requiresCaptcha)
    isLocked.value = Boolean(result.isLocked)

    if (result.isLocked && result.lockedUntil) {
      lockedUntilTime.value = result.lockedUntil
      startLockTimer()
    }
  }

  loading.value = false
}

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<style scoped>
/* ===== ОСНОВА СТРАНИЦЫ ===== */
.page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f8f9fa;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
}

/* ===== ШАПКА ===== */
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 40px;
  background: #f8f9fa;
}

.header-left {
  font-size: 16px;
  color: #1a1a1a;
  font-weight: 400;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  color: #555;
  cursor: pointer;
}

.help-icon {
  font-size: 16px;
  color: #555;
}

/* ===== ЦЕНТР ===== */
.main-content {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 40px 20px;
}

/* ===== КАРТОЧКА ЛОГИНА ===== */
.login-card {
  background: white;
  padding: 48px;
  border-radius: 8px;
  width: 100%;
  max-width: 480px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
}

.title {
  font-size: 32px;
  font-weight: 700;
  color: #1a1a1a;
  margin: 0 0 8px;
}

.subtitle {
  font-size: 14px;
  color: #6b7280;
  margin: 0 0 32px;
}

/* ===== ПОЛЯ ФОРМЫ ===== */
.form-group {
  margin-bottom: 20px;
}

.field-label {
  display: block;
  font-size: 14px;
  font-weight: 600;
  color: #1a1a1a;
  margin-bottom: 8px;
}

.field-input {
  width: 100%;
  padding: 12px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 14px;
  color: #1a1a1a;
  box-sizing: border-box;
  transition: border-color 0.2s;
  background: white;
}

.field-input:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.field-input:disabled {
  background: #f3f4f6;
  cursor: not-allowed;
}

.field-input::placeholder {
  color: #9ca3af;
}

/* ===== ПОЛЕ ПАРОЛЯ С ИКОНКОЙ ===== */
.password-wrapper {
  position: relative;
}

.password-input {
  padding-right: 44px;
}

.eye-button {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  cursor: pointer;
  font-size: 18px;
  padding: 4px;
  color: #6b7280;
}

.eye-button:hover:not(:disabled) {
  color: #2563eb;
}

.eye-button:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

/* ===== ОПЦИИ (Запомнить + Забыли) ===== */
.form-options {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  font-size: 13px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 6px;
  color: #4b5563;
  cursor: pointer;
}

.checkbox-label input {
  cursor: pointer;
}

.forgot-link {
  color: #2563eb;
  text-decoration: none;
  font-weight: 500;
}

.forgot-link:hover {
  text-decoration: underline;
}

/* ===== CAPTCHA ЗАГЛУШКА ===== */
.captcha-block {
  margin-bottom: 20px;
}

.captcha-box {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border: 1px solid #d1d5db;
  border-radius: 4px;
  background: #f9fafb;
  font-size: 14px;
  color: #374151;
}

.captcha-box input {
  cursor: not-allowed;
}

.captcha-logo {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: #9ca3af;
}

.captcha-icon {
  font-size: 20px;
  color: #2563eb;
}

.captcha-brand {
  font-weight: 600;
  color: #6b7280;
}

.captcha-links {
  font-size: 10px;
  color: #9ca3af;
}

/* ===== СООБЩЕНИЕ О БЛОКИРОВКЕ ===== */
.lock-message {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
  padding: 10px 14px;
  border-radius: 6px;
  font-size: 13px;
  margin-bottom: 16px;
  text-align: center;
}

/* ===== КНОПКА "ВОЙТИ" ===== */
.submit-button {
  width: 100%;
  padding: 14px;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 16px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.submit-button:hover:not(:disabled) {
  background: #1d4ed8;
}

.submit-button:disabled {
  background: #93c5fd;
  cursor: not-allowed;
}

/* ===== СООБЩЕНИЕ ОБ ОШИБКЕ ===== */
.error-message {
  color: #dc2626;
  text-align: center;
  margin-top: 16px;
  font-size: 14px;
}
</style>