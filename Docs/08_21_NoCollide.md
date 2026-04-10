# 08.21 — Отключение коллизий (NoCollide) ⛔

## Что мы делаем?

Создаём инструмент `NoCollide` — режим Tool Gun, который отключает физические столкновения между двумя выбранными объектами. Объекты будут проходить сквозь друг друга, не взаимодействуя физически.

## Зачем это нужно?

Отключение коллизий необходимо для:
- Устранения физических конфликтов при пересечении объектов
- Создания составных моделей из нескольких пропов, вставленных друг в друга
- Предотвращения дёрганья объектов, соединённых констрейнтами в тесном пространстве
- Декоративных конструкций, где объекты должны визуально перекрываться

## Как это работает внутри движка?

- **`PhysicsFilter`** — компонент, исключающий коллизию между двумя конкретными объектами. В отличие от отключения коллизий целиком, он действует точечно — только между указанной парой.
- **`CreateConstraint()`** — максимально простая реализация: создаёт один дочерний `GameObject` с `PhysicsFilter`, указывающим на второй объект.
- Не имеет свойства `ReloadAction` — удаление NoCollide не реализовано через Reload.
- Группа `"Tools"` (а не `"Constraints"`) — этот инструмент не создаёт физических связей.

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/NoCollide.cs`

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

## Проверка

- Класс `NoCollide` наследуется от `BaseConstraintToolMode`.
- Использует `PhysicsFilter` вместо физического джоинта.
- Создаёт только один дочерний объект (не два), привязанный к первому выбранному объекту.
- Группа `"Tools"`, а не `"Constraints"` — отражает утилитарный характер инструмента.
- Атрибут `[Title( "No Collide" )]` задаёт отображаемое имя с пробелом.
- Нет `ReloadAction` — базовый класс обеспечивает удаление через `FindConstraints`, который по умолчанию возвращает пустой список.
