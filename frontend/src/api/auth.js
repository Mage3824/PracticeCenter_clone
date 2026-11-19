// src/api/auth.js
import http from './client.js'

/**
 * Функция логина пользователя.
 * 
 * Отправляет POST-запрос на /api/auth/login с логином и паролем.
 * Возвращает объект AuthResult из API-контракта (раздел 7.3):
 * {
 *   isSuccess: true/false,
 *   requiresCaptcha: true/false,
 *   isLocked: true/false,
 *   message: "текст ошибки или пусто",
 *   lockedUntil: "2024-03-10T10:15:00" или null
 * }
 */
export async function loginApi(login, password) {
  try {
    const response = await http.post('/api/auth/login', { 
      login, 
      password 
    })
    return response.data
  } catch (error) {
    // Если сервер упал (500) или нет связи — пробрасываем ошибку дальше
    throw error
  }
}