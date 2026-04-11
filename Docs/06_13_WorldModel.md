# 06.13 — Мировая модель оружия (WorldModel) 🌐

## Что мы делаем?

Создаём класс `WorldModel` — модель оружия, видимую другим игрокам (третье лицо). Он наследует `WeaponModel` и реализует минимальный набор методов для атаки и эффектов дальнего боя.

## Зачем это нужно?

- **Видимость для других**: когда игрок держит оружие, все остальные видят мировую модель, прикреплённую к скелету персонажа.
- **Эффекты атаки**: `OnAttack()` — анимация и визуальные эффекты (вспышка, гильза) для зрителей.
- **Трассер только если нет вью-модели**: `CreateRangedEffects()` создаёт трассер только когда у оружия нет вью-модели (для наблюдателей или третьего лица).

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `sealed class WorldModel` | Финальный класс — не предполагает наследников. |
| `OnAttack()` | Устанавливает `b_attack = true` на рендерере, вызывает `DoMuzzleEffect()` и `DoEjectBrass()` из базового `WeaponModel`. |
| `CreateRangedEffects()` | Проверяет, есть ли вью-модель у оружия (`weapon.ViewModel.IsValid()`). Если есть — трассер уже создан вью-моделью, пропускаем. Если нет — создаём трассер через `DoTracerEffect()`. |
| Наследование от `WeaponModel` | Получает все свойства: `Renderer`, `MuzzleTransform`, `EjectTransform`, `MuzzleEffect`, `EjectBrass`, `TracerEffect` и все методы `Do*()`. |

## Создай файл

**Путь:** `Code/Game/Weapon/WeaponModel/WorldModel.cs`

```csharp
public sealed class WorldModel : WeaponModel
{
	public void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();
	}

	public void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		if ( weapon.ViewModel.IsValid() )
			return;

		DoTracerEffect( hitPoint, origin );
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `OnAttack()` запускает анимацию атаки и создаёт дульную вспышку + гильзу
- [ ] `CreateRangedEffects()` создаёт трассер только при отсутствии вью-модели
- [ ] Класс `sealed` — не допускает наследников
