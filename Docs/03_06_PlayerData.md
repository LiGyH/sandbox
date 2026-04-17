# 03.06 — Данные игрока (PlayerData) 📊

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём компонент **PlayerData** — постоянное хранилище данных об игроке: SteamId, имя, убийства, смерти, режим бога, логика респавна. В отличие от `Player` (который уничтожается при смерти), `PlayerData` живёт всю сессию.

## Зачем два компонента: Player и PlayerData?

| | Player | PlayerData |
|--|--------|-----------|
| Жизненный цикл | Создаётся при спавне, уничтожается при смерти | Живёт всю сессию |
| Данные | Здоровье, броня, ввод | Kills, Deaths, SteamId, GodMode |
| Сеть | На объекте игрока | На отдельном объекте |

Когда игрок умирает, `Player.GameObject` уничтожается. Но мы не хотим терять счёт убийств или Steam-имя. Поэтому `PlayerData` хранится на отдельном GameObject и переживает смерть.

## Как это работает?

### Основные свойства

```csharp
[Property] public Guid PlayerId { get; set; }      // Уникальный ID = Connection.Id
[Property] public long SteamId { get; set; }        // Steam ID
[Property] public string DisplayName { get; set; }  // Имя игрока

[Sync] public int Kills { get; set; }       // Убийства
[Sync] public int Deaths { get; set; }      // Смерти
[Sync] public bool IsGodMode { get; set; }  // Режим бога
```

`[Sync]` без `SyncFlags.FromHost` — значит значение синхронизируется от владельца. Но `Kills`/`Deaths` меняются только на хосте, так что это безопасно.

### Статические хелперы

```csharp
public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();
public static PlayerData For( Connection connection ) => For( connection.Id );
public static PlayerData For( Guid playerId ) => All.FirstOrDefault( x => x.PlayerId == playerId );
```

Удобный доступ к данным любого игрока из любого места кода.

### Система респавна

```csharp
private bool _needsRespawn;
private RealTimeSince _timeSinceDied;

public void MarkForRespawn()          // Вызывается при смерти
{
    _needsRespawn = true;
    _timeSinceDied = 0;
}

protected override void OnUpdate()    // Каждый кадр на хосте
{
    if ( !_needsRespawn ) return;
    if ( _timeSinceDied < 4f ) return; // ждём 4 секунды
    RequestRespawn();                  // автореспавн
}

[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
public void RequestRespawn()          // Можно вызвать раньше (кнопка)
{
    _needsRespawn = false;
    // удалить все PlayerObserver для этого подключения
    GameManager.Current?.SpawnPlayer( this );
}
```

Два пути респавна:
1. **Автоматический** — через 4 секунды в `OnUpdate()`
2. **Ручной** — игрок нажимает кнопку → `PlayerObserver` вызывает `RequestRespawn()`

### Статистика Steam

```csharp
[Rpc.Broadcast]
private void RpcAddStat( string identifier, int amount = 1 )
{
    Sandbox.Services.Stats.Increment( identifier, amount );
}

public void AddStat( string identifier, int amount = 1 )
{
    if ( Application.CheatsEnabled ) return;  // без читов
    using ( Rpc.FilterInclude( Connection ) )
    {
        RpcAddStat( identifier, amount );     // отправить только этому игроку
    }
}
```

`AddStat` вызывается на хосте, но статистика записывается на клиенте (через RPC). `Rpc.FilterInclude` ограничивает broadcast только одним подключением.

## Создай файл

Путь: `Code/Player/PlayerData.cs`

