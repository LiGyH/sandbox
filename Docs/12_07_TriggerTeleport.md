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

Следующий шаг: [13.01 — Выброшенное оружие (DroppedWeapon)](13_01_DroppedWeapon.md)
