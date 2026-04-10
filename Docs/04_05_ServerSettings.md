# 04.05 — Серверные настройки (ServerSettings) 🖥️

## Что мы делаем?

Создаём **серверные настройки** — конфигурацию, которая контролируется хостом и реплицируется на всех клиентов. Пока здесь одна настройка: показывать ли «developer entities» в спавн-меню.

## Как это работает?

### Replicated ConVar

```csharp
[ConVar( "sb.developer_entities", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server )]
public static bool DeveloperEntities { get; set; } = false;
```

- `ConVarFlags.Replicated` — значение автоматически синхронизируется с сервера на все клиенты
- `ConVarFlags.GameSetting` — показывается в настройках игры (Game Settings)
- `ConVarFlags.Server` — только сервер может менять
- По умолчанию `false` — developer entities скрыты

### ShowDeveloperEntities

```csharp
public static bool ShowDeveloperEntities => Game.IsEditor || DeveloperEntities;
```

В редакторе developer entities **всегда видны** (для удобства разработки). На сервере — только если хост включил.

## Создай файл

Путь: `Code/GameLoop/ServerSettings.cs`

```csharp
/// <summary>
/// Server-controlled settings replicated to all clients.
/// </summary>
public static class ServerSettings
{
	/// <summary>
	/// When enabled, developer entities are visible in the spawn menu for all players.
	/// Always on in the editor regardless of this value.
	/// </summary>
	[Title( "Show Developer Entities" )]
	[Group( "Other" )]
	[ConVar( "sb.developer_entities", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server, Help = "Show developer entities in the spawn menu." )]
	public static bool DeveloperEntities { get; set; } = false;

	/// <summary>
	/// Returns true if developer entities should be shown.
	/// </summary>
	public static bool ShowDeveloperEntities => Game.IsEditor || DeveloperEntities;
}
```

## Разница: GamePreferences vs ServerSettings

| | GamePreferences | ServerSettings |
|--|----------------|---------------|
| Кто контролирует | Каждый игрок сам | Только хост |
| Синхронизация | UserInfo (клиент → сервер) | Replicated (сервер → клиенты) |
| Сохранение | У каждого клиента | На сервере |
| Пример | AutoSwitch, Screenshake | DeveloperEntities |

## Проверка

1. Хост: `sb.developer_entities 1` → все клиенты видят developer entities
2. В редакторе → developer entities видны всегда
3. Клиент пытается поменять → не может (Server flag)

## 🎉 Фаза 4 завершена!

Игровой цикл документирован. Следующая фаза — **Фаза 5: Компоненты**.
