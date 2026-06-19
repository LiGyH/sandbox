# 03.06 — Данные игрока (PlayerData) 📊

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём компонент **PlayerData** — постоянное хранилище игровой статистики: убийства, смерти, режим бога. В отличие от `Player` (который уничтожается при смерти), `PlayerData` живёт всю сессию.

> ℹ️ Идентичность игрока (SteamId, имя, ping) больше **не** хранится в `PlayerData`. Её даёт само подключение — `Network.Owner` (тип `Connection`): `Network.Owner.SteamId`, `Network.Owner.DisplayName`, `Network.Owner.Ping`. Логика респавна тоже вынесена из `PlayerData` в `GameManager` (см. `GameManager.RequestRespawn`).

## Зачем два компонента: Player и PlayerData?

| | Player | PlayerData |
|--|--------|-----------|
| Жизненный цикл | Создаётся при спавне, уничтожается при смерти | Живёт всю сессию |
| Данные | Здоровье, броня, ввод | Kills, Deaths, GodMode |
| Сеть | На объекте игрока | На отдельном объекте |

Когда игрок умирает, `Player.GameObject` уничтожается. Но мы не хотим терять счёт убийств. Поэтому `PlayerData` хранится на отдельном GameObject и переживает смерть.

## Как это работает?

### Основные свойства

```csharp
[Sync( SyncFlags.FromHost )] public int Kills { get; internal set; }       // Убийства
[Sync( SyncFlags.FromHost )] public int Deaths { get; internal set; }      // Смерти
[Sync( SyncFlags.FromHost )] public bool IsGodMode { get; internal set; }  // Режим бога
```

`SyncFlags.FromHost` — значение синхронизируется **от хоста** ко всем клиентам (а не от владельца). `internal set` запрещает менять статистику откуда попало: её обновляет только серверный код. Идентификатор игрока (`IsMe`) определяется через подключение-владельца:

```csharp
public bool IsMe => Network.Owner == Connection.Local;
```

### Статические хелперы

```csharp
public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();
public static PlayerData For( Connection connection ) =>
    connection == null ? default : All.FirstOrDefault( x => x.Network.Owner == connection );
```

Удобный доступ к данным любого игрока по его подключению (`Connection`).

### Статистика Steam

```csharp
[Rpc.Broadcast( NetFlags.HostOnly )]
private void RpcAddStat( string identifier, int amount = 1 )
{
    Sandbox.Services.Stats.Increment( identifier, amount );
}

internal void AddStat( string identifier, int amount = 1 )
{
    if ( Application.CheatsEnabled ) return;  // без читов
    Assert.True( Networking.IsHost, "PlayerData.AddStat is host-only!" );
    using ( Rpc.FilterInclude( Network.Owner ) )
    {
        RpcAddStat( identifier, amount );     // отправить только этому игроку
    }
}
```

`AddStat` вызывается на хосте, но статистика записывается на клиенте (через RPC). `Rpc.FilterInclude( Network.Owner )` ограничивает broadcast только подключением-владельцем. `NetFlags.HostOnly` гарантирует, что RPC отправляет только хост.

> 🔁 **Где же респавн?** Раньше `PlayerData` отслеживал таймер смерти и вызывал `RequestRespawn`. Теперь это делает `GameManager.RequestRespawn` (`[Rpc.Host]`), а `PlayerObserver` дёргает `GameManager.Current?.RequestRespawn()`. См. этапы `04.01 — GameManager` и `03.13 — PlayerObserver`.

## Создай файл

Путь: `Code/Player/PlayerData.cs`

```csharp
/// <summary>
/// Holds persistent player information like deaths, kills
/// </summary>
public sealed partial class PlayerData : Component
{
	[Sync( SyncFlags.FromHost )] public int Kills { get; internal set; }
	[Sync( SyncFlags.FromHost )] public int Deaths { get; internal set; }
	[Sync( SyncFlags.FromHost )] public bool IsGodMode { get; internal set; }

	/// <summary>
	/// Is this player data me?
	/// </summary>
	public bool IsMe => Network.Owner == Connection.Local;

	/// <summary>
	/// Data for all players
	/// </summary>
	public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();

	/// <summary>
	/// Get player data for a player
	/// </summary>
	/// <param name="connection"></param>
	/// <returns></returns>
	public static PlayerData For( Connection connection ) => connection == null ? default : All.FirstOrDefault( x => x.Network.Owner == connection );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void RpcAddStat( string identifier, int amount = 1 )
	{
		Sandbox.Services.Stats.Increment( identifier, amount );
	}

	/// <summary>
	/// Called on the host, calls a RPC on the player and adds a stat
	/// </summary>
	/// <param name="identifier"></param>
	/// <param name="amount"></param>
	internal void AddStat( string identifier, int amount = 1 )
	{
		if ( Application.CheatsEnabled ) return;

		Assert.True( Networking.IsHost, "PlayerData.AddStat is host-only!" );

		using ( Rpc.FilterInclude( Network.Owner ) )
		{
			RpcAddStat( identifier, amount );
		}
	}
}
```

## Ключевые концепции

### Идентичность через Connection

- Постоянного `SteamId`/`DisplayName`/`PlayerId` в `PlayerData` больше нет. Имя, Steam ID и ping берутся напрямую у подключения-владельца: `Network.Owner.DisplayName`, `(long)Network.Owner.SteamId`, `Network.Owner.Ping`.
- `Network.Owner` — это `Connection` владельца объекта; сравнение `Network.Owner == Connection.Local` отвечает на вопрос «это мои данные?».
- Поиск данных игрока: `PlayerData.For( connection )` сопоставляет `x.Network.Owner == connection`.

### SyncFlags.FromHost

`Kills`/`Deaths`/`IsGodMode` помечены `[Sync( SyncFlags.FromHost )]` — синхронизируются строго от хоста, а сеттеры `internal`, чтобы статистику нельзя было подменить с клиента.

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
- [03.13 — PlayerObserver](03_13_PlayerObserver.md)

