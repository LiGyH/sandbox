# 08.19 — Шаровой шарнир (BallSocket) 🎱

## Что мы делаем?

Создаём инструмент `BallSocket` — режим Tool Gun для соединения двух объектов шаровым шарниром (`BallJoint`). Этот тип связи позволяет объектам свободно вращаться вокруг точки крепления во всех направлениях.

## Зачем это нужно?

Шаровой шарнир — основа для создания:
- Подвижных сочленений (руки роботов, ноги существ)
- Маятников и качелей с полной свободой вращения
- Шарнирных дверей и люков
- Любых конструкций, где нужна свобода вращения в точке крепления

## Как это работает внутри движка?

- **`EnableCollision`** — включает/отключает коллизию между соединёнными объектами. По умолчанию выключена, чтобы объекты не застревали друг в друге.
- **`CreateConstraint()`** — создаёт два дочерних объекта. Особенность: `go1` устанавливается в мировую позицию `go2` (точка крепления — на второй точке). `BallJoint` с нулевым трением.
- **`FindConstraints()`** — ищет `BallJoint` компоненты для удаления.
- Запрещает привязку к самому себе (`point1.GameObject == point2.GameObject`).

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/BallSocket.cs`

```csharp
﻿
[Icon( "🎱" )]
[ClassName( "ballsocket" )]
[Group( "Constraints" )]
public class BallSocket : BaseConstraintToolMode
{
	[Property, Sync]
	public bool EnableCollision { get; set; } = false;

	public override string Description => Stage == 1 ? "#tool.hint.ballsocket.stage1" : "#tool.hint.ballsocket.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.ballsocket.finish" : "#tool.hint.ballsocket.source";
	public override string ReloadAction => "#tool.hint.ballsocket.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var joint in linked.GetComponentsInChildren<BallJoint>( true ) )
			if ( linked == target || joint.Body?.Root == target )
				yield return joint.GameObject;
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( point1.GameObject == point2.GameObject )
			return;

		var go2 = new GameObject( point2.GameObject, false, "ballsocket" );
		go2.LocalTransform = point2.LocalTransform;

		var go1 = new GameObject( point1.GameObject, false, "ballsocket" );
		go1.WorldTransform = go2.WorldTransform;

		var joint = go1.AddComponent<BallJoint>();
		joint.Body = go2;
		joint.Friction = 0.0f;
		joint.EnableCollision = EnableCollision;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Ballsocket";
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
```

## Проверка

- Класс `BallSocket` наследуется от `BaseConstraintToolMode`.
- Используется `BallJoint` — шаровой шарнир с полной свободой вращения.
- `go1.WorldTransform = go2.WorldTransform` — точка шарнира совпадает со второй выбранной точкой.
- `Friction = 0.0f` — свободное вращение без трения.
- `EnableCollision` по умолчанию `false` для предотвращения физических конфликтов.
- Конструктор `GameObject` принимает родителя первым аргументом — объекты сразу становятся дочерними.
