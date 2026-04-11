# ⛔ NoCollide — Инструмент «Без столкновений»

## Что мы делаем?

Создаём файл `NoCollide.cs` — инструмент, который **отключает столкновения** между двумя объектами. Они будут проходить друг сквозь друга.

## Зачем это нужно?

- Объекты внутри друг друга (двигатель внутри корпуса)
- Декоративные элементы, не мешающие движению
- Решение проблем с «застреванием» объектов

## Как это работает внутри движка?

### PhysicsFilter

Вместо физического соединения (Joint), `NoCollide` использует `PhysicsFilter`:

```csharp
var joint = go.AddComponent<PhysicsFilter>();
joint.Body = point2.GameObject;
```

`PhysicsFilter` — компонент движка, который исключает пару объектов из проверки столкновений.

### Отличие от других инструментов

- **Нет `ConstraintCleanup`** — NoCollide не является физическим соединением
- **Нет `FindConstraints`** — стандартное удаление не используется (нет переопределения)
- **Нет `ReloadAction`** — нет возможности удалить через R
- **Группа `"Tools"`** вместо `"Constraints"`

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/NoCollide.cs`

```csharp
﻿﻿
[Icon( "⛔" )]
[Title( "No Collide" )]
[ClassName( "nocollide" )]
[Group( "Tools" )]
public class NoCollide : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.nocollide.stage1" : "#tool.hint.nocollide.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.nocollide.finish" : "#tool.hint.nocollide.source";

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go = new GameObject( point1.GameObject, false, "no collide" );
		var joint = go.AddComponent<PhysicsFilter>();
		joint.Body = point2.GameObject;

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "No Collide";
		undo.Add( go );
	}
}
```

## Разбор

| Элемент | Описание |
|---------|----------|
| `[Title( "No Collide" )]` | Отображаемое имя с пробелом |
| `PhysicsFilter` | Исключает пару из проверки столкновений |
| Нет `ReloadAction` | Нет возможности удалить |
| Только `undo` с одним объектом | Проще, чем у других инструментов |

## Что проверить?

1. Два объекта стоят рядом → NoCollide → они проходят друг сквозь друга
2. Undo (Z) → столкновения восстанавливаются

---

➡️ Следующий шаг: [10_08 — Remover (удалитель)](10_08_Remover.md)
