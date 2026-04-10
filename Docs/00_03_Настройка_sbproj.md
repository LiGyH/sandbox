# 00.03 — Настройка проекта (sandbox.sbproj)

## Что мы делаем?

Настраиваем **главный файл проекта** — `sandbox.sbproj`.  
Этот файл говорит движку всё о нашей игре: название, тип, количество игроков, стартовую сцену.

## Что такое .sbproj?

Это **JSON файл** (текстовый файл с данными в специальном формате).  
JSON выглядит как набор пар `"ключ": "значение"`, вложенных в фигурные скобки `{}`.

## Шаг 1: Открой файл sandbox.sbproj

Он лежит в **корне проекта** (в самой главной папке).  
Открой его любым текстовым редактором (Блокнот, VS Code, или прямо в редакторе s&box).

## Шаг 2: Замени содержимое на это

```json
{
  "Title": "Sandbox",
  "Type": "game",
  "Org": "facepunch",
  "Ident": "sandbox",
  "Schema": 1,
  "IncludeSourceFiles": false,
  "Resources": "/fonts/*\n/UI/*.png\n/ui/*.png\n/ui/*\n/ui/weapons/melee/*",
  "PackageReferences": [],
  "EditorReferences": null,
  "Mounts": [],
  "IsStandaloneOnly": false,
  "Metadata": {
    "MaxPlayers": 32,
    "MinPlayers": 1,
    "TickRate": 50,
    "GameNetworkType": "Multiplayer",
    "MapSelect": "Unrestricted",
    "MapList": [
      "facepunch.flatgrass"
    ],
    "RankType": "None",
    "PerMapRanking": false,
    "LeaderboardType": "None",
    "CsProjName": "",
    "StartupScene": "scenes/sandbox.scene",
    "ControlModes": {
      "Keyboard": true,
      "VR": false,
      "Gamepad": true
    },
    "LaunchMode": "Launcher",
    "MapStartupScene": "scenes/sandbox.scene",
    "DedicatedServerStartupScene": "scenes/sandbox.scene",
    "SystemScene": "scenes/system.scene"
  }
}
```

## Что означает каждая строка?

| Поле | Значение | Объяснение |
|------|----------|------------|
| `"Title"` | `"Sandbox"` | Название игры, видимое игрокам |
| `"Type"` | `"game"` | Тип проекта: `game` (игра), `addon` (дополнение), `library` (библиотека) |
| `"Org"` | `"facepunch"` | Организация-владелец |
| `"Ident"` | `"sandbox"` | Уникальный идентификатор проекта |
| `"Schema"` | `1` | Версия формата файла |
| `"IncludeSourceFiles"` | `false` | Не включать исходники в публикацию |
| `"Resources"` | строка | Какие файлы из `Assets/` включать в сборку. `\n` — перенос строки |
| `"MaxPlayers"` | `32` | Максимум 32 игрока на сервере |
| `"MinPlayers"` | `1` | Можно играть одному |
| `"TickRate"` | `50` | 50 обновлений сервера в секунду |
| `"GameNetworkType"` | `"Multiplayer"` | Мультиплеер-игра |
| `"MapSelect"` | `"Unrestricted"` | Любая карта может быть выбрана |
| `"StartupScene"` | `"scenes/sandbox.scene"` | Главная сцена при запуске |
| `"SystemScene"` | `"scenes/system.scene"` | Системная сцена (для UI) |
| `"ControlModes"` | объект | Поддержка клавиатуры и геймпада (VR выключен) |
| `"LaunchMode"` | `"Launcher"` | Запуск через лаунчер |

> **Как это работает внутри движка:**  
> При запуске игры движок вызывает `Sandbox.AppSystem` (см. исходники `engine/Sandbox.AppSystem/`),  
> который читает `.sbproj` и создаёт **GameInstance** (`engine/Sandbox.GameInstance/`).  
> 
> `TickRate: 50` означает, что метод **FixedUpdate** вызывается 50 раз в секунду.  
> Это совпадает с настройкой физики (которую мы настроим далее).
>
> `StartupScene` указывает, какой `.scene` файл загружать первым.  
> Сцена содержит все объекты на карте (игроки, камеры, свет, и т.д.).

## Результат

После этого этапа:
- ✅ Файл `sandbox.sbproj` настроен
- ✅ Игра знает что она мультиплеер на 32 игрока
- ✅ Указаны стартовые сцены

---

**Следующий шаг:** [00_04 — Настройки физики](00_04_Настройки_физики.md)
