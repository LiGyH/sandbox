# 🌀 Elastic — Инструмент «Резинка» (пружина)

## Что мы делаем?

Создаём файл `Elastic.cs` — инструмент, который соединяет два объекта **пружиной** (упругим соединением). В отличие от верёвки, пружина **тянет** объекты к длине покоя.

## Зачем это нужно?

- Создание амортизаторов для транспорта
- Пружинные ловушки
- Физические эксперименты
- Декоративные элементы (качели на пружинах)

## Как это работает внутри движка?

### Настройки (Properties)

| Свойство | Тип | По умолчанию | Описание |
|----------|-----|-------------|----------|
| `Frequency` | `float` | `2.0` | Частота колебаний (0–15). Чем больше — тем жёстче пружина |
| `Damping` | `float` | `0.1` | Затухание (0–4). Чем больше — тем быстрее затихают колебания |
| `StretchOnly` | `bool` | `false` | Если true — пружина только **тянет** (не толкает) |

### Отличие от Rope

| Параметр | Rope | Elastic |
|----------|------|---------|
| `Frequency` | 0 (нет пружины) | 2.0 (пружина) |
| `Damping` | 0 | 0.1 |
| `MaxLength` | len (ограничена) | float.MaxValue (без ограничения) |
| `ForceMode` | — | Pull или Both |

### SpringJoint для пружины

```csharp
joint.RestLength = len;                    // длина покоя
joint.Frequency = Frequency;               // частота
joint.Damping = Damping;                   // затухание
joint.ForceMode = StretchOnly
    ? SpringJoint.SpringForceMode.Pull     // только притяжение
    : SpringJoint.SpringForceMode.Both;    // и притяжение, и отталкивание
```

### Визуальное отличие

- **Верёвка**: текстура каната, цилиндрическая форма
- **Пружина**: оранжевый цвет (`Color.Orange`), тонкая линия (0.5), с физической симуляцией провисания

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/Elastic.cs`

```csharp
﻿[Icon( "🌀" )]
[ClassName( "elastic" )]
[Group( "Constraints" )]
public class Elastic : BaseConstraintToolMode
{
	[Range( 0, 15 )]
	[Property, Sync]
	public float Frequency { get; set; } = 2.0f;

	[Range( 0, 4 )]
	[Property, Sync]
	public float Damping { get; set; } = 0.1f;

	[Property, Sync]
	public bool StretchOnly { get; set; } = false;

	public override string Description => Stage == 1 ? "#tool.hint.elastic.stage1" : "#tool.hint.elastic.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.elastic.finish" : "#tool.hint.elastic.source";
	public override string ReloadAction => "#tool.hint.elastic.remove";

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( false, "elastic" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.LocalRotation = Rotation.Identity;

		var go2 = new GameObject( false, "elastic" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.LocalRotation = Rotation.Identity;

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = point1.WorldPosition().Distance( point2.WorldPosition() );

		if ( point1.GameObject != point2.GameObject )
		{
			var joint = go1.AddComponent<SpringJoint>();
			joint.Body = go2;
			joint.MinLength = 0;
			joint.MaxLength = float.MaxValue;
			joint.RestLength = len;
			joint.Frequency = Frequency;
			joint.Damping = Damping;
			joint.EnableCollision = true;
			joint.ForceMode = StretchOnly ? SpringJoint.SpringForceMode.Pull : SpringJoint.SpringForceMode.Both;
		}

		var vertletRope = go1.AddComponent<VerletRope>();
		vertletRope.Attachment = go2;
		vertletRope.SegmentCount = MathX.CeilToInt( len / 16.0f );

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = 0.5f;
		lineRenderer.Color = Color.Orange;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Elastic";
		undo.Add( go1 );
		undo.Add( go2 );
	}
}
```

## Что проверить?

1. Соединяем два объекта пружиной
2. Оттягиваем один — он должен колебаться и возвращаться
3. Увеличиваем `Frequency` — колебания быстрее
4. `StretchOnly = true` — пружина не отталкивает

---

➡️ Следующий шаг: [10_04 — Weld (сварка)](10_04_Weld.md)
