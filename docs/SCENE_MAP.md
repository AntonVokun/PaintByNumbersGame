# Сцены

Источник списка — [EditorBuildSettings](../ProjectSettings/EditorBuildSettings.asset), деталей — сохранённые сцены и [инвентарь](generated/PROJECT_INVENTORY.json).

| Сцена | В сборке | Зоны / палитра | Следующая |
|---|---|---|---|
| [MainMenu](../Assets/_Project/Scenes/MainMenu.unity) | Да | 3 карточки LevelCardUI | Выбранная сцена |
| [Level_001_New](../Assets/_Project/Scenes/Level_001_New.unity) | Да | 16 / 16 | Level_002_New |
| [Level_002_New](../Assets/_Project/Scenes/Level_002_New.unity) | Да | 17 / 17 | Level_003_New |
| [Level_003_New](../Assets/_Project/Scenes/Level_003_New.unity) | Да | 15 / 15 | MainMenu |
| [Level_001](../Assets/_Project/Scenes/Level_001.unity) | Нет | 16 групп; legacy-иерархия | Не использовать вместо New |
| [Level_002](../Assets/_Project/Scenes/Level_002.unity) | Нет | 17 групп; legacy-иерархия | Не использовать вместо New |

## Общий уровень New

Корни: Camera, Canvas, EventSystem, LevelRoot, Managers.
`LevelRoot/Art/Zones` содержит группы; `Art/Outlines` — отдельный верхний контур.
`LevelRoot/Background/WhitePaper` — подложка; `Background/Background` — фон.
Managers содержит PaintController, Scanner и SoundManager.

Canvas — Screen Space Overlay, Scale With Screen Size, 1080×1920, Match Width.
Его основные части — TopBar, PaletteWindow/Palette, GameplayViewport, WinPanel.
CameraZoomAndPan получает явные ссылки на GameplayViewport и фоновый SpriteRenderer.
В Play создаётся дополнительная ViewportBackgroundCamera; её нет в сохранённой иерархии.
Не путать общий экранный фон и двигаемый рисунок. При fit учитываются Art и WhitePaper.

## Главное меню

Корни: Main Camera, EventSystem, Canvas, Managers, Background.
Карточки LevelCard_001/002/003 находятся в
`Canvas/LevelsScrollView/Viewport/Content`; GridLayoutGroup + ContentSizeFitter.
Каждая карточка связывает LevelCardUI с Button, Preview, двумя Sprite и именем New-сцены.
MainMenuController содержит список levelSceneNames для сброса и levelCards для Refresh.
Старые поля level001Preview/level002Preview сохранены, но основная система — массив карточек.
ResetProgressButton вызывает сброс зарегистрированных сцен. Дизайн меню не перестраивать при добавлении карточки.

## WinPanel

Общий [WinPanel.prefab](../Assets/_Project/UI/WinPanel/Prefabs/WinPanel.prefab) изначально неактивен.
После завершения и replay контроллер ждёт нового нажатия, затем показывает панель.
NextButton → WinPanelUI.LoadNextLevel → PaintController.nextLevelSceneName.
MenuButton → WinPanelUI.GoToMenu → BackToMenu.GoToMenu.
Дизайн, иллюстрации и расположение кнопок сохранять.
Вспомогательные [NumberPrefab](../Assets/_Project/Prefabs/NumberPrefab.prefab) и
[NumberPrefab_3D](../Assets/_Project/Prefabs/NumberPrefab_3D.prefab) существуют, но их наличие не означает видимые номера в уровнях.

## Фактический статус третьего уровня

«Котик на круге»: общий холст 1254×1254, 15 цветовых групп, 77 связных участков масок, 89 контуров коллайдеров.
Сцена и подключения существуют; Play Mode ранее проверен в этой сессии.
Это не завершённая Android-валидация: последняя сборка Failed на Gradle loopback, дальнейшая APK-сборка остановлена пользователем.
Датированные результаты, текущие Console и HEAD всегда читать из AgentSync, не принимать эту карту за новый игровой тест.
