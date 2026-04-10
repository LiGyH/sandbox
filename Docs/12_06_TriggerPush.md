# 12.06 — Триггер толчка (TriggerPush) 🌠

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

Следующий шаг: [12.07 — Триггер телепортации (TriggerTeleport)](12_07_TriggerTeleport.md)
