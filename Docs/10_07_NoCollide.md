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

- **Нет `ConstraintCleanup`** — NoCollide не является физическим соединением (Joint), а лишь правилом фильтра.
- **`FindConstraints` переопределён** — вместо стандартного поиска суставов мы ищем все компоненты `PhysicsFilter` на исходном объекте, чьё `Body.Root` равно цели (или сам объект — если он один).
- **`ReloadAction` есть** — нажатие R в первой стадии удаляет существующий `PhysicsFilter` между объектом под прицелом и тем, на котором фильтр висит.
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
	public override string ReloadAction => "#tool.hint.nocollide.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var filter in linked.GetComponentsInChildren<PhysicsFilter>( true ) )
			if ( linked == target || filter.Body?.Root == target )
				yield return filter.GameObject;
	}

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
| `ReloadAction = "#tool.hint.nocollide.remove"` | На R можно удалить уже созданный фильтр без проп-полицейского |
| `FindConstraints(linked, target)` | Базовый класс сам обходит результат и удаляет найденные `GameObject`. Для NoCollide мы возвращаем GameObject'ы с компонентом `PhysicsFilter`, у которых `Body.Root == target` — это и есть «фильтр между этими двумя объектами». |
| Только `undo` с одним объектом | Проще, чем у других инструментов |

## Что проверить?

1. Два объекта стоят рядом → NoCollide → они проходят друг сквозь друга.
2. Undo (Z) → столкновения восстанавливаются.
3. Наведись на один из объектов и нажми **R** → `PhysicsFilter` удаляется, столкновения восстанавливаются без отмены всей цепочки undo.

---

➡️ Следующий шаг: [10_08 — Remover (удалитель)](10_08_Remover.md)
