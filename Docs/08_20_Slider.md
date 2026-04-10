# 08.20 — Слайдер (Slider) ➖

## Что мы делаем?

Создаём инструмент `Slider` — режим Tool Gun для соединения двух объектов скользящим шарниром (`SliderJoint`). Объект может двигаться вдоль оси между двумя выбранными точками, но не может отклоняться от этой оси.

## Зачем это нужно?

Слайдер незаменим для:
- Раздвижных дверей и ворот
- Поршневых механизмов и лифтов
- Направляющих для движения объектов по прямой линии
- Любых конструкций с линейным перемещением

## Как это работает внутри движка?

- **`CreateConstraint()`** — вычисляет ось скольжения как направление от точки 1 к точке 2 (`Vector3.Direction`). Оба anchor-объекта ориентируются вдоль этой оси через `Rotation.LookAt`.
- **`SliderJoint`** — ограничивает движение: `MinLength = 0` (объект может приблизиться), `MaxLength = len` (не может удалиться дальше исходного расстояния).
- **`LineRenderer`** — чёрная линия визуализирует ось скольжения.
- **`FindConstraints()`** — ищет `SliderJoint` компоненты для удаления.
- `ConstraintCleanup` обеспечивает корректную очистку при уничтожении.

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Slider.cs`

```csharp
﻿﻿
[Icon( "➖" )]
[ClassName( "slider" )]
[Group( "Constraints" )]
public class Slider : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.slider.stage1" : "#tool.hint.slider.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.slider.finish" : "#tool.hint.slider.source";
	public override string ReloadAction => "#tool.hint.slider.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var joint in linked.GetComponentsInChildren<SliderJoint>( true ) )
			if ( linked == target || joint.Body?.Root == target )
				yield return joint.GameObject;
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( point1.GameObject == point2.GameObject )
			return;

		var axis = Rotation.LookAt( Vector3.Direction( point1.WorldPosition(), point2.WorldPosition() ) );

		var go1 = new GameObject( false, "slider" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = axis;

		var go2 = new GameObject( false, "slider" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = axis;

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );

		var joint = go1.AddComponent<SliderJoint>();
		joint.Body = go2;
		joint.MinLength = 0;
		joint.MaxLength = len;
		joint.EnableCollision = true;

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = 0.5f;
		lineRenderer.Color = Color.Black;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Slider";
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
```

## Проверка

- Класс `Slider` наследуется от `BaseConstraintToolMode`.
- Ось скольжения вычисляется из направления между двумя точками.
- `SliderJoint` ограничивает перемещение в пределах `[0, len]`.
- Оба anchor-объекта ориентированы одинаково (`WorldRotation = axis`).
- Чёрная линия (`Color.Black`) визуально показывает направляющую.
- Коллизия включена (`EnableCollision = true`).
