# ⚡ ToolMode.Effects — Эффекты выстрела инструмента

## Что мы делаем?

Создаём файл `ToolMode.Effects.cs` — partial-часть класса `ToolMode`, отвечающая за визуальные и звуковые эффекты при успешном использовании инструмента (выстрел, луч, анимация).

## Зачем это нужно?

При успешном действии инструмента (сварка, создание верёвки и т.д.) нужно показать:
- Эффект попадания на целевой точке (impact)
- Луч от дула до точки попадания (beam)
- Анимацию вращения катушки на viewmodel
- Анимацию атаки на модели оружия

## Как это работает внутри движка?

- `ShootEffects` — `[Rpc.Broadcast]` метод, вызываемый на всех клиентах при успешном действии
- Получает `SelectionPoint target` — точку попадания с привязкой к объекту
- `SpinCoil()` — вращает катушку на модели Toolgun
- Клонирует `SuccessImpactEffect` в точке попадания с поворотом нормали
- Клонирует `SuccessBeamEffect` от дула, устанавливая `BeamEffect.TargetPosition`
- Устанавливает `b_attack = true` на `SkinnedModelRenderer` viewmodel для анимации
- `ShootFailEffects` — виртуальный метод для эффектов неудачи (пустая реализация по умолчанию)

## Создай файл

📄 `Code/Weapons/ToolGun/ToolMode.Effects.cs`

```csharp
﻿public abstract partial class ToolMode
{
	[Rpc.Broadcast]
	public virtual void ShootEffects( SelectionPoint target )
	{
		if ( !Toolgun.IsValid() ) return;

		var player = Toolgun.Owner;
		if ( !player.IsValid() ) return;

		if ( !target.IsValid() )
		{
			Log.Warning( "ShootEffects: Unknown object" );
			return;
		}

		Toolgun.SpinCoil();

		var muzzle = Toolgun.MuzzleTransform;

		if ( Toolgun.SuccessImpactEffect is GameObject impactPrefab )
		{
			var wt = target.WorldTransform();
			wt.Rotation = wt.Rotation * new Angles( 90, 0, 0 );

			var impact = impactPrefab.Clone( wt, null, false );
			impact.Enabled = true;
		}

		if ( Toolgun.SuccessBeamEffect is GameObject beamEffect )
		{
			var wt = target.WorldTransform();

			var go = beamEffect.Clone( new Transform( muzzle.WorldTransform.Position ), null, false );

			foreach ( var beam in go.GetComponentsInChildren<BeamEffect>( true ) )
			{
				beam.TargetPosition = wt.Position;
			}

			go.Enabled = true;
		}

		Toolgun.ViewModel?.GetComponentInChildren<SkinnedModelRenderer>().Set( "b_attack", true );
	}

	public virtual void ShootFailEffects( SelectionPoint target )
	{

	}

}

```

## Проверка

- Файл `Code/Weapons/ToolGun/ToolMode.Effects.cs` создан как partial-класс `ToolMode`
- `ShootEffects` помечен `[Rpc.Broadcast]` для сетевой синхронизации эффектов
- Клонируются эффекты попадания и луча из префабов Toolgun
- `ShootFailEffects` предоставляет точку расширения для эффектов неудачи
