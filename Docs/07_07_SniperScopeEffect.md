# 07.07 — Эффект оптического прицела (SniperScopeEffect) 🔭

## Что мы делаем?

Создаём пост-процессный эффект `SniperScopeEffect`, который применяет шейдер оптического прицела снайперской винтовки с динамическим размытием.

## Зачем это нужно?

- **Визуализация прицела**: при включении скопа на экране появляется эффект снайперского прицела через пост-процесс шейдер.
- **Динамическое размытие**: значение `BlurInput` (задаётся из `SniperWeapon`) управляет чёткостью изображения в прицеле — при движении мыши или персонажа изображение размывается.
- **Плавность**: размытие интерполируется (`LerpTo`) для плавного перехода между состояниями.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `BasePostProcess<SniperScopeEffect>` | Базовый класс для создания пост-процесс эффектов. |
| `BlurInput` | Входное значение размытия (0–1), задаётся извне из `SniperWeapon`. |
| `_smoothedBlur` | Сглаженное значение размытия через `LerpTo` с множителем `Time.Delta * 8`. |
| `blurAmount` | Инвертированное значение: `1 - _smoothedBlur`, зажатое в диапазон [0.1, 1.0]. При 1.0 — чёткое изображение, при 0.1 — максимальное размытие. |
| `_cachedMaterial` | Кэшированный материал шейдера `sniper_scope.shader`, загружается один раз. |
| `Render()` | Устанавливает атрибуты шейдера (`BlurAmount`, `Offset`) и выполняет blit на стадии `AfterPostProcess`. |
| `Stage.AfterPostProcess` | Эффект применяется после всех остальных пост-процессов. |

## Создай файл

**Путь:** `Code/Weapons/Sniper/SniperScopeEffect.cs`

```csharp
using Sandbox.Rendering;

public sealed class SniperScopeEffect : BasePostProcess<SniperScopeEffect>
{
	public float BlurInput { get; set; }

	private float _smoothedBlur;
	private static Material _cachedMaterial;

	public override void Render()
	{
		_smoothedBlur = _smoothedBlur.LerpTo( BlurInput, Time.Delta * 8f );
		var blurAmount = (1.0f - _smoothedBlur).Clamp( 0.1f, 1f );

		Attributes.Set( "BlurAmount", blurAmount );
		Attributes.Set( "Offset", Vector2.Zero );

		_cachedMaterial ??= Material.FromShader( "shaders/postprocess/sniper_scope.shader" );
		var blit = BlitMode.WithBackbuffer( _cachedMaterial, Stage.AfterPostProcess, 200, false );
		Blit( blit, "SniperScope" );
	}
}
```

## Проверка

1. Включите оптический прицел на снайперской винтовке (ПКМ) — на экране должен появиться эффект прицела.
2. Стойте неподвижно — изображение в прицеле должно быть чётким.
3. Быстро двигайте мышь — изображение должно размыться.
4. Бегите — размытие должно усилиться.
5. Остановитесь — размытие должно плавно исчезнуть (интерполяция через `LerpTo`).
6. Выключите прицел (ПКМ) — эффект должен полностью исчезнуть (компонент уничтожается).
