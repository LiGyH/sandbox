# 05.04 — Компонент: Ручная связь (ManualLink) 🔗

## Что мы делаем?

Создаём **ManualLink** — компонент, реализующий логическую (не физическую) связь между двумя `GameObject`. При удалении одного объекта автоматически удаляется и связанный.

## Зачем это нужно?

Инструмент Linker позволяет игрокам группировать объекты без физического соединения. Это нужно для Duplicator — инструмента копирования: он рассматривает связанные через `ManualLink` объекты как часть одной конструкции и дублирует их вместе. Без `ManualLink` дупликатор скопировал бы только один объект, потеряв связанные элементы.

## Как это работает внутри движка?

- `[Property, Sync]` — свойство `Body` синхронизируется по сети, так что все клиенты знают о связи.
- `OnDestroy()` — при уничтожении этого `GameObject` проверяем `Body.IsValid()` и удаляем связанный объект.
- `OnUpdate()` — каждый кадр проверяем: если `Body` уже не валиден (удалён), уничтожаем себя через `DestroyGameObject()`.
- Паттерн «взаимного уничтожения» аналогичен `ConstraintCleanup` (05.01), но с `sealed`-классом и синхронизацией по сети.

## Создай файл

Путь: `Code/Components/ManualLink.cs`

```csharp
/// <summary>
/// A non-physics logical link between two GameObjects.
/// Used by the Linker tool to group unconnected objects so the Duplicator
/// treats them as part of the same contraption.
/// </summary>
public sealed class ManualLink : Component
{
	[Property, Sync]
	public GameObject Body { get; set; }

	protected override void OnDestroy()
	{
		if ( Body.IsValid() )
			Body.Destroy();

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if ( !Body.IsValid() )
			DestroyGameObject();
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент будет задействован инструментом Linker и дупликатором в более поздних фазах.
