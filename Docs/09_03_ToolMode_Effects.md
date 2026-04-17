# ✨ ToolMode.Effects.cs — Эффекты стрельбы инструмента

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём файл `ToolMode.Effects.cs` — расширение `partial class ToolMode`, которое отвечает за визуальные и звуковые эффекты при использовании инструмента (выстрел, луч, частицы попадания).

## Зачем это нужно?

Когда игрок нажимает ЛКМ инструментом и попадает в объект, должно происходить:
1. Коил (катушка) на пушке начинает вращаться
2. На месте попадания появляется эффект удара (частицы)
3. От дула пушки к точке попадания летит луч (beam)
4. Viewmodel проигрывает анимацию выстрела

Всё это делается через **RPC** (`[Rpc.Broadcast]`), чтобы **все игроки** видели эффекты.

## Как это работает внутри движка?

### Архитектура эффектов

```
ShootEffects(target)  — вызывается из конкретного инструмента
  ├── [Rpc.Broadcast]  — выполняется на ВСЕХ клиентах
  ├── Toolgun.SpinCoil()  — крутит катушку
  ├── SuccessImpactEffect.Clone()  — создаёт эффект удара
  ├── SuccessBeamEffect.Clone()  — создаёт луч
  │     └── BeamEffect.TargetPosition  — куда летит луч
  └── ViewModel.Set("b_attack", true)  — анимация viewmodel
```

### SelectionPoint

Метод принимает `SelectionPoint` — это структура, описывающая точку попадания в мире (подробнее в `09_04`). Из неё берётся:
- `WorldTransform()` — позиция и поворот удара
- `WorldPosition()` — только позиция

### Метод ShootFailEffects

Пустой виртуальный метод — можно переопределить для эффекта промаха.

## Ссылки на движок

- [`Rpc.Broadcast`](https://github.com/LiGyH/sbox-public) — RPC, выполняемый на всех клиентах
- `BeamEffect` — компонент, который рисует луч от точки A до точки B
- `SkinnedModelRenderer.Set()` — установка параметра анимации

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

## Разбор кода

| Строка | Что делает |
|--------|-----------|
| `[Rpc.Broadcast]` | Метод выполняется на ВСЕХ клиентах |
| `Toolgun.SpinCoil()` | Запускает анимацию вращения катушки на пушке |
| `SuccessImpactEffect` | Префаб эффекта удара (настраивается в редакторе) |
| `impactPrefab.Clone(wt, null, false)` | Создаёт копию префаба в точке попадания |
| `new Angles(90, 0, 0)` | Поворачивает эффект на 90° чтобы он смотрел от поверхности |
| `SuccessBeamEffect` | Префаб эффекта луча |
| `BeamEffect.TargetPosition` | Куда летит луч |
| `Set("b_attack", true)` | Говорит аниматору запустить анимацию выстрела |
| `ShootFailEffects` | Пустой метод для переопределения (эффект промаха) |

## Что проверить?

Файл сам по себе не требует проверки — эффекты появятся когда будут созданы конкретные инструменты и настроены префабы эффектов.

---

➡️ Следующий шаг: [09_04 — ToolMode.Helpers.cs](09_04_ToolMode_Helpers.md)
