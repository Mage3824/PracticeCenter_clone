# WorkTime — API-контракты

## 1. Источник истины

**Swagger — главный источник истины по эндпоинтам, схемам и кодам ответов:** `/swagger` (UI) и `/swagger/v1/swagger.json` (машиночитаемая схема, генерируется из атрибуты маршрутов, DTO и `[ProducesResponseType]` в `src/WorkTime.Api/Controllers`). Комментарии Swagger приходят из XML-документации C# (`GenerateDocumentationFile` + `ShowDoc` в `Program.cs`).

Этот документ описывает **проводные правила, которые Swagger не передаёт**: формат скаляров (дата, время суток, минуты, метки), токены перечислений, семантику null-тела, набор machine-readable кодов ошибок, правило BR32 и ограничение частоты входа. Если текст здесь и схема в swagger.json расходятся — истина в swagger.json.

Транспорт: `application/json; charset=utf-8` для тел запросов и ответов; тело ошибки — `application/problem+json`. Принудительного HTTPS-редиректа в коде нет.

## 2. Эндпоинты (реализованы сейчас)

Список выведен из атрибутов контроллеров (`src/WorkTime.Api/Controllers/*.cs`) и `Program.cs`. Доступ: «любая роль» = Default/Fallback-политика (аутентифицированный вызывающий, см. §7).

