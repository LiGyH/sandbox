# 03.05 — Консольные команды игрока (Player.ConsoleCommands) ⌨️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём **консольные команды** — специальные функции, которые можно вызвать из консоли разработчика (~). Здесь хранятся команды `kill`, `god`, `map`, `undo` и вспомогательные методы поиска игрока.

## Как это работает?

### ConCmd — атрибут консольной команды

```csharp
[ConCmd( "kill" )]
public static void KillSelf( Connection source ) { ... }
```

- `[ConCmd("kill")]` — регистрирует статический метод как консольную команду с именем `kill`
- `Connection source` — s&box автоматически передаёт подключение вызвавшего игрока (серверная команда)
- Флаги: `ConVarFlags.Server` — только на сервере, `ConVarFlags.Cheat` — только с включёнными читами, `ConVarFlags.Admin` — только для админов

### Безопасность команд

Каждая команда проверяет, имеет ли вызвавший право на действие:

```csharp
// kill — находим игрока по подключению
var player = Player.FindForConnection( source );
if ( player is null ) return;

// god — проверяем через PlayerData
var player = PlayerData.For( source );
if ( !player.IsValid() ) return;
```

Без этих проверок любой клиент мог бы выполнить команду за другого игрока.

### RPC vs ConCmd

| Механизм | Кто вызывает | Где выполняется |
|----------|-------------|----------------|
| `[ConCmd]` | Игрок через консоль | На сервере (с параметром Connection) |
| `[Rpc.Host]` | Код на клиенте | На хосте |
| `[Rpc.Broadcast]` | Код на хосте | На всех клиентах |

`KillSelf()` использует оба: `[ConCmd("kill")]` вызывается из консоли, а внутренний `[Rpc.Host] KillSelf()` — из кода (например, при нажатии клавиши "die").

## Создай файл

Путь: `Code/Player/Player.ConsoleCommands.cs`

```csharp
public sealed partial class Player
{
	/// <summary>
	/// Find a player for this connection
	/// </summary>
	public static Player FindForConnection( Connection c )
	{
		return Game.ActiveScene.GetAll<Player>().FirstOrDefault( x => x.Network.Owner == c );
	}

	/// <summary>
	/// Get player from a connecction id
	/// </summary>
	/// <param name="playerId"></param>
	/// <returns></returns>
	public static Player For( Guid playerId )
	{
		return Game.ActiveScene.GetAll<Player>().FirstOrDefault( x => x.PlayerId.Equals( playerId ) );
	}

	/// <summary>
	/// Kill yourself
	/// </summary>
	[ConCmd( "kill" )]
	public static void KillSelf( Connection source )
	{
		var player = Player.FindForConnection( source );
		if ( player is null ) return;

		player.KillSelf();
	}

	[Rpc.Host]
	internal void KillSelf()
	{
		if ( Rpc.Caller != Network.Owner ) return;

		this.OnDamage( new DamageInfo( 5000, GameObject, null ) );
	}

	[ConCmd( "god", ConVarFlags.Server | ConVarFlags.Cheat, Help = "Toggle invulnerability" )]
	public static void God( Connection source )
	{
		var player = PlayerData.For( source );
		if ( !player.IsValid() )
			return;

		player.IsGodMode = !player.IsGodMode;
		source.SendLog( LogLevel.Info, player.IsGodMode ? "Godmode enabled" : "Godmode disabled" );
	}

	/// <summary>
	/// Switch to another map
	/// </summary>
	[ConCmd( "map", ConVarFlags.Admin )]
	public static void ChangeMap( string mapName )
	{
		LaunchArguments.Map = mapName;
		Game.Load( Game.Ident, true );
	}

	/// <summary>
	/// Switch to another map
	/// </summary>
	[ConCmd( "undo", ConVarFlags.Server )]
	public static void RunUndo( Connection source )
	{
		var player = Player.FindForConnection( source );
		if ( !player.IsValid() )
			return;

		player.Undo.Undo();
	}
}
```

## Построчное объяснение

### FindForConnection (строки 6–9)

```csharp
public static Player FindForConnection( Connection c )
{
    return Game.ActiveScene.GetAll<Player>().FirstOrDefault( x => x.Network.Owner == c );
}
```

Ищет в сцене игрока, чей `Network.Owner` совпадает с данным подключением. Каждый сетевой объект в s&box имеет владельца — это подключение клиента.

### KillSelf — двойная проверка (строки 32–38)

```csharp
[Rpc.Host]
internal void KillSelf()
{
    if ( Rpc.Caller != Network.Owner ) return; // защита от спуфинга
    this.OnDamage( new DamageInfo( 5000, GameObject, null ) );
}
```

- `[Rpc.Host]` — выполняется на хосте
- `Rpc.Caller != Network.Owner` — проверяем, что команду отправил именно владелец этого игрока
- `DamageInfo( 5000, ... )` — 5000 урона гарантированно убьёт (макс. HP = 100)

### God Mode (строки 40–49)

```csharp
[ConCmd( "god", ConVarFlags.Server | ConVarFlags.Cheat )]
```

- `ConVarFlags.Server` — команда выполняется на сервере
- `ConVarFlags.Cheat` — работает только если сервер включил `sv_cheats 1`
- `player.IsGodMode = !player.IsGodMode` — простой toggle (вкл/выкл)

### ChangeMap (строки 54–58)

```csharp
[ConCmd( "map", ConVarFlags.Admin )]
public static void ChangeMap( string mapName )
{
    LaunchArguments.Map = mapName;
    Game.Load( Game.Ident, true );
}
```

Только админ может менять карту. `Game.Load` перезагружает игру с новой картой.

## Проверка

1. Открой консоль (~) → введи `kill` → игрок умирает
2. Введи `god` (с читами) → включается режим бога
3. Введи `map gm_flatgrass` (как админ) → смена карты
4. Введи `undo` → отменяет последнее действие

## Следующий файл

Переходи к **03.05 — Система отмены (Player.Undo)**.
