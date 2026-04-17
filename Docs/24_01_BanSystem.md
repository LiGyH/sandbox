# 24.01 — Система банов (BanSystem) 🔨

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.25 — События сети](00_25_Network_Events.md)

## Что мы делаем?

Создаём **BanSystem** — систему, которая хранит список забаненных игроков, проверяет их при подключении и позволяет банить/разбанивать через консольную команду.

## Как это работает?

`BanSystem` — это `GameObjectSystem`, который живёт на уровне сцены (один экземпляр на всю игру). Он реализует интерфейс `INetworkListener`, что позволяет перехватывать попытки подключения и отклонять забаненных игроков.

Баны хранятся локально через `LocalData` (см. [01.06](01_06_LocalData.md)) — словарь `SteamId → BanEntry`. При каждом изменении список сохраняется на диск.

### Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| `GameObjectSystem<T>` | Синглтон-система движка, создаётся один раз при загрузке сцены |
| `INetworkListener` | Интерфейс движка — метод `AcceptConnection` вызывается при каждом подключении |
| `LocalData` | Локальное хранилище данных хоста (файл на диске) |
| `Connection` | Представляет подключённого игрока (содержит SteamId, DisplayName) |

### Поток работы

1. При создании системы — загружаем баны из `LocalData`
2. При подключении игрока — проверяем его `SteamId` в словаре
3. Если забанен — отклоняем с сообщением причины
4. Команда `ban` — ищет игрока по имени или SteamId, банит и кикает

## Создай файл

Путь: `Code/Game/BanSystem/BanSystem.cs`

```csharp
using Sandbox.UI;

/// <summary>
/// Holds a banlist, can ban users
/// </summary>
public sealed class BanSystem : GameObjectSystem<BanSystem>, Component.INetworkListener
{
	public record struct BanEntry( string DisplayName, string Reason );

	private Dictionary<long, BanEntry> _bans = new();

	public BanSystem( Scene scene ) : base( scene )
	{
		_bans = LocalData.Get<Dictionary<long, BanEntry>>( "bans", new() ) ?? new();
	}

	bool Component.INetworkListener.AcceptConnection( Connection connection, ref string reason )
	{
		if ( !_bans.TryGetValue( connection.SteamId, out var entry ) )
			return true;

		reason = $"You're banned from this server: {entry.Reason}";
		return false;
	}

	/// <summary>
	/// Bans a connected player and kicks them immediately
	/// </summary>
	public void Ban( Connection connection, string reason )
	{
		Assert.True( Networking.IsHost, "Only the host may ban players." );

		_bans[connection.SteamId] = new BanEntry( connection.DisplayName, reason );
		Save();
		Scene.Get<Chat>()?.AddSystemText( $"{connection.DisplayName} was banned: {reason}", "🔨" );
		connection.Kick( reason );
	}

	/// <summary>
	/// Bans a Steam ID by value. Use for pre-banning or banning players who are not currently connected.
	/// Display name falls back to the Steam ID string.
	/// </summary>
	public void Ban( SteamId steamId, string reason )
	{
		Assert.True( Networking.IsHost, "Only the host may ban players." );

		_bans[steamId] = new BanEntry( steamId.ToString(), reason );
		Save();
	}

	/// <summary>
	/// Removes the ban for the given Steam ID.
	/// </summary>
	public void Unban( SteamId steamId )
	{
		Assert.True( Networking.IsHost, "Only the host may unban players." );

		if ( _bans.Remove( steamId ) )
			Save();
	}

	/// <summary>
	/// Returns true if the given Steam ID is currently banned
	/// </summary>
	public bool IsBanned( SteamId steamId ) => _bans.ContainsKey( steamId );

	/// <summary>
	/// Returns a read-only view of all active bans
	/// </summary>
	public IReadOnlyDictionary<SteamId, BanEntry> GetBannedList() => _bans.ToDictionary( x => (SteamId)x.Key, x => x.Value );

	private void Save() => LocalData.Set( "bans", _bans );

	/// <summary>
	/// Bans a player by name or Steam ID. Optionally provide a reason.
	/// Usage: ban [name|steamid] [reason]
	/// </summary>
	[ConCmd( "ban" )]
	public static void BanCommand( string target, string reason = "Banned" )
	{
		if ( !Networking.IsHost ) return;

		// Try parsing as a Steam ID (64-bit integer) first
		if ( ulong.TryParse( target, out var steamIdValue ) )
		{
			var steamId = steamIdValue;
			var connection = Connection.All.FirstOrDefault( c => c.SteamId == steamId );

			if ( connection is not null )
				Current.Ban( connection, reason );
			else
				Current.Ban( steamId, reason );

			Log.Info( $"Banned {steamId}: {reason}" );
			return;
		}

		// Fall back to partial name match
		var conn = GameManager.FindPlayerWithName( target );
		if ( conn is not null )
		{
			Current.Ban( conn, reason );
			Log.Info( $"Banned {conn.DisplayName}: {reason}" );
		}
		else
		{
			Log.Warning( $"Could not find player '{target}'" );
		}
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `record struct BanEntry` | Компактная запись: имя игрока + причина бана |
| `LocalData.Get<T>("bans")` | Загружает баны из файла при запуске |
| `AcceptConnection` | Вызывается движком при подключении — возвращает `false` для бана |
| `connection.Kick(reason)` | Немедленно отключает игрока с сообщением |
| `[ConCmd("ban")]` | Регистрирует консольную команду `ban` |
| `GameManager.FindPlayerWithName` | Ищет подключение по частичному имени |

## Результат

После создания этого файла:
- Хост может банить игроков командой `ban имя причина`
- Забаненные не смогут подключиться к серверу
- Баны сохраняются между перезапусками

---

Следующий шаг: [11.02 — Система управления (ControlSystem)](17_01_ControlSystem.md)
