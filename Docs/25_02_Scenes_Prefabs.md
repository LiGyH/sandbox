# 25.02 — Сцены и префабы

## Что мы делаем?

Настраиваем **сцены** (scenes) и **префабы** (prefabs) — финальные JSON-файлы, которые связывают весь код в рабочую игру. Без них движок не знает, какие объекты создавать при запуске.

## Как это работает внутри движка

### Сцены (Scenes)
Сцена в s&box — это JSON-файл (`.scene`), который описывает **дерево игровых объектов** (`GameObject`). Каждый объект содержит:
- Позицию, вращение, масштаб
- Список компонентов (`Component`)
- Дочерние объекты

При запуске игры движок загружает **стартовую сцену** (указана в `sandbox.sbproj` → `Metadata.StartupScene`).

### Префабы (Prefabs)
Префаб — это **шаблон** игрового объекта (`.prefab`). Он похож на сцену, но содержит **один** объект с его компонентами. Префабы можно инстанцировать (создавать копии) в рантайме через `SceneUtility.Instantiate()`.

## Структура файлов

```
Assets/
├── scenes/
│   ├── sandbox.scene          ← главная игровая сцена
│   ├── system.scene           ← системная сцена (HUD, глобальные системы)
│   ├── tools.scene            ← сцена для тестирования инструментов
│   ├── npc_testing.scene      ← сцена для тестирования NPC
│   └── zoo.scene              ← сцена-зоопарк (демонстрация)
└── prefabs/
    └── engine/
        ├── player.prefab      ← префаб игрока
        ├── spawn-progress.prefab  ← индикатор спавна
        └── spawnpoint.prefab  ← точка спавна
```

## Главная сцена: sandbox.scene

Это минимальная сцена, которая загружает карту:

```json
{
  "__guid": "325a4042-0696-43dd-a79d-dcc314299ba3",
  "GameObjects": [
    {
      "__guid": "f7192e2f-1093-4cca-a93c-40cb86e20686",
      "__version": 2,
      "Flags": 0,
      "Name": "MapLoader",
      "Position": "0,0,0",
      "Rotation": "0,0,0,1",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": true,
      "NetworkMode": 2,
      "NetworkFlags": 0,
      "NetworkOrphaned": 0,
      "NetworkTransmit": true,
      "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.MapInstance",
          "__guid": "50042ecd-9dd2-457a-837c-0cda534d9eb8",
          "__enabled": true,
          "Flags": 0,
          "__version": 1,
          "EnableCollision": true,
          "MapName": "facepunch.flatgrass",
          "NoOrigin": false,
          "OnComponentDestroy": null,
          "OnComponentDisabled": null,
          "OnComponentEnabled": null,
          "OnComponentFixedUpdate": null,
          "OnComponentStart": null,
          "OnComponentUpdate": null,
          "OnMapLoaded": null,
          "OnMapUnloaded": null,
          "UseMapFromLaunch": true
        }
      ],
      "Children": []
    }
  ],
  "SceneProperties": {
    "NetworkInterpolation": true,
    "TimeScale": 1,
    "WantsSystemScene": true,
    "Metadata": {},
    "NavMesh": {
      "Enabled": true,
      "IncludeStaticBodies": true,
      "IncludeKeyframedBodies": true,
      "EditorAutoUpdate": true,
      "AgentHeight": 64,
      "AgentRadius": 16,
      "AgentStepSize": 18,
      "AgentMaxSlope": 40,
      "ExcludedBodies": "",
      "IncludedBodies": ""
    }
  },
  "ResourceVersion": 3,
  "Title": null,
  "Description": null,
  "__references": [],
  "__version": 3
}
```

## Разбор сцены

### Объект MapLoader
- **`Name: "MapLoader"`** — имя объекта в иерархии
- **`NetworkMode: 2`** — объект реплицируется по сети (все клиенты видят одну карту)
- **`Components`** — список компонентов:
  - **`Sandbox.MapInstance`** — встроенный компонент движка, загружает карту
    - `MapName: "facepunch.flatgrass"` — карта по умолчанию
    - `UseMapFromLaunch: true` — если сервер запущен с другой картой, использовать её

### SceneProperties
- **`NetworkInterpolation: true`** — плавная интерполяция сетевых объектов
- **`TimeScale: 1`** — нормальная скорость времени
- **`WantsSystemScene: true`** — загружать системную сцену параллельно
- **`NavMesh`** — настройки навигационной сетки для NPC:
  - `AgentHeight: 64` — высота NPC-агента
  - `AgentRadius: 16` — радиус NPC
  - `AgentStepSize: 18` — максимальная высота ступеньки
  - `AgentMaxSlope: 40` — максимальный угол наклона

## Настройка в sbproj

В файле `sandbox.sbproj` стартовая сцена указана так:

```json
{
  "Metadata": {
    "StartupScene": "scenes/sandbox.scene"
  }
}
```

## Как создать сцену в редакторе

1. Откройте s&box Editor
2. **File → New Scene**
3. Добавьте объект с компонентом `MapInstance`
4. Сохраните как `Assets/scenes/sandbox.scene`
5. В настройках проекта укажите эту сцену как стартовую

## Как создать префаб

1. Создайте `GameObject` в сцене
2. Добавьте нужные компоненты
3. Перетащите объект в папку `Assets/prefabs/`
4. Движок автоматически создаст `.prefab` файл

## Что проверить

1. Файл `Assets/scenes/sandbox.scene` существует и содержит объект `MapLoader`
2. Файл `Assets/prefabs/engine/player.prefab` существует
3. В `sandbox.sbproj` указана правильная стартовая сцена
4. При запуске игры загружается карта `facepunch.flatgrass`
