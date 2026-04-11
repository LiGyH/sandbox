# 💥 Toolgun.Effects — Эффекты инструментальной пушки

## Что мы делаем?

Создаём файл `Toolgun.Effects.cs` — partial-часть класса `Toolgun`, содержащая свойства визуальных эффектов и метод анимации переключения режимов.

## Зачем это нужно?

- Определяет префабы эффектов попадания (`SuccessImpactEffect`) и луча (`SuccessBeamEffect`) для использования инструментами
- Метод `SwitchToolMode` переключает анимацию модели оружия (firing_mode) при смене инструмента, создавая визуальный отклик

## Как это работает внутри движка?

- `SuccessImpactEffect` и `SuccessBeamEffect` — ссылки на префабы GameObject, которые клонируются при успешном действии инструмента
- `SwitchToolMode` — переключает булево значение `ping` и устанавливает параметр `firing_mode` в анимации модели оружия (0 или 1), создавая эффект «перещёлкивания»
- Используется `WeaponModel?.Renderer?.Set` для установки параметров на `SkinnedModelRenderer`

## Создай файл

📄 `Code/Weapons/ToolGun/Toolgun.Effects.cs`

```csharp
﻿public partial class Toolgun : ScreenWeapon
{
	[Header( "Effects" )]
	[Property] public GameObject SuccessImpactEffect { get; set; }
	[Property] public GameObject SuccessBeamEffect { get; set; }

	bool ping = false;
	public void SwitchToolMode()
	{
		ping = !ping;
		WeaponModel?.Renderer?.Set( "firing_mode", ping ? 1 : 0 );
	}
}
```

## Проверка

- Файл `Code/Weapons/ToolGun/Toolgun.Effects.cs` создан как partial-класс `Toolgun : ScreenWeapon`
- Определены свойства эффектов с атрибутами `[Header]` и `[Property]`
- `SwitchToolMode` переключает параметр анимации на модели
