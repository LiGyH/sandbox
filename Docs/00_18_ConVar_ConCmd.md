# 00.18 — ConVar и ConCmd (консоль)

## Что мы делаем?

Разбираемся в **консоли** s&box — как сделать свои команды и переменные. В Sandbox через ConVar настраивается всё: урон оружия, гравитация в невесомости, разрешённость NPC, лимиты спавна. Через ConCmd — действия (`undo`, `cleanup`, `noclip`, `thirdperson`).

## Открыть консоль в игре

Клавиша `~` (тильда) во время игры. Сверху появляется строка ввода.

## ConVar — консольная переменная

Это **статическое свойство**, которое можно поменять из консоли, и которое (при нужных флагах) сохраняется между сессиями или синхронизируется между клиентом и сервером.

```csharp
[ConVar]
public static bool debug_bullets { get; set; } = false;
```

Теперь в консоли работает:

```
] debug_bullets                 // выведет текущее значение
] debug_bullets 1               // включит
] debug_bullets 0               // выключит
```

### Типы значений

`ConVar` поддерживает базовые типы: `bool`, `int`, `float`, `string`. Для более сложных — придётся парсить самому через `ConCmd`.

### Имя

По умолчанию имя — как у свойства. Можно задать явно:

```csharp
[ConVar( "bullet_count" )]
public static int BulletCount { get; set; } = 6;
```

### Флаги

Комбинируются через `|`:

```csharp
// Сохраняется на диск, переживает перезапуск
[ConVar( "crosshair_size", ConVarFlags.Saved )]
public static float CrosshairSize { get; set; } = 1.0f;

// Только хост может менять, значение синхронизируется клиентам
[ConVar( "friendly_fire", ConVarFlags.Replicated )]
public static bool FriendlyFire { get; set; } = false;

// Летит на сервер как информация о пользователе
[ConVar( "view_mode", ConVarFlags.UserInfo )]
public static string ViewMode { get; set; } = "firstperson";

// Не показывается в find и автокомплите
[ConVar( "secret_dev_flag", ConVarFlags.Hidden )]
public static int SecretFlag { get; set; } = 0;
```

## ConCmd — консольная команда

**Статический метод** с атрибутом:

```csharp
[ConCmd( "hello" )]
public static void HelloCommand()
{
    Log.Info( "Hello there!" );
}
```

`] hello` → в консоли появится «Hello there!».

### Команды с аргументами

```csharp
[ConCmd( "hello" )]
public static void HelloCommand( string name )
{
    Log.Info( $"Hello there {name}!" );
}
```

`] hello dave` → «Hello there dave!».

Движок сам конвертирует строки в нужные типы (`int`, `float`, `bool` и т.п.). Если строка с пробелами — оберни в кавычки:

```
] hello "Mr Smith"
```

### Серверные команды

Если команда должна запускаться **всегда на сервере** (например, `kick`, `cleanup_all`), добавь флаг:

```csharp
[ConCmd( "kick", ConVarFlags.Server )]
public static void Kick( Connection caller, string targetName )
{
    Log.Info( $"{caller.DisplayName} хочет кикнуть {targetName}" );
    // ... логика
}
```

Если **первый параметр** — `Connection`, он будет автоматически заполнен соединением того, кто вызвал команду. Удобно для проверки прав.

### Клиентские команды

По умолчанию команды вызываются **у того, кто их ввёл**. Если клиент вводит команду, она выполняется на клиенте. Если ей нужен доступ к серверной логике — добавь `ConVarFlags.Server`.

## Практика в Sandbox

Примеры команд из Sandbox, которые ты встретишь позже:

```csharp
[ConCmd( "undo" )]
public static void UndoCmd() => UndoSystem.Current.Undo();

[ConCmd( "cleanup" )]
public static void CleanupCmd() => CleanupSystem.Current.CleanAll();

[ConCmd( "noclip" )]
public static void NoclipCmd() => Player.Local?.ToggleNoclip();
```

Переменные:

```csharp
[ConVar( "sv_gravity", ConVarFlags.Replicated )]
public static float Gravity { get; set; } = 800f;

[ConVar( "sandbox_max_props", ConVarFlags.Saved )]
public static int MaxProps { get; set; } = 300;
```

## Вывод в консоль

| Метод | Когда |
|---|---|
| `Log.Info( "..." )` | Обычная информация (белый) |
| `Log.Warning( "..." )` | Предупреждение (жёлтый) |
| `Log.Error( "..." )` | Ошибка (красный) |
| `Log.Trace( "..." )` | Подробный отладочный вывод |

С интерполяцией:

```csharp
Log.Info( $"Игрок {player.Name} убил {victim.Name}" );
```

## Полезные встроенные команды

| Команда | Что делает |
|---|---|
| `help <name>` | Информация о ConVar/ConCmd |
| `find <substr>` | Поиск команд по подстроке |
| `hotload_log 2` | Подробный лог hotload |
| `connect <ip>` | Присоединиться к серверу |
| `disconnect` | Отключиться |
| `host_timescale 0.5` | Замедлить игру (если не отключено) |

## Результат

После этого этапа ты знаешь:

- ✅ Что такое ConVar и как его объявить.
- ✅ Что такое ConCmd и как передавать в него аргументы.
- ✅ Флаги `Saved`, `Replicated`, `UserInfo`, `Server`, `Hidden` — что они делают.
- ✅ Как определить вызвавшего команду через `Connection caller`.
- ✅ Как логировать через `Log.Info`/`Warning`/`Error`.

---

📚 **Facepunch docs:** [code/code-basics/console-variables.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/code/code-basics/console-variables.md)

**Следующий шаг:** [00.19 — Input (ввод): основы](00_19_Input_Basics.md)
