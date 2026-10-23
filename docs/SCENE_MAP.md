# Сцены

Источник списка — [EditorBuildSettings](../ProjectSettings/EditorBuildSettings.asset), деталей — сохранённые сцены и [инвентарь](generated/PROJECT_INVENTORY.json).

| Сцена | В сборке | Зоны / палитра | Следующая |
|---|---|---|---|
| [MainMenu](../Assets/_Project/Scenes/MainMenu.unity) | Да | 7 разных карточек LevelCardUI, прокручиваемая сетка в2 колонки | Выбранная сцена |
| [Level_001_New](../Assets/_Project/Scenes/Level_001_New.unity) | Да | 16 / 16 | Level_002_New |
| [Level_002_New](../Assets/_Project/Scenes/Level_002_New.unity) | Да | 17 / 17 | Level_003_New |
| [Level_003_New](../Assets/_Project/Scenes/Level_003_New.unity) | Да | 15 / 15 | Level_004_New |
| [Level_004_New](../Assets/_Project/Scenes/Level_004_New.unity) | Да | 17 / 17, «7. кот» | Level_005_New |
| [Level_005_New](../Assets/_Project/Scenes/Level_005_New.unity) | Да | 38 / 38, «Медуза» | Level_006_New |
| [Level_006_New](../Assets/_Project/Scenes/Level_006_New.unity) | Да | 25 / 25, «Домик» | Level_007_New |
| [Level_007_New](../Assets/_Project/Scenes/Level_007_New.unity) | Да | 23 / 23, «Чаепитие» | MainMenu |
| [Level_001](../Assets/_Project/Scenes/Level_001.unity) | Нет | 16 групп; legacy-иерархия | Не использовать вместо New |
| [Level_002](../Assets/_Project/Scenes/Level_002.unity) | Нет | 17 групп; legacy-иерархия | Не использовать вместо New |

## Общий уровень New

Корни: Camera, Canvas, EventSystem, LevelRoot, Managers.
`LevelRoot/Art/Zones` содержит группы; `Art/Outlines` — отдельный верхний контур.
`LevelRoot/Background/WhitePaper` — подложка; `Background/Background` — фон.
Managers содержит PaintController, Scanner и SoundManager.

Canvas — Screen Space Overlay, Scale With Screen Size, 1080×1920, Match Width.
Его основные части — TopBar, PaletteWindow/Palette, GameplayViewport, WinPanel.
TopBar содержит BackButton и HintButton с лампочкой; HintButton заменяет звук только в New-уровнях.
CameraZoomAndPan получает явные ссылки на GameplayViewport и фоновый SpriteRenderer.
В Play создаётся дополнительная ViewportBackgroundCamera; её нет в сохранённой иерархии.
Не путать общий экранный фон и двигаемый рисунок. При fit учитываются Art и WhitePaper.

## Главное меню

Корни: Main Camera, EventSystem, Canvas, Managers, Background.
Карточки LevelCard_001/002/003/004/005/006/007 находятся в
`Canvas/LevelsScrollView/Viewport/Content`; GridLayoutGroup + ContentSizeFitter.
Каждая карточка связывает LevelCardUI с Button, Preview, двумя Sprite и именем New-сцены.
MainMenuController содержит список levelSceneNames для сброса и levelCards для Refresh.
Старые поля level001Preview/level002Preview сохранены, но основная система — массив карточек.
ResetProgressButton вызывает сброс зарегистрированных сцен. Дизайн меню не перестраивать при добавлении карточки.
Имена карточек последовательно «Уровень 1»–«Уровень 7»; первые три превью сохранены.
Четвёртая карточка показывает новый контур «7. кот», прежний повтор рисунка второго уровня заменён.
Отдельной системы блокировки уровней нет: карточки открывают любую зарегистрированную сцену.

## WinPanel

Общий [WinPanel.prefab](../Assets/_Project/UI/WinPanel/Prefabs/WinPanel.prefab) изначально неактивен.
После завершения и replay контроллер ждёт нового нажатия, затем показывает панель.
NextButton → WinPanelUI.LoadNextLevel → PaintController.nextLevelSceneName.
MenuButton → WinPanelUI.GoToMenu → BackToMenu.GoToMenu.
Фон и украшения общие; MenuButton и NextButton используют готовые мятные кнопки с домиком/стрелкой
из Art/UI и подписи «В меню»/«Вперёд». Старые дочерние MenuIcon/NextIcon выключены, чтобы не накладываться.
Обработчики и порядок завершения сохранены.
Вспомогательные [NumberPrefab](../Assets/_Project/Prefabs/NumberPrefab.prefab) и
[NumberPrefab_3D](../Assets/_Project/Prefabs/NumberPrefab_3D.prefab) существуют, но их наличие не означает видимые номера в уровнях.

## Фактический статус третьего уровня

«Котик на круге»: общий холст 1254×1254, 15 цветовых групп, 77 связных участков масок, 89 контуров коллайдеров.
Сцена и подключения существуют; Play Mode ранее проверен в этой сессии.
Это не завершённая Android-валидация: последняя сборка Failed на Gradle loopback, дальнейшая APK-сборка остановлена пользователем.
Датированные результаты, текущие Console и HEAD всегда читать из AgentSync, не принимать эту карту за новый игровой тест.

## Четвёртый уровень

«7. кот»: 1254×1254, 18 исходных RGBA-масок, 17 PaintZone/кнопок, 73 участка, 81 контур.
Один PaintZone содержит все участки группы; Zone_017/018 объединены в группу 17 с шестью участками.
Zone_013/016 раздельны. RGB, alpha, градиенты и детали исходников сохранены.
Старые ресурсы и прогресс Level_004 сохранены вне Assets; несовместимый прогресс только этого уровня сброшен.
Последний Play Mode и ограничения смотреть в AgentSync; Android APK в этой задаче запрещён пользователем.
