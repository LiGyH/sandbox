# 04.03 — Утилиты GameManager: кик игроков (GameManager.Util) 🥾

## Что мы делаем?

Добавляем **систему кика** — возможность выгнать игрока с сервера по имени или SteamID. Это partial-файл `GameManager`.

## Как это работает?

### Отслеживание кикнутых

```csharp
private readonly HashSet<Guid> _kickedPlayers = new();
```

Когда игрок кикается, его `Connection.Id` добавляется в `_kickedPlayers`. При отключении (`OnDisconnected`) проверяем: если игрок в этом списке — не показываем сообщение «X has left» (уже показали «X was kicked»).

### Кик по Connection

```csharp
public void Kick( Connection connection, string reason = "Kicked" )
{
    _kickedPlayers.Add( connection.Id );
    Scene.Get<Chat>()?.AddSystemText( $"{connection.DisplayName} was kicked: {reason}", "🥾" );
    connection.Kick( reason );
}
```

### Консольная команда

```csharp
[ConCmd( "kick" )]
public static void KickCommand( string target, string reason = "Kicked" )
```

Два способа указать цель:
1. **По SteamID**: `kick 76561198012345678 "Bad behavior"`
2. **По имени**: `kick "PlayerName" "Reason"` (частичное совпадение)

### Поиск по имени

```csharp
public static Connection FindPlayerWithName( string name, bool partial = true )
{
    return Connection.All.FirstOrDefault( c =>
        partial
            ? c.DisplayName.Contains( name, StringComparison.OrdinalIgnoreCase )
            : c.DisplayName.Equals( name, StringComparison.OrdinalIgnoreCase )
    );
}
```

`StringComparison.OrdinalIgnoreCase` — регистронезависимый поиск. `partial = true` — достаточно части имени.

## Создай файл

Путь: `Code/GameLoop/GameManager.Util.cs`

```csharp
using Sandbox.UI;

public sealed partial class GameManager
{
	private readonly HashSet<Guid> _kickedPlayers = new();

	public static Connection FindPlayerWithName( string name, bool partial = true )
	{
		return Connection.All.FirstOrDefault( c =>
			partial
				? c.DisplayName.Contains( name, StringComparison.OrdinalIgnoreCase )
				: c.DisplayName.Equals( name, StringComparison.OrdinalIgnoreCase )
		);
	}

	/// <summary>
	/// Kicks a connected player with an optional reason.
	/// </summary>
	public void Kick( Connection connection, string reason = "Kicked" )
	{
		Assert.True( Networking.IsHost, "Only the host may kick players." );

		_kickedPlayers.Add( connection.Id );
		Scene.Get<Chat>()?.AddSystemText( $"{connection.DisplayName} was kicked: {reason}", "🥾" );
		connection.Kick( reason );
	}

	/// <summary>
	/// Kicks a player by name or Steam ID. Optionally provide a reason.
	/// Usage: kick [name|steamid] [reason]
	/// </summary>
	[ConCmd( "kick" )]
	public static void KickCommand( string target, string reason = "Kicked" )
	{
		if ( !Networking.IsHost ) return;

		if ( ulong.TryParse( target, out var steamIdValue ) )
		{
			var connection = Connection.All.FirstOrDefault( c => c.SteamId == steamIdValue );
			if ( connection is not null )
			{
				Current.Kick( connection, reason );
				Log.Info( $"Kicked {connection.DisplayName}: {reason}" );
			}
			else
			{
				Log.Warning( $"Could not find player with Steam ID '{target}'" );
			}
			return;
		}

		var conn = FindPlayerWithName( target );
		if ( conn is not null )
		{
			Current.Kick( conn, reason );
			Log.Info( $"Kicked {conn.DisplayName}: {reason}" );
		}
		else
		{
			Log.Warning( $"Could not find player '{target}'" );
		}
	}
}
```

## Проверка

1. Консоль: `kick "PlayerName"` → игрок выгнан, в чате «PlayerName was kicked: Kicked»
2. Консоль: `kick 76561198012345678 "Cheating"` → кик по SteamID
3. Кикнутый игрок видит причину при отключении

## Следующий файл

Переходи к **04.03 — Ачивки (GameManager.Achievements)**.
