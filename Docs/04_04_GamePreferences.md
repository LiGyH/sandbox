# 04.04 — Игровые настройки (GamePreferences) ⚙️

## Что мы делаем?

Создаём **настройки игрока** — статический класс с ConVar'ами, которые сохраняются между сессиями. Влияют на автопереключение оружия, быстрый выбор, тряску камеры и view bobbing.

## Как это работает?

### ConVar — консольная переменная

```csharp
[ConVar( "sb.autoswitch", ConVarFlags.UserInfo | ConVarFlags.Saved )]
public static bool AutoSwitch { get; set; } = true;
```

- `"sb.autoswitch"` — имя переменной в консоли
- `ConVarFlags.UserInfo` — отправляется на сервер (сервер знает настройки клиента)
- `ConVarFlags.Saved` — сохраняется в конфиге между сессиями
- `= true` — значение по умолчанию

### Все настройки

| Переменная | Тип | По умолчанию | Назначение |
|-----------|-----|-------------|-----------|
| `sb.autoswitch` | bool | true | Автопереключение на лучшее оружие при подборе |
| `sb.fastswitch` | bool | false | Быстрый выбор оружия (без подтверждения) |
| `sb.viewbob` | bool | true | Наклон камеры при движении |
| `sb.screenshake` | float | 0.3 | Интенсивность тряски камеры (0.1–2.0) |

### Использование в коде

```csharp
// В PlayerInventory.cs
if ( !GamePreferences.AutoSwitch ) return false;

// В Player.Camera.cs
if ( !GamePreferences.ViewBobbing ) return;

// В EnvironmentShake.cs
shake *= GamePreferences.Screenshake;
```

## Создай файл

Путь: `Code/GameLoop/GamePreferences.cs`

```csharp
/// <summary>
/// The local user's preferences in Deathmatch
/// </summary>
public static class GamePreferences
{
	/// <summary>
	/// Enables automatic switching to better weapons on item pickup
	/// </summary>
	[ConVar( "sb.autoswitch", ConVarFlags.UserInfo | ConVarFlags.Saved )]
	public static bool AutoSwitch { get; set; } = true;

	/// <summary>
	/// Enables fast switching between inventory weapons
	/// </summary>
	[ConVar( "sb.fastswitch", ConVarFlags.Saved )]
	public static bool FastSwitch { get; set; } = false;

	/// <summary>
	/// Intensity of your camera's screenshake
	/// </summary>
	[ConVar( "sb.viewbob", ConVarFlags.Saved )]
	[Group( "Camera" )]
	public static bool ViewBobbing { get; set; } = true;

	/// <summary>
	/// Intensity of your camera's screenshake
	/// </summary>
	[ConVar( "sb.screenshake", ConVarFlags.Saved )]
	[Range( 0.1f, 2f ), Step( 0.1f ), Group( "Camera" )]
	public static float Screenshake { get; set; } = 0.3f;
}
```

## Ключевые концепции

### static class — без экземпляра

`GamePreferences` — `static class`. Нельзя создать экземпляр. Все свойства доступны через `GamePreferences.AutoSwitch`. Это правильно для глобальных настроек.

### ConVarFlags

| Флаг | Назначение |
|------|-----------|
| `Saved` | Сохраняется в файле конфигурации |
| `UserInfo` | Отправляется на сервер |
| `Server` | Управляется сервером |
| `Cheat` | Работает только с sv_cheats |
| `Replicated` | Реплицируется с сервера на клиенты |

### [Range] и [Step]

```csharp
[Range( 0.1f, 2f ), Step( 0.1f )]
public static float Screenshake { get; set; } = 0.3f;
```

В настройках s&box Editor отображается как слайдер от 0.1 до 2.0 с шагом 0.1.

## Проверка

1. Консоль: `sb.autoswitch 0` → автопереключение оружия выключено
2. Перезапусти игру → значение сохранено
3. Консоль: `sb.screenshake 2` → камера трясётся сильнее

## Следующий файл

Переходи к **04.05 — Серверные настройки (ServerSettings)**.
