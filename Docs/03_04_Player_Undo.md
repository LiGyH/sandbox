# 03.04 — Доступ к отмене (Player.Undo) ↩️

## Что мы делаем?

Добавляем игроку **доступ к системе отмены (Undo)**. Это крошечный partial-файл, который предоставляет свойство `Undo` для быстрого доступа к стеку отмены конкретного игрока.

## Как это работает?

```csharp
public UndoSystem.PlayerStack Undo => UndoSystem.Current.For( SteamId );
```

Одна строка, которая делает следующее:
1. `UndoSystem.Current` — получает синглтон системы отмены (GameObjectSystem)
2. `.For( SteamId )` — возвращает стек отмены для конкретного SteamId
3. Каждый игрок имеет свой собственный стек — один игрок не может отменить действия другого

### Где используется?

В `Player.ConsoleCommands.cs` (03.04):
```csharp
[ConCmd( "undo", ConVarFlags.Server )]
public static void RunUndo( Connection source )
{
    var player = Player.FindForConnection( source );
    player.Undo.Undo();  // ← вот здесь
}
```

И в `Player.cs` (03.01), при нажатии клавиши:
```csharp
if ( Input.Pressed( "undo" ) )
{
    ConsoleSystem.Run( "undo" );
}
```

## Создай файл

Путь: `Code/Player/Player.Undo.cs`

```csharp
public sealed partial class Player
{
	/// <summary>
	/// Access the undo system for this player
	/// </summary>
	public UndoSystem.PlayerStack Undo => UndoSystem.Current.For( SteamId );
}
```

## Зачем отдельный файл для одной строки?

1. **Разделение ответственности** — Player.cs и так 450+ строк
2. **Паттерн partial** — каждый аспект игрока в своём файле
3. **Навигация** — легко найти через File Explorer: «Player.Undo» → сразу видно, что это доступ к Undo

## Следующий файл

Переходи к **03.06 — Камера смерти (DeathCameraTarget)**.
