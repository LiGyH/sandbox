# 06.10 — Модель оружия (WeaponModel) 🔧

## Что мы делаем?

Создаём абстрактный базовый класс `WeaponModel` — общую логику для `ViewModel` (первое лицо) и `WorldModel` (третье лицо). Он управляет рендерером, трансформами для дула/гильзоприёмника и создаёт визуальные эффекты выстрела.

## Зачем это нужно?

- **Общий интерфейс**: и вью-модель, и мировая модель имеют одинаковые точки (дуло, гильзоприёмник) и одинаковые методы для создания эффектов.
- **Дульная вспышка**: `DoMuzzleEffect()` — клонирует эффект в точку дула.
- **Трассер**: `DoTracerEffect()` — клонирует эффект трассера и задаёт конечную точку.
- **Выброс гильзы**: `DoEjectBrass()` — клонирует гильзу, задаёт направление и вращение.
- **Развёртывание**: `Deploy()` — устанавливает анимационный параметр `b_deploy` для анимации доставания оружия.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `Renderer` | `SkinnedModelRenderer` — основной рендерер модели. |
| `MuzzleTransform` | `GameObject` — точка откуда вылетают пули / вспышка. |
| `EjectTransform` | `GameObject` — точка откуда вылетают гильзы. |
| `MuzzleEffect` | Префаб дульной вспышки. |
| `EjectBrass` | Префаб гильзы. |
| `TracerEffect` | Префаб трассера. |
| `Deploy()` | Устанавливает `b_deploy = true` на рендерере. |
| `GetTracerOrigin()` | Возвращает трансформ дула или объекта по умолчанию. |
| `DoTracerEffect()` | Клонирует трассер, ищет компонент `Tracer` и задаёт `EndPoint`. |
| `DoEjectBrass()` | Клонирует гильзу, вычисляет направление выброса (вперёд + вправо + случайность), задаёт скорость и угловую скорость `Rigidbody`. |
| `DoMuzzleEffect()` | Клонирует вспышку как дочерний объект `MuzzleTransform` с нулевым трансформом. |

## Создай файл

**Путь:** `Code/Game/Weapon/WeaponModel/WeaponModel.cs`

```csharp
﻿public abstract class WeaponModel : Component
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public GameObject MuzzleTransform { get; set; }
	[Property] public GameObject EjectTransform { get; set; }
	[Property] public GameObject MuzzleEffect { get; set; }
	[Property] public GameObject EjectBrass { get; set; }
	[Property] public GameObject TracerEffect { get; set; }

	public void Deploy()
	{
		Renderer?.Set( "b_deploy", true );
	}

	public Transform GetTracerOrigin()
	{
		if ( MuzzleTransform.IsValid() )
			return MuzzleTransform.WorldTransform;

		return WorldTransform;
	}

	public void DoTracerEffect( Vector3 hitPoint, Vector3? origin = null )
	{
		if ( !TracerEffect.IsValid() ) return;

		var tracerOrigin = GetTracerOrigin().WithScale( 1 );
		if ( origin.HasValue ) tracerOrigin = tracerOrigin.WithPosition( origin.Value );

		var effect = TracerEffect.Clone( new CloneConfig { Transform = tracerOrigin, StartEnabled = true } );

		if ( effect.GetComponentInChildren<Tracer>() is Tracer tracer )
		{
			tracer.EndPoint = hitPoint;
		}
	}

	public void DoEjectBrass()
	{
		if ( !EjectBrass.IsValid() ) return;
		if ( !EjectTransform.IsValid() ) return;

		var effect = EjectBrass.Clone( new CloneConfig { Transform = EjectTransform.WorldTransform.WithScale( 1 ), StartEnabled = true } );
		effect.WorldRotation = effect.WorldRotation * new Angles( 90, 0, 0 );

		var ejectDirection = (EjectTransform.WorldRotation.Forward * 250 + (EjectTransform.WorldRotation.Right + Vector3.Random * -0.35f) * 250);

		var rb = effect.GetComponentInChildren<Rigidbody>();
		rb.Velocity = ejectDirection;
		rb.AngularVelocity = EjectTransform.WorldRotation.Right * 50f;
	}

	public void DoMuzzleEffect()
	{
		if ( !MuzzleEffect.IsValid() ) return;
		if ( !MuzzleTransform.IsValid() ) return;

		MuzzleEffect.Clone( new CloneConfig { Parent = MuzzleTransform, Transform = global::Transform.Zero, StartEnabled = true } );
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `Deploy()` устанавливает параметр анимации `b_deploy`
- [ ] `DoTracerEffect()` клонирует трассер и задаёт конечную точку
- [ ] `DoEjectBrass()` клонирует гильзу с правильным направлением и вращением
- [ ] `DoMuzzleEffect()` клонирует вспышку в точку дула
- [ ] `GetTracerOrigin()` возвращает трансформ дула или объекта по умолчанию
