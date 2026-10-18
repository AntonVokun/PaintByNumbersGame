# Обмен результатами агентов

Стабильные код и карта — [GitHub](https://github.com/AntonVokun/PaintByNumbersGame).
Оперативные отчёты — [SmartColoring-AgentSync в Drive](https://drive.google.com/drive/folders/1b1AoAK3OG31qDGcCmOpdOOd54j6ePph9).
Папка частная, права доступа не расширялись. Использовать подключённую учётную запись.
Локальная папка `AgentSync/` исключена из Git; привязки Drive и отчёты не коммитить.

## Другому ChatGPT

Команда пользователя: **«Проверь последнюю работу Work по Умной раскраске».**

1. Через подключённый Google Drive найти SmartColoring-AgentSync и открыть **корневой STATE.json**.
2. Проверить projectId, generatedAtUtc, reportId, branch, headSha, dirty и sync.status. Старый отчёт не считать текущим.
3. Открыть STATUS, CHANGES, TESTS и NEXT_STEPS **по URLs из STATE.files**. Они находятся в неизменяемой папке reports/reportId.
4. Убедиться, что reportId внутри файлов совпадает. При доступном вычислении SHA256 сверить files[].sha256; при несовпадении остановить оценку.
5. CODE_DIFF.patch читать по необходимости: это ограниченная выборка текста относительно baseSha, а не полный проект. patch.omitted объясняет пропуски.
6. Сверить код/документацию с GitHub. headSha — локальная база, remote — отдельно снятые сведения; незакоммиченные изменения ещё не опубликованы. Разные SHA не доказывают отношение предок/потомок.
7. Дать независимую оценку, указав подтверждённое, непроверенное и вопросы. При отсутствии/неполноте отчёта явно сообщить это.

Корневой STATUS — удобная копия; при публикации он может смениться раньше STATE.
Только STATE с версионными URLs определяет согласованный снимок. При смене указателя во время чтения старый снимок остаётся доступен.
Drive не даёт доступа к D:, запущенному Unity или полной истории чата. Проверка отчёта не заменяет запуск игры.
До отдельного commit/push новая карта есть локально и в текстовом patch отчёта, но может отсутствовать на GitHub.

## Work после задачи

1. Обновить затронутые разделы и выполнить `Tools/Update-ProjectInventory.ps1 -ValidateDocs`.
2. Записать короткие факты в `AgentSync/observations.json`: completed, impact, checks, issues, nextSteps, remote. Для check указать name, status, scope, evidence, observedAtUtc и при необходимости artifactHashes (путь → SHA256).
3. Выполнить `Tools/Generate-AgentReport.ps1 -Task "Описание задачи" -Status ready_for_review`.
4. В среде инструментов текущего агента выполнить функцию из [sync_via_connector.js](../Tools/AgentSync/sync_via_connector.js), передав tools и projectRoot. Она по умолчанию сама вызывает генератор; после шага 3 использовать generate:false.
5. Проверить возвращённый PASS и `AgentSync/SYNC_RECEIPT.json`. При ошибке сообщить, что новый снимок не подтверждён; не называть локальную подготовку синхронизацией.

Вызов издателя агентом через functions.exec (исходник из доверенного checkout, выводить только результат):

```javascript
const source = await tools.exec_command({
  cmd: "Get-Content -LiteralPath 'Tools/AgentSync/sync_via_connector.js' -Raw -Encoding UTF8",
  workdir: "D:\\Unity game\\PaintByNumbersGame", max_output_tokens: 16000
});
if (source.exit_code !== 0) throw new Error('Publisher source unavailable');
const publish = eval(source.output);
text(await publish(tools, {projectRoot: "D:\\Unity game\\PaintByNumbersGame", generate: false}));
```

Издателю нужны уже подключённые Drive create_folder, list_folder, upload_file, update_file, fetch и get_file_metadata, а также exec_command.
Это вызов инструментов Work после задачи, без нового OAuth и фоновой службы.
**Обычный запуск PowerShell готовит отчёт; сам по себе он не имеет доступа к коннектору чата.**
Поэтому автономная синхронизация произвольных изменений диска без агента не настроена.
Google Drive для компьютера в проверенных стандартных местах/процессах не обнаружен; программы не устанавливались.
В новой среде агенту надо сначала получить действующую привязку папки через Drive и сохранить её локально; секреты не нужны.

## Протокол и ограничения

- Генератор: [agent_report.py](../Tools/AgentSync/agent_report.py), стандартная библиотека Python. Unity читается через CLI, второй Editor не запускается.
- Он собирает Git, карту сцен, манифесты, факты тестов и компактные счётчики Editor. Недоступный Editor отмечается available:false.
- `AgentSync/reports/reportId/` формируется временно и переименовывается, затем корневой STATE заменяется атомарно последним. Читатель использует пути снимка.
- Издатель проверяет SHA256 и допустимые пути всех шести файлов, создаёт версионную папку Drive, читает обратно каждый текст, затем последним обновляет корневой STATE. Повторная публикация того же reportId использует те же ID; несовпадающий снимок не перезаписывает.
- Чтение обратно сравнивает полный текст; SHA256 посчитаны локально. Drive metadata не выдала md5Checksum, поэтому серверный MD5 не заявляется.
- STATE.json хранит обычный JSON с MIME text/plain: при application/json этот коннектор вернул пустой readable content; после смены MIME тот же файл читается полностью. Расширение и JSON-схема сохранены.
- Код diff ограничен 200 KB и разрешёнными текстовыми путями. Сцены, .meta, PNG, большие файлы и общие логи исключены. Сканер известных форматов секретов останавливает передачу, но не заменяет внимательную проверку новых типов данных.
- В STATE.files перечислены пять содержательных файлов; сам STATE не может содержать собственный хеш. План и sync.snapshotState дают хеш шестого файла.
- Новая генерация создаёт новый reportId. История сохраняется; очистка/удаление отчётов автоматически не выполняется.

Первый тест: локальный STATUS A загружен, затем тот же ID обновлён до B; содержимое и modified_time прочитаны через коннектор ChatGPT. Проверка отдельного другого чата остаётся пользовательской проверкой доступности его подключения.
Точные ID и времена актуальной публикации — SYNC_RECEIPT, а не постоянная карта.

## Контекст для Codex и Unity Assistant

Codex автоматически читает AGENTS.md по правилам своего клиента: [официальная инструкция](https://learn.chatgpt.com/docs/agent-configuration/agents-md).
Этот документ направляет агента к одному нужному разделу. Для ручного получения контекста есть `Tools/Get-AgentContext.ps1 -Topic levels` (также overview, architecture, scenes, gameplay, assets, troubleshooting, sync).

В установленном Unity AI Assistant 2.13.0-pre.2 проверены GatewayProjectPreferences.IncludeDefaultAgentsMd=true и AcpProvider.TryCopyDefaultAgentsMd: при запуске ACP-провайдера рабочая папка по умолчанию — корень проекта, AGENTS.md создаётся только если файла ещё нет. Наш файл не перезаписывается.
Это подтверждает поддержку файла в этой интеграции; прочтение зависит от ACP-клиента. Сам MCP передаёт команды, а не гарантирует загрузку документации в любой внешний чат.
Если клиент не загрузил AGENTS, дать ему файл и один нужный раздел через Get-AgentContext; не отправлять весь docs.
