# Умная Раскраска | Smart Coloring

## О проекте | About Project

**Умная Раскраска** — мобильная 2D игра-раскраска по номерам на Unity.

Главная идея игры — быстрый и приятный процесс раскрашивания без долгого и скучного закликивания маленьких зон.

В обычных раскрасках по номерам процесс часто слишком медленный и утомительный.
В этой игре игрок быстро выбирает цвет и закрашивает сразу все подходящие области изображения.

Идея вдохновлена быстрым и легким потреблением контента, как в TikTok:

* зашел;
* быстро покрасил;
* получил удовольствие;
* перешел к следующей картинке.

---

**Smart Coloring** is a mobile 2D paint-by-numbers game made with Unity.

The main idea of the game is fast and relaxing coloring without tedious tapping on tiny areas.

Traditional coloring-by-number games can feel too slow and repetitive.
In this project the player quickly selects a color and fills matching areas much faster.

The gameplay is inspired by fast and lightweight content consumption similar to TikTok:

* open the game;
* quickly color a picture;
* enjoy the process;
* move to the next one.

---

# Технологии | Technologies

* Unity 6 (6000.4.4f1)
* C#
* TextMeshPro
* GitHub
* Cursor AI
* ChatGPT

---

# Платформы | Platforms

Планируемые платформы:

* Android
* iPhone (iOS)

Планируемый релиз:

* RuStore

---

# Текущий статус | Current Status

Проект находится в активной разработке.

Сейчас реализовано:

* главное меню;
* система раскрашивания;
* палитра цветов;
* переход между уровнями;
* экран завершения уровня;
* сохранение прогресса;
* 3 игровых уровня, включая «Котик на круге» (Android-проверка третьего пока не завершена).

---

# Планы | Roadmap

До релиза планируется:

* добавить 20–40 уровней;
* улучшить интерфейс;
* добавить больше иллюстраций;
* улучшить визуальные эффекты;
* оптимизировать мобильную производительность;
* подготовить игру к публикации в RuStore.

---

# Стиль игры | Visual Style

* детский;
* уютный;
* приятные цвета;
* мультяшный стиль;
* котики, цветы, листья и милые элементы.

---

# Структура сцен | Scenes

Текущие сцены:

* MainMenu
* Level_001_New
* Level_002_New
* Level_003_New — «Котик на круге»

## Карта ресурсов и создание уровней

* `Assets/_Project/Scenes/` — меню и включённые игровые сцены.
* `Assets/_Project/Art/Levels/Level_003/` — игровые Outline, Zones и Previews третьего уровня.
* `ArtSources/Level_003/Original/` — неизменённые копии графических исходников.
* `ArtSources/Level_003/level_manifest.json` — описание уровня и результаты проверок.
* `Tools/LevelCreation/` — подготовка PNG, проверки и единая команда импорта.
* `Assets/_Project/Editor/LevelCreation/` — инструменты открытого Unity Editor.

Вход для агентов: [AGENTS.md](AGENTS.md), [короткий индекс карты](docs/INDEX.md).
Инструкции: [создание уровней](docs/LEVEL_PIPELINE.md),
[проверки](LEVEL_CREATION_CHECKLIST.md), [короткий промт](LEVEL_CREATION_PROMPT.md).
Генерация следующего уровня запускается одной командой PowerShell; Play Mode и Android-сборка проверяются отдельно.
Оперативные отчёты и обмен через подключённый Drive: [AGENT_SYNC_GUIDE](docs/AGENT_SYNC_GUIDE.md).
AgentSync исключён из Git; commit/push выполняются только по отдельной команде.

---

# Примечание | Note

Это первый Unity-проект разработчика, созданный для изучения разработки мобильных игр и построения полноценного игрового пайплайна от идеи до релиза.
