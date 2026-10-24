# Архитектура

«Умная раскраска» — мобильная 2D-игра: выбрать цвет и нажатием заполнить его группу участков.
Подтверждённая версия — Unity 6000.4.4f1; основная цель Android.
Версии проверять в [ProjectVersion](../ProjectSettings/ProjectVersion.txt) и [manifest](../Packages/manifest.json).

## Входы и размещение

- [MainMenu](../Assets/_Project/Scenes/MainMenu.unity) — карточки и сброс.
- [Сцены уровней](SCENE_MAP.md) — экземпляры общих runtime-компонентов.
- `Assets/_Project/Art/` — изображения; `Assets/_Project/Prefabs/` — вспомогательные префабы номеров.
- `Assets/_Project/UI/WinPanel/` — общий экран завершения.
- `Assets/_Project/Editor/LevelCreation/` и `Tools/LevelCreation/` — редакторский и файловый конвейер.
- `ArtSources/` — оригиналы и рабочая геометрия вне импорта Unity.
- `ProjectSettings/EditorBuildSettings.asset` — включённые сцены; отдельный активный BuildProfile при проверке не выбран.
- `docs/` — стабильные знания; игнорируемый `AgentSync/` — оперативные отчёты и частные ID Drive.

## Компоненты

| Файл | Ответственность / связи |
|---|---|
| [PaintController](../Assets/_Project/Scripts/PaintController.cs) | Singleton сцены; массивы зон/палитры; подсчёт, SaveSystem, replay и WinPanel; LoadNextLevel |
| [PaintZone](../Assets/_Project/Scripts/PaintZone.cs) | SpriteRenderer + Collider2D; Paint после tap; анимация, контрастный overlay, уведомление контроллера |
| [SelectColor](../Assets/_Project/Scripts/SelectColor.cs) | Выбранный ID и его сброс; подсветка, 10-секундная подсказка и подключение лампочки HintButton в TopBar |
| [SaveSystem](../Assets/_Project/Scripts/SaveSystem.cs) | Статический слой PlayerPrefs: цвет/завершение по имени сцены, регистрация ID, сброс и звук |
| [MainMenuController](../Assets/_Project/Scripts/MainMenuController.cs) | Карточки, список сцен для сброса; сохранены старые вспомогательные поля двух уровней |
| [LevelCardUI](../Assets/_Project/Scripts/LevelCardUI.cs) | Имя сцены, два превью, кнопка; Start регистрирует OpenLevel, Refresh читает SaveSystem |
| [CameraZoomAndPan](../Assets/_Project/Scripts/CameraZoomAndPan.cs) | Fit, tap/drag/pinch, UI-блокировка, экранный допуск касания, плавные zoom и поиск выбранной группы |
| [ZoneFocusTargets](../Assets/_Project/Scripts/ZoneFocusTargets.cs) | Кэш пригодных точек внутри отдельных путей существующего PolygonCollider2D |
| [SelectedZoneHighlight](../Assets/_Project/Scripts/SelectedZoneHighlight.shader) | Общий shader выбора; материал Assets/Resources/SelectedZoneHighlight.mat использует существующие PNG |
| [LevelTransition](../Assets/_Project/Scripts/LevelTransition.cs) | Общий временный экран загрузки и LoadSceneAsync для карточек, Next, возврата и перезапуска |
| [PaletteDragScroll](../Assets/_Project/Scripts/PaletteDragScroll.cs) | Настройка ScrollRect, HorizontalLayoutGroup и ContentSizeFitter, обновление после удаления кнопок |
| [ProjectScanner](../Assets/_Project/Scripts/ProjectScanner.cs) | Диагностика групп, контроллера, палитры и Collider2D. Считает PaintZone-объекты, не островки PNG |
| [GameSound](../Assets/_Project/Scripts/GameSound.cs) | Singleton сцены; AudioSource.PlayOneShot(clickSound), учёт настройки звука |
| [SoundToggleButton](../Assets/_Project/Scripts/SoundToggleButton.cs) | Кнопка звука и спрайты включено/выключено |
| [BackToMenu](../Assets/_Project/Scripts/BackToMenu.cs) | Выход через Application.Quit в MainMenu; в уровнях — LevelTransition.Load(MainMenu) |
| [WinPanelUI](../Assets/_Project/UI/WinPanel/Scripts/WinPanelUI.cs) | Делегирует Next контроллеру, Menu — BackToMenu |
| [UIButtonPressAnimation](../Assets/_Project/Scripts/UIButtonPressAnimation.cs) | Доступный вспомогательный обработчик нажатия; не считать назначенным каждой кнопке автоматически |
| [LevelPreviewCard](../Assets/_Project/Scripts/LevelPreviewCard.cs) | Старый вспомогательный компонент превью; текущая система карточек — LevelCardUI |

```mermaid
flowchart LR
  Card[LevelCardUI] --> Loading[LevelTransition]
  Loading --> Scene[Игровая сцена]
  Select[SelectColor] --> Zone[PaintZone]
  Zone --> Controller[PaintController]
  Controller --> Save[SaveSystem / PlayerPrefs]
  Card --> Save
  Controller --> Win[WinPanelUI]
  Win --> Controller
  Win --> Menu[MainMenu]
```

## Изображения интерфейса и Android-иконка

[HintBulb](../Assets/_Project/Art/UI/Buttons/HintBulb.png) — лампочка в TopBar New-уровней.
WinPanel использует готовые кнопки из Art/UI, обработчики WinPanelUI сохранены.
В Android Player Settings заполнены Legacy, Round и оба слоя Adaptive во всех размерах:
[AppIcon_CatApples](../Assets/_Project/Art/UI/Icons/AppIcon_CatApples.png),
[AppIcon_CatApples_Adaptive](../Assets/_Project/Art/UI/Icons/AppIcon_CatApples_Adaptive.png) и
[мятный фон](../Assets/_Project/Art/UI/Icons/AppIcon_MintBackground.asset).
Исходник — кот с яблоками Level_001: outlines1.jpg и outlines.png. Встроенный ImageGen подготовил
вариант «тот же кот, половина цветная, половина контурная, мятный фон, поля под Android-маску».
Для Adaptive использован отдельный вариант с увеличенными полями; маска Android обрезает только фон.

## Внешние зависимости

Подтверждены UPM: Input System 1.19.0, uGUI 2.0.0, Sprite 1.0.0, Pipeline 0.6.0-exp.1,
Unity AI Assistant 2.13.0-pre.2, Test Framework 1.6.0, Purchasing 4.15.0, Ads 4.16.4.
Их установка **не подтверждает** настроенные покупки, рекламу или backend.
Полный список — инвентарь/manifest; версии без необходимости не менять.
Генерация PNG требует Pillow + numpy, отчёты — Python standard library + PowerShell + Git.
