# 03.17 — Система отмены (UndoSystem) ↩️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём **UndoSystem** — систему отмены действий (как Ctrl+Z). Когда игрок размещает пропы, сущности или конструкции, каждое действие записывается в стек. Нажатие Z (или команда `undo`) отменяет последнее действие — обычно уничтожая созданный объект.

## Архитектура

```
UndoSystem (GameObjectSystem — синглтон)
  └── Dictionary<long, PlayerStack>    // стек для каждого SteamId
        └── PlayerStack
              └── Stack<Entry>          // стек записей (LIFO)
                    └── Entry
                          ├── Name      // "Spawn Prop"
                          ├── Icon      // иконка
                          └── Actions   // список действий (обычно: удалить GameObject)
```

### GameObjectSystem

```csharp
public class UndoSystem : GameObjectSystem<UndoSystem>
```

`GameObjectSystem<T>` — синглтон s&box, привязанный к сцене. Доступ: `UndoSystem.Current`. Создаётся автоматически при загрузке сцены.

### Стек по SteamId

```csharp
Dictionary<long, PlayerStack> stacks = new();

public PlayerStack For( long steamId )
{
    if ( !stacks.TryGetValue( steamId, out var stack ) )
    {
        stack = new PlayerStack( steamId );
        stacks[steamId] = stack;
    }
    return stack;
}
```

Каждый игрок имеет свой стек. Один игрок не может отменить действия другого. Lazy-создание: стек появляется при первом обращении.

### Entry — запись действия

```csharp
public class Entry
{
    public string Name { get; set; }  // "Spawn Prop", "Weld"
    public string Icon { get; set; }  // иконка для уведомления

    long SteamId;

    List<GameObject> gameObjects = new();  // объекты, которые будут уничтожены при отмене
}
```

> **Изменение от старой версии:** раньше Entry хранил мультикаст-делегат `Action actions` и флаг `actioned` — каждый `Add` добавлял лямбду. В актуальном апстриме это упрощено до простого `List<GameObject>`. Это позволило корректно реализовать `Remove(GameObject)` (см. ниже) — из делегата отдельную лямбду удалить было нельзя.

#### Добавление объектов

```csharp
public void Add( GameObject go )
{
    gameObjects.Add( go );
}
```

#### Удаление объектов из записи

```csharp
public void Remove( GameObject go )
{
    gameObjects.Remove( go );
}
```

Нужен, например, в `PlayerInventory`: когда игрок поднял лежащее на земле оружие, его GameObject уже мог быть в чьём-то undo-стеке (от спавнера). Если его не убрать, кто-то нажмёт Z — и оружие исчезнет прямо из инвентаря. Поэтому при подборе вызывается `UndoSystem.Current.Remove( item.GameObject )` (см. [03.08 — PlayerInventory](03_08_PlayerInventory.md)).

#### Выполнение отмены

```csharp
public bool Run( bool sendNotice = true )
{
    var actioned = false;

    foreach ( var go in gameObjects )
    {
        if ( go.IsValid() )
        {
            go.Destroy();
            actioned = true;
        }
    }

    if ( !actioned ) return false; // ничего не удалилось → false

    // Отправляем уведомление игроку
    UndoNotice( Name );
    return true;
}
```

`actioned` теперь — локальная переменная, выставляется только если хотя бы один объект остался валиден и был уничтожен. Если все объекты уже были удалены другим способом → возвращаем `false` → `PlayerStack.Undo()` пробует следующую запись.

### Рекурсивный undo

```csharp
public void Undo()
{
    if ( entries.Count == 0 ) return;
    
    var entry = entries.Pop();
    
    if ( !entry.Run() )  // если ничего не удалилось
    {
        Undo();          // пробуем следующую запись
    }
}
```

Если текущая запись «пустая» (объект уже удалён), автоматически переходим к следующей. Это удобно: если другой игрок удалил ваш проп через Remover Tool, ваш undo пропустит эту запись.

### Уведомление

```csharp
[Rpc.Broadcast]
public static void UndoNotice( string title )
{
    Notices.AddNotice( "cached", "#3273eb", $"Undo {title}".Trim(), 5 );
    Sound.Play( "sounds/ui/ui.undo.sound" );
}
```

