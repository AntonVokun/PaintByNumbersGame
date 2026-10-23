# Индекс проекта

Вход для агента: [AGENTS.md](../AGENTS.md). Читать один нужный раздел, затем конкретный код по ссылке.
Текущее состояние диска — Git и Editor; опубликованный снимок — **AgentSync/STATE.json** с датой, HEAD и границами проверок. Эта стабильная карта содержит только навигацию.

| Что нужно | Документ |
|---|---|
| Найти компонент и связи | [PROJECT_MAP](PROJECT_MAP.md) |
| Разобраться в сценах / меню / WinPanel | [SCENE_MAP](SCENE_MAP.md) |
| Изменить или проверить поведение | [GAMEPLAY_SYSTEMS](GAMEPLAY_SYSTEMS.md) |
| Добавить уровень | [LEVEL_PIPELINE](LEVEL_PIPELINE.md), [чеклист](../LEVEL_CREATION_CHECKLIST.md) |
| Импорт / архив / GUID | [ASSET_STRUCTURE](ASSET_STRUCTURE.md) |
| Известная ошибка | [TROUBLESHOOTING](TROUBLESHOOTING.md) |
| Причина принятого решения | [DECISIONS](DECISIONS.md) |
| Передать / проверить результаты | [AGENT_SYNC_GUIDE](AGENT_SYNC_GUIDE.md) |

Машинный список реальных сцен, скриптов, префабов и ресурсов: [инвентарь](generated/PROJECT_INVENTORY.json).
Не читать весь JSON по умолчанию; `Tools/Update-ProjectInventory.ps1` печатает только количества.
Собрать выбранный контекст: `Tools/Get-AgentContext.ps1 -Topic levels -IncludeStatus`.
Короткий промт следующего уровня: [LEVEL_CREATION_PROMPT](../LEVEL_CREATION_PROMPT.md).

Короткий вход: `Tools/Get-AgentContext.ps1 -Compact` (опционально `-WithUnity`). Контракт source-only подготовки и пакетных проверок — раздел P0 в [LEVEL_PIPELINE](LEVEL_PIPELINE.md).
