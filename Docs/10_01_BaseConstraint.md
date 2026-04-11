# 🔗 BaseConstraintToolMode.cs — Базовый класс инструментов-соединений

## Что мы делаем?

Создаём файл `BaseConstraintToolMode.cs` — абстрактный класс для всех инструментов, которые **соединяют два объекта** (Rope, Weld, Elastic, BallSocket, Slider, NoCollide).

## Зачем это нужно?

Все соединения работают по одной схеме:
1. **Стадия 0**: Игрок наводит на первый объект и нажимает ЛКМ → запоминаем **точку 1**
2. **Стадия 1**: Игрок наводит на второй объект и нажимает ЛКМ → запоминаем **точку 2**
3. Вызываем `CreateConstraint(point1, point2)` — создаём соединение

Без этого базового класса каждый инструмент повторял бы одну и ту же логику стадий.

## Как это работает внутри движка?

### Наследование

```
ToolMode (абстрактный)
  └── BaseConstraintToolMode (абстрактный)
        ├── Rope
        ├── Elastic
        ├── Weld
        ├── BallSocket
        ├── Slider
        └── NoCollide
```

### Стадии (Stage)

```
Stage 0 → Наведение на объект A
  └── ЛКМ → Point1 = select, Stage = 1

Stage 1 → Наведение на объект B  
  └── ЛКМ → Point2 = select, CreateConstraint(), Stage = 0

Любая стадия:
  └── ПКМ (зажать) → Сброс (Stage = 0)
  └── R → Удалить все соединения с объекта
```

### Проверка валидности

`UpdateValidity()` проверяет:
- Оба объекта существуют
- Если `CanConstraintToSelf == false` — объекты не должны быть одним и тем же

### Snap Grid

`UseSnapGrid` по умолчанию `true` — все инструменты-соединения показывают сетку привязки.

### Удаление соединений

`RemoveConstraints(go)` — RPC-метод, который:
1. Находит все связанные объекты через `LinkedGameObjectBuilder`
2. Вызывает `FindConstraints()` для каждого — виртуальный метод, который каждый инструмент переопределяет
3. Уничтожает найденные соединения

## Ссылки на движок

- [`Rpc.Host(NetFlags.OwnerOnly)`](https://github.com/LiGyH/sbox-public) — RPC на хосте, только от владельца
- `LinkedGameObjectBuilder` — обход графа связанных объектов

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/BaseConstraintToolMode.cs`

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

## Разбор ключевых моментов

### Почему `Create()` отдельный RPC?

```csharp
[Rpc.Host( NetFlags.OwnerOnly )]
private void Create( SelectionPoint point1, SelectionPoint point2 )
```

Создание соединений должно происходить **на хосте** (сервере), потому что:
- Только хост может создавать сетевые объекты
- Нужна повторная проверка валидности (защита от читеров)
- После создания вызывается `CheckContraptionStats` для ачивок

### Почему `FindConstraints` виртуальный?

Каждый тип соединения ищет **свои** компоненты:
- `Rope` ищет `SpringJoint` и `VerletRope`
- `Weld` ищет `FixedJoint`
- `BallSocket` ищет `BallJoint`
- `Slider` ищет `SliderJoint`

## Что проверить?

Этот файл не может работать сам по себе (абстрактный). Проверяйте после создания конкретных инструментов (10_02 и далее).

---

➡️ Следующий шаг: [10_02 — Rope (верёвка)](10_02_Rope.md)
