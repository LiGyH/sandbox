# 08.15 — Базовый режим констрейнтов (BaseConstraintToolMode) 🔗

## Что мы делаем?

Создаём абстрактный базовый класс `BaseConstraintToolMode`, от которого наследуются все инструменты, работающие с ограничениями (constraints) между объектами. Этот класс реализует двухэтапный процесс выбора: сначала выбираем первый объект, затем второй — и между ними создаётся связь.

## Зачем это нужно?

Все инструменты-констрейнты (Weld, Rope, Elastic, BallSocket, Slider, NoCollide) разделяют общую логику:
- Двухстадийный выбор точек
- Валидация выбранных объектов
- Возможность удаления существующих констрейнтов (по `reload`)
- Сброс выбора по правой кнопке мыши

Вынесение этой логики в базовый класс избавляет от дублирования кода и гарантирует единообразное поведение.

## Как это работает внутри движка?

- **`Stage`** — текущая стадия выбора: `0` — выбираем первую точку, `1` — вторую.
- **`OnControl()`** — основной цикл: обрабатывает `attack2` (сброс), `reload` (удаление констрейнтов), `attack1` (выбор точки или создание связи).
- **`UpdateValidity()`** — проверяет, что объекты валидны и (если `CanConstraintToSelf == false`) не являются одним и тем же объектом.
- **`Create()`** — RPC-метод на хосте, вызывающий абстрактный `CreateConstraint()`.
- **`RemoveConstraints()`** — находит все связанные объекты через `LinkedGameObjectBuilder` и удаляет найденные констрейнты.
- **`FindConstraints()`** — виртуальный метод, переопределяемый наследниками для указания, какие именно констрейнты удалять.
- **`CreateConstraint()`** — абстрактный метод, реализуемый в каждом конкретном инструменте.

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/BaseConstraintToolMode.cs`

```csharp
﻿public abstract class BaseConstraintToolMode : ToolMode
{
	protected SelectionPoint Point1;
	protected SelectionPoint Point2;
	protected int Stage = 0;

	public virtual bool CanConstraintToSelf => false;
	public override bool UseSnapGrid => true;

	protected override void OnDisabled()
	{
		base.OnDisabled();
		Stage = 0;
		Point1 = default;
		Point2 = default;
	}

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Down( "attack2" ) )
		{
			Stage = 0;
			IsValidState = false;
			return;
		}

		var select = TraceSelect();
		if ( !select.IsValid() )
			return;

		if ( Input.Pressed( "reload" ) )
		{
			var go = select.GameObject.Network.RootGameObject ?? select.GameObject;
			RemoveConstraints( go );
			ShootEffects( select );
		}

		IsValidState = true;

		if ( Stage == 0 )
		{
			IsValidState = UpdateValidity( select );
		}

		if ( Stage == 1 )
		{
			IsValidState = UpdateValidity( Point1, select );
		}

		if ( !IsValidState ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			if ( Stage == 0 )
			{
				Point1 = select;
				Stage++;
				ShootEffects( select );
				return;
			}

			if ( Stage == 1 )
			{
				Point2 = select;

				Create( Point1, Point2 );
				ShootEffects( select );
			}

			Stage = 0;
		}


	}

	bool UpdateValidity( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( !point1.GameObject.IsValid() ) return false;
		if ( !point2.GameObject.IsValid() ) return false;

		if ( !CanConstraintToSelf )
		{
			if ( point1.GameObject == point2.GameObject )
			{
				return false;
			}
		}

		return true;
	}

	bool UpdateValidity( SelectionPoint point1 )
	{
		if ( !point1.GameObject.IsValid() ) return false;

		return true;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void Create( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( !UpdateValidity( point1, point2 ) )
		{
			Log.Warning( "Tried to create invalid constraint" );
			return;
		}

		CreateConstraint( point1, point2 );
		CheckContraptionStats( point1.GameObject );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void RemoveConstraints( GameObject go )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( go );

		var toRemove = new List<GameObject>();
		foreach ( var linked in builder.Objects )
			toRemove.AddRange( FindConstraints( linked, go ) );

		foreach ( var host in toRemove )
			host.Destroy();
	}

	/// <summary>
	/// Lets tools define what constraints should be removed when removing constraints from a game object.
	/// </summary>
	/// <param name="linked"></param>
	/// <param name="target"></param>
	/// <returns></returns>
	protected virtual IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target ) => [];

	protected abstract void CreateConstraint( SelectionPoint point1, SelectionPoint point2 );
}
```

## Проверка

- Класс является `abstract` и наследуется от `ToolMode`.
- Двухстадийный процесс: `Stage == 0` → выбор первой точки, `Stage == 1` → выбор второй и создание констрейнта.
- `attack2` сбрасывает выбор, `reload` удаляет констрейнты с целевого объекта.
- Валидация проверяет существование объектов и запрет привязки к самому себе (если `CanConstraintToSelf == false`).
- Создание и удаление констрейнтов выполняются через `[Rpc.Host]` — только на сервере.
