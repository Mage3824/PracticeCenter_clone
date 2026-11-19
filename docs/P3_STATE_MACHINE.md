# Практикант №3 — автомат состояний рабочего дня

## Рабочие состояния

`notStarted` — на выбранную дату нет записи WorkDay.

`working` — есть WorkDay со статусом `Active`, нет незавершённого Break.

`onBreak` — есть WorkDay `Active` и Break, у которого `EndTime == null`.

`finished` — WorkDay со статусом `Finished` или `AutoFinished`.

`absent` — имеется Absence на дату, имеет приоритет в отображении перед другими состояниями.

На backend `WorkDayStatus` содержит только `Active`, `Finished`, `AutoFinished`. Состояние `onBreak` **выводится из связанных записей Break** и не добавляется в доменный enum.

## Переходы

```text
Нет смены --start--> Active/working
Active/working --start break--> Active/onBreak
Active/onBreak --finish break--> Active/working
Active/working --finish day--> Finished
Active/onBreak --finish day--> Finished (перерыв закрывается автоматически)
Active --system auto-finish--> AutoFinished (метод есть, планировщик ещё не реализован)
```

Запрещённые переходы: второй старт незавершённого дня (BR1), завершение неактивной смены (BR2), перерыв без активной смены (BR6), два одновременных перерыва (BR10), чужая смена (BR21). Отсутствие блокирует начало дня (BR27). Перерыв меньше 5 минут не вычитается из итогового времени (BR8).

`AttendanceSummaryService` вычисляет отображаемый статус по фактическим данным. В нём нет собственного источника истины: все сущности приходят из репозиториев.

## Реализованные API

- `GET /api/attendance/summary?date=YYYY-MM-DD` — только Manager/Admin, у Manager автоматически выбирается свой отдел; у Admin допускается `departmentId`.
- `GET /api/work-days/current?date=YYYY-MM-DD` — текущий открытый день самого пользователя.
- `GET /api/work-days/history?from=YYYY-MM-DD&to=YYYY-MM-DD` — история своих смен (до 366 дней).
- `GET /api/work-days/{id}/breaks` — только собственные перерывы.
- `GET /api/reports/search?text=&employeeId=&departmentId=&from=&to=` — поиск отчётов (по ролям).

## Проверка

`backend/tests/WorkTime.Tests/UnitTests/Services/AttendanceSummaryServiceTests.cs`:
- отсутствие смены → `notStarted`;
- незакрытый перерыв → `onBreak`;
- законченный день → корректное вычитание только учитываемого перерыва;
- отсутствие → `absent`;
- фильтр отделов исключает чужих сотрудников.

## Доработки после встречи

- Выделить формальную policy-таблицу переходов и расширить xUnit на все граничные сценарии.
- Добавить фоновый процесс (HostedService) автозавершения с правильной timezone-политикой.
- Использовать единое серверное время, а не местное время браузера; избежать расхождения часов.
- Завершить MySQL-персистентность; определить идемпотентность и атомарность переходов.
