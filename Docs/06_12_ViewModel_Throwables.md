# 06.12 — Метательные предметы во вью-модели (ViewModel.Throwables) 💣

## Что мы делаем?

Создаём partial-часть `ViewModel`, добавляющую поддержку метательных предметов (гранаты, молотовы, светошумовые). Определяем перечисление типов бросков и логику инициализации анимации.

## Зачем это нужно?

- **Типы гранат**: перечисление `Throwable` определяет виды бросаемых предметов (HE, дымовая, оглушающая, молотов, светошумовая).
- **Анимация броска**: при включении устанавливается параметр `throwable_type`, управляющий соответствующей анимацией.
- **Feature-система**: `[FeatureEnabled("Throwables")]` / `[Feature("Throwables")]` — свойства появляются в инспекторе только при включённой фиче.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `Throwable` (enum) | Типы гранат: `HEGrenade`, `SmokeGrenade`, `StunGrenade`, `Molotov`, `Flashbang`. |
| `IsThrowable` | `[FeatureEnabled]` — включает функциональность бросков. Используется в `OnAttack()` основного класса. |
| `ThrowableType` | Тип конкретного метательного предмета. |
| `OnEnabled()` | При активации — устанавливает анимационный параметр `throwable_type` (приведение enum к int) на рендерере. |
| Интеграция с `OnAttack()` | В основном `ViewModel.OnAttack()` проверяется `IsThrowable` для запуска анимации броска (`b_throw`, `b_deploy_new`, `b_pull`). |

## Создай файл

**Путь:** `Code/Game/Weapon/WeaponModel/ViewModel.Throwables.cs`

```csharp
public partial class ViewModel
{
	/// <summary>
	/// Throwable type
	/// </summary>
	public enum Throwable
	{
		HEGrenade,
		SmokeGrenade,
		StunGrenade,
		Molotov,
		Flashbang
	}

	/// <summary>
	/// Is this a throwable?
	/// </summary>
	[Property, FeatureEnabled( "Throwables" )] public bool IsThrowable { get; set; }

	/// <summary>
	/// The throwable type
	/// </summary>
	[Property, Feature( "Throwables" )] public Throwable ThrowableType { get; set; }

	protected override void OnEnabled()
	{
		if ( IsThrowable )
		{
			Renderer?.Set( "throwable_type", (int)ThrowableType );
		}
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] Перечисление `Throwable` содержит 5 типов гранат
- [ ] При `IsThrowable = true` — устанавливается параметр `throwable_type` на рендерере
- [ ] `[FeatureEnabled]` и `[Feature]` корректно группируют свойства в инспекторе
- [ ] Интеграция с `OnAttack()` работает — при броске запускается анимация