Показывает уведомление «Undo Spawn Prop» с синим цветом (#3273eb) на 5 секунд и проигрывает звук.

## Создай файл

Путь: `Code/Player/UndoSystem/UndoSystem.cs`

```csharp
using Sandbox.UI;

public class UndoSystem : GameObjectSystem<UndoSystem>
{
	Dictionary<long, PlayerStack> stacks = new();

	public UndoSystem( Scene scene ) : base( scene )
	{
	}

	/// <summary>
	/// Get the undo stack for a specific SteamId
	/// </summary>
	public PlayerStack For( long steamId )
	{
		if ( !stacks.TryGetValue( steamId, out var stack ) )
		{
			stack = new PlayerStack( steamId );
			stacks[steamId] = stack;
		}
		return stack;
	}

	/// <summary>
	/// Remove a GameObject from all player undo stacks so it can no longer be undone.
	/// </summary>
	public void Remove( GameObject go )
	{
		foreach ( var stack in stacks.Values )
			stack.Remove( go );
	}

	/// <summary>
	/// Per-player undo stack
	/// </summary>
	public class PlayerStack
	{
		long steamId;
		Stack<Entry> entries = new();

		public PlayerStack( long steamId )
		{
			this.steamId = steamId;
		}

		/// <summary>
		/// Create a new undo entry
		/// </summary>
		public Entry Create()
		{
			var entry = new Entry( steamId );
			entries.Push( entry );
			return entry;
		}

		/// <summary>
		/// Run the undo
		/// </summary>
		public void Undo()
		{
			if ( entries.Count == 0 )
				return;

			var entry = entries.Pop();

			// if we didn't do anything, do the next one
			if ( !entry.Run() )
			{
				Undo();
			}
		}

		/// <summary>
		/// Remove a GameObject from all entries in this stack.
		/// </summary>
		public void Remove( GameObject go )
		{
			foreach ( var entry in entries )
				entry.Remove( go );
		}
	}

	/// <summary>
	/// An undo entry
	/// </summary>
	public class Entry
	{
		/// <summary>
		/// The name of the undo, should fit the format "Undo something". Like "Undo Spawn Prop".
		/// </summary>
		public string Name { get; set; }
		public string Icon { get; set; }

		long SteamId;

		List<GameObject> gameObjects = new();

		internal Entry( long steamId )
		{
			SteamId = steamId;
		}

		/// <summary>
		/// Add a GameObject that should be destroyed when the undo is undone
		/// </summary>
		public void Add( GameObject go )
		{
			gameObjects.Add( go );
		}

		/// <summary>
		/// Add a collection of GameObjects that should be destroyed when the undo is undone
		/// </summary>
		public void Add( params IEnumerable<GameObject> gos )
		{
			foreach ( var go in gos )
			{
				Add( go );
			}
		}

		/// <summary>
		/// Remove a GameObject from this entry so it will no longer be destroyed on undo.
		/// </summary>
		public void Remove( GameObject go )
		{
			gameObjects.Remove( go );
		}

		/// <summary>
		/// Run this undo
		/// </summary>
		public bool Run( bool sendNotice = true )
		{
			var actioned = false;

			foreach ( var go in gameObjects )
			{
				if ( go.IsValid() )
				{
					go.Destroy();
					actioned = true;
				}
			}

			if ( !actioned )
				return false;

			if ( sendNotice )
			{
				var c = Connection.All.FirstOrDefault( x => x.SteamId == SteamId );
				if ( c is not null )
				{
					using ( Rpc.FilterInclude( c ) )
					{
						UndoNotice( Name );
					}
				}
			}

			return true;
		}

		[Rpc.Broadcast]
		public static void UndoNotice( string title )
		{
			Notices.AddNotice( "cached", "#3273eb", $"Undo {title}".Trim(), 5 );
			Sound.Play( "sounds/ui/ui.undo.sound" );
		}
	}
}
```

## Как используется в других местах

### Спавн пропа (PropSpawner)

```csharp
var undo = player.Undo.Create();
undo.Name = "Spawn Prop";
undo.Add( spawnedGameObject );
```

### Инструмент Weld (ToolGun)

```csharp
var undo = player.Undo.Create();
undo.Name = "Weld";
undo.Add( constraint );     // удалит Joint
undo.Add( frozenBody );     // опционально: удалит замороженное тело
```

## Проверка

1. Заспавни проп → нажми Z → проп удаляется, появляется уведомление «Undo Spawn Prop»
2. Заспавни несколько пропов → Z, Z, Z → удаляются в обратном порядке
3. Удали проп Remover'ом → Z → пропускает удалённый, удаляет предыдущий

## 🎉 Фаза 3 завершена!

Система игрока полностью документирована. Следующая фаза — **Фаза 4: Игровой цикл (GameLoop)**.
