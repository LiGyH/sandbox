# 16.04 — Триггер толчка (TriggerPush) 🌠

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём **TriggerPush** — триггер-зону, которая толкает объекты и игроков в заданном направлении (джампады, потоки воздуха, гравитационные аномалии).

## Как это работает?

Объект с `TriggerPush` и коллайдером-триггером:
1. При входе в зону (`OnTriggerEnter`) — добавляет объект в список
2. При выходе (`OnTriggerExit`) — убирает из списка
3. Каждый физический тик (`OnFixedUpdate`) — прибавляет скорость в направлении `Direction * Power`
4. Для игроков — дополнительно вызывает `PreventGrounding` чтобы не «прилипали» к земле

## Создай файл

Путь: `Code/Map/TriggerPush.cs`

```csharp
﻿[Category( "Gameplay" ), Icon( "compare_arrows" ), EditorHandle( Icon = "🌠" )]
public sealed class TriggerPush : Component, Component.ITriggerListener
{
	[Property] Vector3 Direction { get; set; } = Vector3.Up;
	[Property] float Power { get; set; } = 20.0f;

	[Property]
	List<GameObject> Objects = new();

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Arrow( 0, Direction * 50 );
	}

	protected override void OnFixedUpdate()
	{
		if ( Objects is null )
			return;

		foreach ( var obj in Objects )
		{
			var plycomp = obj.Components.Get<Player>();
			var cc = obj.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );

			if(plycomp.IsValid())
				plycomp.Controller.PreventGrounding( 0.1f );

			if ( !cc.IsValid() )
			{
				Objects.Remove( obj );
				return;
			}

			cc.Velocity += Direction * Power;
		}
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.Get<Rigidbody>(FindMode.EverythingInSelfAndParent).IsValid())
		{
			var obj = other.GameObject.Root;
			Objects.Add( obj );
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		if ( other.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent ).IsValid() )
		{
			var obj = other.GameObject.Root;
			Objects.Remove( obj );
		}
	}
}
```

## Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| `ITriggerListener` | Интерфейс движка для реакции на вход/выход из триггера |
| `PreventGrounding` | Метод контроллера игрока — не считать поверхность «землёй» на 0.1 сек |
| `FindMode.EverythingInSelfAndParent` | Поиск Rigidbody и в самом объекте, и в родителях |
| `Direction * Power` | Постоянная сила, прибавляемая каждый тик — объект ускоряется |
| `Gizmo.Draw.Arrow` | Рисует стрелку в редакторе для визуализации направления |

## Результат

- Триггер-зона, толкающая физические объекты и игроков
- Визуализация направления стрелкой в редакторе

---

Следующий шаг: [12.07 — Триггер телепортации (TriggerTeleport)](16_04_Triggers.md)


---

# 12.07 — Триггер телепортации (TriggerTeleport) ✨

## Что мы делаем?

Создаём **TriggerTeleport** — триггер-зону, которая мгновенно перемещает объекты к целевой точке. Поддерживает фильтрацию по тегам.

## Как это работает?

При входе объекта в триггер:
1. Проверяем, что объект не прокси (управляется локально)
2. Проверяем теги (Include/Exclude)
3. Телепортируем в позицию `Target`
4. Очищаем интерполяцию (`ClearInterpolation`) чтобы не было «скольжения»
5. Вызываем событие `OnTeleported` на всех клиентах

## Создай файл

Путь: `Code/Map/TriggerTeleport.cs`

```csharp
public sealed class TriggerTeleport : Component, Component.ITriggerListener
{
	/// <summary>
	/// If not empty, the target must have one of these tags
	/// </summary>
	[Property, Group( "Target" )] public TagSet Include { get; set; } = new();

	/// <summary>
	/// If not empty, the target must not have one of these tags
	/// </summary>
	[Property, Group( "Target" )] public TagSet Exclude { get; set; } = new();

	[Property] public GameObject Target { get; set; }
	[Property] public Action<GameObject> OnTeleported { get; set; }

	protected override void DrawGizmos()
	{
		if ( !Target.IsValid() )
			return;

		Gizmo.Draw.Arrow( 0, WorldTransform.PointToLocal( Target.WorldPosition ) );
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var go = other.GameObject;

		if ( !IsValidTarget( ref go ) ) return;

		go.WorldPosition = Target.WorldPosition;
		go.Transform.ClearInterpolation();

		DoTeleportedEvent( go );
	}

	bool IsValidTarget( ref GameObject go )
	{
		go = go.Root;
		if ( go.IsProxy ) return false;

		if ( !Exclude.IsEmpty && go.Tags.HasAny( Exclude ) )
			return false;

		if ( !Include.IsEmpty && !go.Tags.HasAny( Include ) )
			return false;

		return true;
	}

	[Rpc.Broadcast]
	void DoTeleportedEvent( GameObject obj )
	{
		OnTeleported?.Invoke( obj );
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `TagSet Include/Exclude` | Фильтрация: телепортировать только `player` или не телепортировать `npc` |
| `go.Root` | Берём корневой объект (игрок может войти коллайдером ноги) |
| `IsProxy` | Только владелец объекта перемещает его |
| `ClearInterpolation` | Без этого объект «проплывёт» от старой к новой позиции |
| `[Rpc.Broadcast]` | Событие `OnTeleported` вызывается на всех клиентах |

## Результат

- Телепорт-зоны для карт (порталы, ловушки, переходы между зонами)
- Фильтрация по тегам
- Гизмо-стрелка к цели в редакторе

---

Следующий шаг: [13.01 — Выброшенное оружие (DroppedWeapon)](15_01_DroppedWeapon.md)
