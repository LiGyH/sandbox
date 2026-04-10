# 07.08 — Вью-модель снайперки (SniperViewModel) 👁️

## Что мы делаем?

Создаём компонент `SniperViewModel`, который опускает модель оружия вниз за экран при активном оптическом прицеле.

## Зачем это нужно?

- **Реалистичное прицеливание**: при включении скопа модель оружия в руках должна исчезнуть из поля зрения, уступив место оверлею прицела.
- **Плавная анимация**: вью-модель плавно опускается вниз через `LerpTo`, а не резко исчезает.
- **Модульность**: компонент добавляется к префабу вью-модели снайперки и автоматически находит родительский `SniperWeapon`.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `Component, ICameraSetup` | Компонент реализует интерфейс `ICameraSetup` для встраивания в цепочку настройки камеры. |
| `PostSetup()` | Вызывается после основной настройки камеры каждый кадр. |
| `GetComponentInParent<SniperWeapon>()` | Находит родительский компонент `SniperWeapon` в иерархии. |
| `weapon.IsScoped` | Проверяет, активен ли оптический прицел. |
| `_offset` | Текущее смещение вниз. Плавно интерполируется: 0 → 3 (прицел включён) или 3 → 0 (прицел выключен). |
| `LerpTo( target, Time.Delta * 15f )` | Быстрая интерполяция (множитель 15) для отзывчивости. |
| `cc.WorldRotation.Down * _offset` | Смещает вью-модель вниз относительно текущей ориентации камеры. |

## Создай файл

**Путь:** `Code/Weapons/Sniper/SniperViewModel.cs`

```csharp
/// <summary>
/// Add to the sniper viewmodel prefab. Looks up the parent SniperWeapon
/// and pushes the viewmodel down out of view when scoped.
/// </summary>
public sealed class SniperViewModel : Component, ICameraSetup
{
	private float _offset;

	void ICameraSetup.PostSetup( CameraComponent cc )
	{
		var weapon = GetComponentInParent<SniperWeapon>();
		if ( !weapon.IsValid() ) return;

		var target = weapon.IsScoped ? 3 : 0f;
		_offset = _offset.LerpTo( target, Time.Delta * 15f );

		if ( _offset > 0.01f )
		{
			WorldPosition += cc.WorldRotation.Down * _offset;
		}
	}
}
```

## Проверка

1. Включите оптический прицел на снайперской винтовке — модель оружия должна плавно уехать вниз за экран.
2. Выключите прицел — модель должна плавно вернуться в нормальное положение.
3. Быстро переключайте прицел — переход должен быть плавным, без рывков.
4. Убедитесь, что компонент `SniperViewModel` присутствует на префабе вью-модели снайперки.