```csharp
/// <summary>
/// Holds persistent player information like deaths, kills
/// </summary>
public sealed partial class PlayerData : Component, ISaveEvents
{
	/// <summary>
	/// Unique Id per each player and bot, equal to owning Player connection Id if it's a real player.
	/// </summary>
	[Property] public Guid PlayerId { get; set; }
	[Property] public long SteamId { get; set; } = -1L;
	[Property] public string DisplayName { get; set; }

	[Sync] public int Kills { get; set; }
	[Sync] public int Deaths { get; set; }

	[Sync] public bool IsGodMode { get; set; }

	public Connection Connection => Connection.Find( PlayerId );

	/// <summary>
	/// Is this player data me?
	/// </summary>
	public bool IsMe => PlayerId == Connection.Local.Id;

	/// <inheritdoc cref="Connection.Ping"/>
	public float Ping => Connection?.Ping ?? 0;

	/// <summary>
	/// Data for all players
	/// </summary>
	public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();

	/// <summary>
	/// Get player data for a player
	/// </summary>
	/// <param name="connection"></param>
	/// <returns></returns>
	public static PlayerData For( Connection connection ) => connection == null ? default : For( connection.Id );

	/// <summary>
	/// Get player data for a player's id
	/// </summary>
	/// <param name="playerId"></param>
	/// <returns></returns>
	public static PlayerData For( Guid playerId )
	{
		return All.FirstOrDefault( x => x.PlayerId == playerId );
	}

	// Host-side respawn tracking. No sync required.
	private bool _needsRespawn;
	private RealTimeSince _timeSinceDied;

	/// <summary>
	/// Called on the host when the player dies. Starts the respawn countdown so that
	/// PlayerData can trigger a respawn if the PlayerObserver is destroyed (e.g. by cleanup)
	/// before it fires.
	/// </summary>
	public void MarkForRespawn()
	{
		_needsRespawn = true;
		_timeSinceDied = 0;
	}

	/// <summary>
	/// Called by PlayerObserver (owner-only RPC) when the player presses to respawn early,
	/// or by OnUpdate after the timeout. Single entry point for all respawn logic.
	/// </summary>
	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void RequestRespawn()
	{
		_needsRespawn = false;

		// Clean up any lingering observer for this connection.
		foreach ( var observer in Scene.GetAllComponents<PlayerObserver>().Where( x => x.Network.Owner?.Id == PlayerId ).ToArray() )
		{
			observer.GameObject.Destroy();
		}

		GameManager.Current?.SpawnPlayer( this );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_needsRespawn ) return;
		if ( _timeSinceDied < 4f ) return;

		RequestRespawn();
	}

	[Rpc.Broadcast]
	private void RpcAddStat( string identifier, int amount = 1 )
	{
		Sandbox.Services.Stats.Increment( identifier, amount );
	}

	/// <summary>
	/// Called on the host, calls a RPC on the player and adds a stat
	/// </summary>
	/// <param name="identifier"></param>
	/// <param name="amount"></param>
	public void AddStat( string identifier, int amount = 1 )
	{
		if ( Application.CheatsEnabled ) return;

		Assert.True( Networking.IsHost, "PlayerData.AddStat is host-only!" );

		using ( Rpc.FilterInclude( Connection ) )
		{
			RpcAddStat( identifier, amount );
		}
	}

	void ISaveEvents.AfterLoad( string filename )
	{
		var connection = Connection;
		if ( connection == null )
		{
			// Get new PlayerId from SteamId if this is a new session
			PlayerId = Connection.All.FirstOrDefault( x => x.SteamId == SteamId )?.Id ?? Guid.Empty;
		}
	}
}
```

## Ключевые концепции

### Guid vs SteamId

- **PlayerId (Guid)** = `Connection.Id` — уникален для каждой сессии, меняется при переподключении
- **SteamId (long)** — постоянный Steam ID, не меняется никогда

При загрузке сохранения (`AfterLoad`) подключение может иметь **новый** `Connection.Id`, но **тот же** `SteamId`. Поэтому код ищет подключение по SteamId и обновляет PlayerId.

### ISaveEvents

`PlayerData` реализует `ISaveEvents` для правильной работы с системой сохранений. При загрузке карты нужно переназначить PlayerId на актуальное подключение.

## Проверка

1. Зайди в игру → в инспекторе найди PlayerData → видны Kills=0, Deaths=0
2. Убей кого-то → Kills увеличивается
3. Умри → через 4 секунды автореспавн
4. Включи god mode → IsGodMode = true

## Следующий файл

Переходи к **03.09 — Урон от падения (PlayerFallDamage)**.

---

<!-- seealso -->
## 🔗 См. также

- [04.01 — GameManager](04_01_GameManager.md)
- [23.01 — ISaveEvents](23_01_ISaveEvents.md)

