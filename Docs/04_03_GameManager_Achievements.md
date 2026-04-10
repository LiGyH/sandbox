# 04.03 — Ачивки (GameManager.Achievements) 🏆

## Что мы делаем?

Добавляем **проверку достижений** при подключении игроков. Основная ачивка: «Играть на одном сервере с Гарри Ньюманом» (создателем s&box).

## Как это работает?

### Магический SteamID

```csharp
private const long ChosenSteamId = 76561197960279927L;
```

Это SteamID Гарри Ньюмана (Garry Newman) — создателя Garry's Mod и s&box. Если вы окажетесь на одном сервере — получите ачивку.

### Логика проверки

```csharp
private void CheckConnectionAchievement( Connection newConnection )
{
    // Случай 1: Гарри сам подключился → дать ачивку ВСЕМ
    if ( newConnection.SteamId == ChosenSteamId )
    {
        Grant();  // Rpc.Broadcast → все получают ачивку
    }
    else
    {
        // Случай 2: Подключился обычный игрок, но Гарри УЖЕ на сервере
        if ( Connection.All.Any( c => c.SteamId == ChosenSteamId ) )
        {
            // Дать ачивку ТОЛЬКО новому игроку
            using ( Rpc.FilterInclude( newConnection ) )
            {
                Grant();
            }
        }
    }
}
```

### Статистика друзей

```csharp
[Rpc.Broadcast( NetFlags.HostOnly )]
private static void CheckFriendsOnlineStat()
{
    var friendCount = Connection.All.Count( c => 
        c.SteamId != Connection.Local.SteamId && new Friend( c.SteamId ).IsFriend );
    Sandbox.Services.Stats.SetValue( "social.friends.max", friendCount );
}
```

При каждом подключении пересчитываем: сколько друзей из Steam на сервере? Записываем максимум в статистику. `Rpc.Broadcast` → каждый клиент считает своих друзей.

## Создай файл

Путь: `Code/GameLoop/GameManager.Achievements.cs`

```csharp
public sealed partial class GameManager
{
	private const long ChosenSteamId = 76561197960279927L;

	private void CheckConnectionAchievement( Connection newConnection )
	{
		bool isMatched = newConnection.SteamId == ChosenSteamId;

		if ( isMatched )
		{
			Grant();
		}
		else
		{
			// Someone else joined, check if they're here
			if ( Connection.All.Any( c => c.SteamId == ChosenSteamId ) )
			{
				// Grant only to the player who just joined
				using ( Rpc.FilterInclude( newConnection ) )
				{
					Grant();
				}
			}
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void Grant()
	{
		Sandbox.Services.Achievements.Unlock( "garry_in_server" );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void CheckFriendsOnlineStat()
	{
		var friendCount = Connection.All.Count( c => c.SteamId != Connection.Local.SteamId && new Friend( c.SteamId ).IsFriend );
		Sandbox.Services.Stats.SetValue( "social.friends.max", friendCount );
	}
}
```

## Ключевые концепции

### Rpc.FilterInclude

```csharp
using ( Rpc.FilterInclude( newConnection ) )
{
    Grant();
}
```

`Rpc.FilterInclude` ограничивает следующий Rpc.Broadcast только указанным подключением. Без него ачивку получили бы все — а нужно только новичку.

### SetValue vs Increment

- `Stats.Increment` — **добавляет** к счётчику
- `Stats.SetValue` — **устанавливает** максимум (если новое значение > текущего)

Для `social.friends.max` используем `SetValue`: нас интересует максимальное количество друзей одновременно на сервере.

## Проверка

1. Зайди на сервер с Гарри → получишь ачивку «garry_in_server»
2. Зайди на сервер с другом → `social.friends.max` обновится

## Следующий файл

Переходи к **04.04 — Игровые настройки (GamePreferences)**.
