# Индекс проекта

Вход для агента: [AGENTS.md](../AGENTS.md). Читать один нужный раздел, затем конкретный код по ссылке.
Текущий статус, ветка, HEAD и тесты — **AgentSync**, а не эта стабильная карта.

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

P0-вход: `Tools/Get-AgentContext.ps1 -Compact` (опционально `-WithUnity`). Не печатать STATE/однострочные baseline целиком. Source-only подготовка трёх наборов и контракт проверок — раздел P0 в [LEVEL_PIPELINE](LEVEL_PIPELINE.md).
