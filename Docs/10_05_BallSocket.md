# 🎱 BallSocket — Инструмент «Шарнир»

## Что мы делаем?

Создаём файл `BallSocket.cs` — инструмент, который соединяет два объекта **шарнирным** соединением (`BallJoint`). Объекты могут свободно вращаться вокруг точки крепления.

## Зачем это нужно?

- Колёсная подвеска (если не используется WheelTool)
- Шарнирные двери
- Маятники
- Суставы для «рагдолл»-конструкций

## Как это работает внутри движка?

### BallJoint

`BallJoint` — компонент движка, который позволяет двум телам вращаться вокруг общей точки, но не отдаляться друг от друга.

```
Объект A ──── BallJoint ──── Объект B
              (общая точка)
              A может вращаться, B тоже
              но они остаются в одной точке
```

### Настройки

| Свойство | Тип | По умолчанию | Описание |
|----------|-----|-------------|----------|
| `EnableCollision` | `bool` | `false` | Сталкиваются ли объекты друг с другом |

### Особенность: позиционирование

```csharp
var go2 = new GameObject( point2.GameObject, false, "ballsocket" );
go2.LocalTransform = point2.LocalTransform;

var go1 = new GameObject( point1.GameObject, false, "ballsocket" );
go1.WorldTransform = go2.WorldTransform;  // <-- ключевая строка
```

Оба anchor-объекта (`go1`, `go2`) размещаются **в одной мировой точке** — в точке второго клика. Это гарантирует, что шарнир работает как единая ось.

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/BallSocket.cs`

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

## Что проверить?

1. Соединяем два объекта → первый может вращаться вокруг точки
2. `EnableCollision = true` → объекты сталкиваются
3. R → удаление шарнира

---

➡️ Следующий шаг: [10_06 — Slider (ползунок)](10_06_Slider.md)