| # | Метод и путь | Доступ | Тело / параметры | Успех | Ошибки (кроме 200) |
|---|---|---|---|---|---|
| 1 | `POST /api/auth/login` | анонимно + лимит 5/60 с по IP (§8) | `{ login, password }` | `LoginWire` (§5) | 400 `VALIDATION`/`FIELD_MISSING`; 404 `ENTITY_NOT_FOUND` (`entityType:"User"`); 429 `RATE_LIMITED` |
| 2 | `POST /api/work-days/start` | любая роль | `{ date, startTime }` | `WorkDayWire` | 400 `VALIDATION`/`DATE_INVALID`/`TIME_INVALID`/`IDENTITY_*`; 404 `User`; 409 `BR27`, `BR1` |
| 3 | `POST /api/work-days/{workDayId}/finish` | любая роль | `{ endTime }` | `WorkDayWire` (открытый перерыв закрывается вместе с днём — BR7 в сервисе) | 400 `VALIDATION`/`TIME_INVALID`/`IDENTITY_*`; 404 `WorkDay`; 409 `BR2`, `BR21` |
| 4 | `GET /api/work-days/current` | любая роль | — | `WorkDayWire` **или тело `null`** (открытой смены нет — 200, не 204/404) | 400 `VALIDATION`/`IDENTITY_*`/`User`→404 |
| 5 | `POST /api/work-days/{workDayId}/auto-finish` | любая роль | — | `WorkDayWire` + запись журнала (BR3) | 400 `IDENTITY_*`; 404 `WorkDay`; 409 `BR2` |
| 6 | `GET /api/work-days/{employeeId}/can-start?date=` | любая роль | query `date` | `{ canStart: true/false }` — BR27 приходит как `false`, тот же факт при открытии дня (эндпоинт 2) даёт 409 | 400 `VALIDATION`/`DATE_INVALID`; 404 `User`; 409 `BR21` (вопрос про чужого) |
| 7 | `POST /api/work-days/{workDayId}/breaks/start` | любая роль | `{ startTime }` | `BreakWire`; превышение 4 ч перерывов — не ошибка, флаг `isBreakLimitExceeded` (BR9) | 400 `VALIDATION`/`TIME_INVALID`; 404 `WorkDay`; 409 `BR6`, `BR10`, `BR21` |
| 8 | `POST /api/breaks/{breakId}/finish` | любая роль | `{ endTime }` | `BreakWire`; перерыв короче 5 мин закрывается без ошибки с `countedInReports:false` (BR8) | 400 `VALIDATION`/`TIME_INVALID`; 404 `Break`/`WorkDay`; 409 `BR10`, `BR6`, `BR8`, `BR21` |
| 9 | `GET /api/reports/daily/{workDayId}` | любая роль | path `workDayId` | `DailyReportWire` **или тело `null`** (BR15: незавершённый день / отчёта нет — 200, не пустое тело) | 400 `VALIDATION`; 404 `WorkDay`/`User`; 409 `BR21`, `BR12` |
| 10 | `GET /api/reports/monthly?periodClosureId=&departmentId=` | ManagerOrAdmin | query (§6) | `MonthlyReportWire[]`, `[]` когда окно не разрешается; ничего не выбрасывается — BR15/BR16/BR26/BR5/BR4/BR11 формируют payload | 400 `VALIDATION` (нечитаемый guid); 403 (§7) |
| 11 | `POST /api/reports/{workDayId}` | любая роль | `{ text }` (пустая строка легальна — очищенный черновик) | `ReportWire` | 400 `VALIDATION`/`FIELD_MISSING`; 404 `WorkDay`/`User`; 409 `BR21`, `BR14`, `BR12` (после конца дня правит только администратор) |
| 12 | `POST /api/reports/{workDayId}/comments` | ManagerOrAdmin | `{ reportId, text }` | `CommentWire` | 400 `VALIDATION`/`REPORT_ID_REQUIRED`/`REPORT_WORK_DAY_MISMATCH`/`FIELD_MISSING`; 404 `Report`/`User` |
| 13 | `GET /api/reports/{workDayId}/comments` | ManagerOrAdmin | path `workDayId` | `CommentWire[]`, `[]` когда комментариев нет | 400 `VALIDATION`; 403 |
| 14 | `GET /api/reports/monthly/export?periodClosureId=&departmentId=` | ManagerOrAdmin | query как у 10 | 200, байты CSV: `text/csv; charset=utf-8`, `Content-Disposition: attachment; filename="monthly-report.csv"`, UTF-8 с BOM, разделитель `;` | 400 `VALIDATION`; 403 |
| 15 | `GET /api/reports/monthly/export.xlsx` | ManagerOrAdmin | — | — | **501 `EXPORT_NOT_IMPLEMENTED`** (Excel появится на этапе 4; данные — эндпоинт 14) |
| 16 | `POST /api/absences/{employeeId}` | ManagerOrAdmin | `{ date, reason }` | `AbsenceWire` | 400 `VALIDATION`/`DATE_INVALID`/`REASON_INVALID`; 404 `User` (актор или цель); 409 `BR25` |
| 17 | `PUT /api/absences/{employeeId}` | ManagerOrAdmin | то же тело, та же семантика (BR28 переиспользует id записи за день) | `AbsenceWire` | как 16 |
| 18 | `GET /api/absences/{employeeId}?date=` | любая роль; Employee — только свой id | query `date` | `AbsenceWire[]`, `[]` когда статуса нет | 400 `VALIDATION`/`DATE_INVALID`; **403** чужой id для Employee (пустое тело) |
| 19 | `GET /api/absences?from=&to=` | ManagerOrAdmin | query `from`, `to` | `AbsenceWire[]`, `[]` когда диапазон пуст | 400 `VALIDATION`/`DATE_INVALID` |
| 20 | `GET /api/users?name=&includeInactive=` | ManagerOrAdmin | query: подstring по имени; `includeInactive` добавляет деактивированные (BR24) | `UserWire[]`, `[]` когда совпадений нет | 403 |
| 21 | `GET /api/users/{userId}` | ManagerOrAdmin | path | `UserWire` | 400 `VALIDATION`; 404 `User` |
| 22 | `POST /api/users` | AdminOnly | `{ login, fullName, password, role, departmentId }` — пароль только в запросе, никогда не возвращается (BR34) | `UserWire` | 400 `VALIDATION`/`ROLE_INVALID`/`FIELD_MISSING`; 404 `Department`; 409 `USER_LOGIN_TAKEN` |
| 23 | `PUT /api/users/{userId}` | AdminOnly | `{ fullName, role, departmentId?, isActive? }` (BR24: снятие доступа без удаления записи) | `UserWire` | 400 `VALIDATION`/`ROLE_INVALID`; 404 `User`/`Department`; 409 BR |
| 24 | `DELETE /api/users/{userId}` | AdminOnly | path | 200 без тела | 400 `VALIDATION`; 404 `User`; 409 `BR20` (последний активный администратор) |
| 25 | `POST /api/users/{userId}/deactivate` | AdminOnly | path | `UserWire` (запись и история сохранены, доступ закрыт — BR24) | 400 `VALIDATION`; 404 `User`; 409 `BR20` |
| 26 | `POST /api/users/{userId}/unlock` | AdminOnly | path | `UserWire` (досрочная разблокировка, право администратора — BR35) | 400 `VALIDATION`; 404 `User` |
| 27 | `GET /api/departments` | ManagerOrAdmin | — | `DepartmentWire[]`, `[]` когда отделов нет | 403 |
| 28 | `GET /api/departments/{departmentId}` | ManagerOrAdmin | path | `DepartmentWire` | 400 `VALIDATION`; 404 `Department` |
| 29 | `GET /api/departments/{departmentId}/children` | ManagerOrAdmin | path | `DepartmentWire[]` | 400 `VALIDATION`; 404 `Department` |
| 30 | `POST /api/departments` | AdminOnly | `{ name, parentId? }` | `DepartmentWire` | 400 `VALIDATION`/`FIELD_MISSING`; 404 `Department` (родитель) |
| 31 | `PUT /api/departments/{departmentId}` | AdminOnly | `{ name?, parentId? }` | `DepartmentWire` | 400 `VALIDATION`/`FIELD_MISSING`/**`SELF_PARENT`** (отдел как собственный родитель); 404 `Department` |
| 32 | `DELETE /api/departments/{departmentId}` | AdminOnly | path | 200 без тела | 400 `VALIDATION`; 404 `Department`; 409 `DEPARTMENT_HAS_CHILDREN`, `DEPARTMENT_HAS_EMPLOYEES` |
| 33 | `GET /api/audit-logs?entityId=` | AdminOnly (BR29) | query `entityId` обязателен: читается история одной сущности | `AuditLogWire[]` | 400 `ENTITY_ID_MISSING` (Guid.Empty) / `VALIDATION` (нечитаемый или отсутствует); 403 |
| 34 | `POST /api/period-closures/{periodClosureId}/close` | AdminOnly (BR23.1: с этого момента данные внутри периода редактируются только администратором) | path | `PeriodClosureWire` | 400 `IDENTITY_*`; 404 `PeriodClosure`; 409 `BR23.1` (уже закрыт) |
| 35 | `POST /api/period-closures/{periodClosureId}/reopen` | AdminOnly (BR23.2 «Emergency correction», операция логируется) | `{ reason }` — причина/номер акта обязателен | `PeriodClosureWire` | 400 `VALIDATION`/`FIELD_MISSING`; 404 `PeriodClosure`; 409 `BR23.2` (не закрыт) |

В телах запросов больше нет `employeeId`/`currentUserId`/`editorId`/`actorId`: вызывающий берётся из subject токена (§7).

## 3. Сериализация скаляров

Реализация — `Wire.cs` + `WireFormats`; ключи wire-классов зафиксированы `[JsonPropertyName]`, поэтому проводные имена стабильны независимо от регистра C#-свойств (они PascalCase).

| Тип (C#) | JSON | Правило |
|---|---|---|
| `Guid` | строка — канонический UUID (`9b2f…`), никогда не null | id непрозрачны: фронтенд хранит и возвращает без изменений |
| `Guid?` | строка или null | `DepartmentWire.parentId`, `AbsenceWire.createdByUserId` |
| `TimeSpan` время суток | `"HH:mm"`, 24 ч, без секунд | секунды для времени часов не значимы; round-trip точен для значений системы |
| `TimeSpan?` время суток | `"HH:mm"` или null | null = смена/перерыв ещё открыты (BR2, BR10, BR15) |
| `TimeSpan` длительность | число целых минут (`480` = 8 ч) | арифметика на фронтенде тривиальна; ключи с суффиксом `Minutes` (`durationMinutes`, `totalMinutes`) — ключ не обещает «часы», неся минуты |
| `TimeSpan?` длительность | целые минуты или null | null = перерыв ещё открыт |
| `DateTime` дата | `"YYYY-MM-DD"` | серверная календарная дата (границу рабочего дня владеет сервер — BR3); не дата браузера |
| `DateTime` метка | `"YYYY-MM-DDTHH:mm:ss"`, серверное время, **без суффикса зоны** | ключ без суффикса: часы владеет сервер |
| `bool` / `int` / `long` | `true`/`false`, число | |
| перечисление | строка = имя члена C#, точный регистр (§4) | порядковый номер никогда не отправляется |
| `List<T>` | массив, `[]` когда пусто (никогда не null) | |
| `Dictionary<string,long>` (`hoursByDay`) | объект `"YYYY-MM-DD"` → минуты | разреженные дни остаются разреженными: BR5, BR15, BR26 пропускают дни, поэтому массив из 28/31 слотов — неверная форма |
| nullable результат (`DailyReport?`, открытой смены нет) | **тело `null`** с 200 и `content-type: application/json` | «не существует / нечего показать», а не 204 и не 404 |

## 4. Токены перечислений (`src/WorkTime.Domain/Enums.cs`)

| Перечисление | Токены в API |
|---|---|
| `UserRole` (в JWT-claim роли) | `"Admin"`, `"Manager"`, `"Employee"` |
| `WorkDayStatus` | `"Active"`, `"Finished"`, `"AutoFinished"` |
| `AbsenceReason` | `"Sick"`, `"Leave"`, `"DayOff"`, `"Absence"` |
| `AuditAction` | `"Create"`, `"Update"`, `"Delete"` |

Фронтенд сопоставляет токены → свои подписи и громко падает на неизвестном токене: новый член перечисления — изменение контракта, а не баг. Разбор запроса сверяет имя ординально (`RequestParsers.TryParseEnum`) и отказывает неизвестный токен с 400 (`REASON_INVALID`).

## 5. DTO на проводе

### Запросы (`Requests.cs`, `Requests.Management.cs`)

Отклонение обязательного поля модели — 400 `VALIDATION` (модель-state: `[Required]`, `[RegularExpression]` для даты `YYYY-MM-DD` и времени `HH:mm`); нечитаемое значение после разбора — отдельный код (§6).

| Тело | Ключи |
|---|---|
| `LoginRequest` | `login`, `password` |
| `StartWorkDayRequest` | `date` (`YYYY-MM-DD`), `startTime` (`HH:mm`) |
| `FinishWorkDayRequest` | `endTime` (`HH:mm`) |
| `StartBreakRequest` | `startTime` (`HH:mm`) |
| `FinishBreakRequest` | `endTime` (`HH:mm`) |
| `CreateReportRequest` | `text` (обязателен, пустая строка легальна) |
| `SetAbsenceRequest` | `date` (`YYYY-MM-DD`), `reason` (токены `AbsenceReason`) |
| `CreateUserRequest` | `login`, `fullName`, `password`, `role` (токены `UserRole`), `departmentId` (uuid) |
| `UpdateUserRequest` | `fullName`, `role`, `departmentId?`, `isActive?` |
| `CreateDepartmentRequest` | `name`, `parentId?` (uuid) |
| `UpdateDepartmentRequest` | `name?`, `parentId?` (uuid) |
| `CreateCommentRequest` | `reportId` (uuid), `text` |
| `ReopenPeriodRequest` | `reason` |

### Ответы

**`LoginWire`** — эндпоинт 1. Неудачный вход — данные, не ошибка (200): блокировка (BR35), обратный отсчёт и CAPTCHA (BR36) — флаги.

| Ключ | Тип | Примечание |
|---|---|---|
| `isSuccess` | boolean | false никогда не означает «выбросить» |
| `requiresCaptcha` | boolean | BR36: со второго неправильного пароля (только Manager/Admin) |
| `isLocked` | boolean | BR35 блокировка; BR24 деактивированный аккаунт |
| `message` | string | только для отображения (заполнитель `[RU]`); пусто при успехе |
| `lockedUntil` | timestamp \| null | BR35 цель обратного отсчёта UI |
| `accessToken` | string | **ключ отсутствует при неуспешном входе** (`JsonIgnore WhenWritingNull`) |
| `expiresIn` | number (секунды) | ключ отсутствует при неуспешном входе; срок токена = 8 ч (§7) |

**`WorkDayWire`** — 2, 3, 4, 5: `id`, `userId`, `date` `"YYYY-MM-DD"`, `startTime` `"HH:mm"`, `endTime` `"HH:mm"|null` (null ⇒ открытая смена), `status` (токены `WorkDayStatus`), `countedInReports` boolean (BR5: < 15 мин ⇒ день отсутствует в месячной таблице), `isBreakLimitExceeded` boolean (BR9: > 4 ч перерывов — уведомление менеджера, не ошибка).

**`BreakWire`** — 7, 8: `id`, `workDayId`, `startTime` `"HH:mm"`, `endTime` `"HH:mm"|null` (null ⇒ открыт; BR10 — не более одного открытого перерыва), `durationMinutes` integer|null, `countedInReports` boolean (BR8: false для < 5 мин — не вычитается и не попадает в итоги отчёта).

**`ReportWire`** — 11: `id`, `workDayId` (завершённый день), `text`, `createdAt` timestamp, `updatedAt` timestamp|null (после редактирования — BR12/BR30).

**`DailyReportWire`** — 9: `startTime` `"HH:mm"`, `endTime` `"HH:mm"|null`, `breaks` array of `{ startTime, endTime|null }` (список, не словарь — BR14), `totalMinutes` integer минуты (BR4 + BR11 + BR8: интервал минус учтённые перерывы).

**`MonthlyReportWire`** — 10, 14: `employeeName` (BR16 подпись строки), `hoursByDay` объект `"YYYY-MM-DD"` → минуты (разреженный), `totalMinutes` integer, `absenceDays` array `"YYYY-MM-DD"` (BR26: дни отсутствия — отдельная колонка, никогда часы).

**`AbsenceWire`** — 16–19: `id`, `userId`, `date`, `reason` (токены `AbsenceReason`), `createdByUserId` uuid|null (BR27 подсказка: к кому обратиться для снятия статуса). BR28: смена статуса за день переиспользует id записи, поэтому история остаётся привязанной к одной записи.

**`CommentWire`** — 12, 13: `id`, `reportId`, `managerId`, `text`, `createdAt` (BR13: видим руководителю и администратору, никогда строке сотрудника).

**`UserWire`** — 20–26: `id`, `login`, `fullName`, `role` (токены `UserRole`), `departmentId`, `isActive` (BR24), `failedLoginAttempts` integer (BR35 счётчик для UI обратного отсчёта), `lockedUntil` timestamp|null, `locked` boolean (BR35). `PasswordHash` намеренно отсутствует (BR34): ни одна форма не ожидает поле пароля в ответе.

**`DepartmentWire`** — 27–32: `id`, `name`, `parentId` uuid|null (null ⇒ верхний уровень; дерево — часть передачи, BR23.1 закрытия настраиваются по отделу).

**`PeriodClosureWire`** — 34, 35 (и query `periodClosureId` эндпоинта 10): `id`, `departmentId` (BR23.1), `year`, `month` integer (календарный месяц окна отчёта — BR16/BR18), `isClosed` boolean (BR23.1), `closedAt` timestamp|null, `closedBy` uuid|null, `reopenReason` string|null (BR23.2 обязательная причина/номер акта), `reopenedByUserId` uuid|null.

**`AuditLogWire`** — 33: `id`, `userId` (BR30 автор), `action` (токены `AuditAction`), `entityType` (`"WorkDay"`, `"Break"`, `"Report"`, …), `entityId`, `oldValue`/`newValue` string|null (BR30 предыдущее/новое значение; автозавершение BR3 пишет `Auto-finish: EndTime = 23:59:59`), `timestamp`.

**`CanStartWorkDayWire`** — 6: `{ canStart: boolean }`.

## 6. Формат ошибок

Тело — **ProblemDetails (RFC 7807)** в `application/problem+json`: `type`, `title`, `status`, `detail`. Machine-readable ключи travel как extensions того же тела: `code`, `message`, `httpStatus`, плюс `entityType`/`entityId` для 404, и `error` — конверт `{ code, message, httpStatus }` для совместимости фронтенда (`ErrorEnvelope`, `Middleware.cs`). Фронтенд выбирает текст по `code` и никогда не парсил человеческое сообщение; `[RU]` в сообщениях — заполнитель локализации.

Сборка тела: `WorkTimeExceptionHandler` (оборачивает весь конвейер — `Program.cs`), BR32-middleware, отказ rate limiter и MVC model-state (все через `ErrorEnvelope.TryEnvelope`).

| Статус | Когда | Коды |
|---|---|---|
| 400 | нечитаемый/неполный запрос: `ArgumentException(paramName: <код>)` или model-state (`[Required]`/`[RegularExpression]`) | `VALIDATION` (model-state, `type …/validation`), `DATE_INVALID`, `TIME_INVALID`, `FIELD_MISSING`, `REASON_INVALID`, `ROLE_INVALID`, `SELF_PARENT`, `REPORT_ID_REQUIRED`, `REPORT_WORK_DAY_MISMATCH`, `ENTITY_ID_MISSING`, `IDENTITY_MISSING`, `IDENTITY_INVALID`, `ROLE_MISSING`, `NO_REQUEST_CONTEXT`, `INVALID_REQUEST` (без paramName) |
| 401 | `UnauthorizedAccessException` → `WWW-Authenticate: Bearer` + код из префикса `[КОД]` в сообщении, иначе `UNAUTHORIZED`; BR32 → `SESSION_EXPIRED` (пишет middleware, `type …/session-expired`); отказ валидации JWT (JwtBearer) → 401 `WWW-Authenticate: Bearer` без тела | `SESSION_EXPIRED`, `UNAUTHORIZED`, … |
| 403 | политический отказ авторизации или Employee → чужой `employeeId` (эндпоинт 18) | пустое тело (без ProblemDetails) |
| 404 | `EntityNotFoundException` | `ENTITY_NOT_FOUND` + `entityType` (`User`, `WorkDay`, `Break`, `Report`, `Department`, `PeriodClosure`), `entityId` |
| 409 | `BusinessRuleException` | `BR1` (день уже открыт), `BR2` (день не открыт), `BR6` (день закрыт — перерыв), `BR8` (конец перерыва раньше начала), `BR10` (уже открыт/закрыт перерыв), `BR12` (окно редактирования закрыто: после конца дня правит только администратор), `BR14` (день ещё не завершён), `BR20` (последний активный администратор), `BR21` (чужая сущность), `BR25` (ролевой гейт статуса отсутствия), `BR27` (день помечен отсутствием), `BR23.1` (уже закрыт), `BR23.2` (не закрыт), `USER_LOGIN_TAKEN`, `DEPARTMENT_HAS_CHILDREN`, `DEPARTMENT_HAS_EMPLOYEES` |
| 429 | отказ политики `login-rate-limit` (только вход) | `RATE_LIMITED` (`type …/rate-limit`) |
| 500 | неожиданный сбой — внутреннее сообщение не утекает | `INTERNAL_ERROR` (`type …/internal-error`, detail `[RU] The request could not be completed.`) |
| 501 | контрактный путь Excel-экспорта до этапа 4 | `EXPORT_NOT_IMPLEMENTED` (`type …/export-not-implemented`, detail указывает CSV-вариант) |

Конверт 404, пример:

```json
{
  "type": "https://errors.worktime/entity-not-found",
  "title": "Entity not found",
  "status": 404,
  "detail": "[RU] No such working day.",
  "code": "ENTITY_NOT_FOUND",
  "message": "[RU] No such working day.",
  "httpStatus": 404,
  "entityType": "WorkDay",
  "entityId": "5a1c9b0e-2f31-4a6d-9b7c-0d2e4f60a8b1",
  "error": { "code": "ENTITY_NOT_FOUND", "message": "[RU] No such working day.", "httpStatus": 404 }
}
```

## 7. Аутентификация и сессия (BR32)

- `Authorization: Bearer <jwt>`; JWT подписан ключом из конфигурации (`JwtSettings`: `SecretKey`/`Issuer`/`Audience`/`ExpiryMinutes`, значения по умолчанию `WorkTime.Api`/`WorkTime.Client`/480 мин). Ключ проверяется на старте приложения; issuer/audience/lifetime/signing-key проверяются JwtBearer.
- Subject токена (`ClaimTypes.NameIdentifier`) = Guid пользователя — из него все эндпоинты берут вызывающего (`RequestIdentity.TrySubjectUserId`); `ClaimTypes.Role` = имя роли, читается политиками; `ClaimTypes.Name` = логин (только для отображения/журнала, в решениях не участвует).
- Политики: `AdminOnly` (Admin), `ManagerOrAdmin` (Manager, Admin), `EmployeeOnly` (Employee). Default и Fallback требуют аутентифицированного вызывающего, поэтому эндпоинт без атрибута тоже закрыт анонимно; анонимны только `POST /api/auth/login` (`[AllowAnonymous]`) и Swagger (`/swagger`, `/swagger/v1/swagger.json` — middleware, не MVC).
- **BR32:** срок токена и окно неактивности — 8 часов. `UserActivityTimeoutMiddleware` работает после аутентификации и до авторизации: если с последней записанной активности прошло больше 8 ч, ответ 401 `SESSION_EXPIRED` + `WWW-Authenticate: Bearer` (нужно войти заново) независимо от оставшегося срока токена. Любой аутентифицированный запрос продлевает активность — включая запросы, отвергнутые 400/409/404; успешный вход восстанавливает baseline (`AuthController`).
- Ролевые решения в сервисе (`BR21`, `BR12`, `BR25`) приходят как 409, а не 403 — 403 означает только политический отказ или чужой `employeeId` на эндпоинте 18.

## 8. Ограничение частоты входа

Реализовано (встроенный rate limiter): политика `login-rate-limit` — фиксированное окно, **5 разрешений на 60 секунд** для одного client IP (partition по `RemoteIpAddress`, без адреса — одна partition `unknown`), очередь пуста (`QueueLimit = 0`), авто-восполнение включено. Применена только к `POST /api/auth/login`; limiter стоит до аутентификации и авторизации, поэтому отказанный запрос не тратит проверку токена и ролей. Отказ: 429 + ProblemDetails `RATE_LIMITED` (без внутренних деталей). В окружении `Testing` middleware отключён (интеграционные тесты делают много входов с одного IP).

## 9. Чего нет в контракте

- Нигде нет списка `employeeIds`: `GET /api/reports/monthly` сам разрешает строки через активные сотрудники отдела. Клиент отправляет **критерии** (`id` / дата / период / отдел), никогда список ids. Разрешение окна: `?periodClosureId=<uuid>` → окно этого закрытого периода; `?departmentId=<uuid>` → окно активного закрытия отдела; ничего → `[]` (окно всегда должно приходить из закрытия или отдела).
- ids репозиториев, собранные клиентами; `cancellationToken` (только на сервере); текст сообщений `[RU]` как основа логики; `PasswordHash` в ответях; поля `employeeId`/`editorId`/`actorId`/`currentUserId` в телах.

## 10. Что изменилось при рефакторинге

- **Архитектура контроллеров:** контроллеры не зависят от репозиториев — логика и бизнес-правила в Application-сервисах (`UserService`, `DepartmentService`, `PeriodClosureService`, `AuditLogService`, `AbsenceService`, `WorkTimeService`, `ReportService`, `BreakService`); на контроллере остаются wire-маппинг, разбор параметров и личность субъекта. Добавлены сервисные методы: `AbsenceService.GetAbsencesForEmployeeOnDateAsync`/`GetAbsencesInRangeAsync`, `WorkTimeService.GetOpenWorkDayAsync`, `ReportService.AddCommentAsync`/`ListCommentsAsync` — отсюда новые эндпоинты 4, 12, 13, 18, 19.
- **Исключения:** `src/WorkTime.Api/ApiExceptions.cs` удалён; вместо кастомных типов стандартные `ArgumentException` (код в `paramName` → 400), `UnauthorizedAccessException` → 401, `EntityNotFoundException` → 404, `BusinessRuleException` → 409, прочее → 500. Устаревшие `InvalidRequestException`/`UnauthorizedRequestException` из старых таблиц больше не существуют. Тело — ProblemDetails + extensions вместо отдельного конверта.
- **.NET 10** (`net10.0`): включена XML-документация, Swagger показывает комментарии; Swagger стал машиночитаемым источником истины вместо markdown-таблицы «гипотетических» маршрутов этапа 1.
- **Именование:** свойства C# на проводе — PascalCase, но проводные ключи JSON **не изменились** (каждый зафиксирован `[JsonPropertyName]`). Все таблицы выше описывают проводные ключи.
- Убраны пометки `RESERVED` (этап 2 и этап 4): аутентификация, JWT, сессия BR32, комментарии, закрытие/вскрытие периодов, журнал событий и CSV-экспорт реализованы; остаётся нереализованным только Excel-экспорт (эндпоинт 15 отвечает 501).
