# 07.06 — Снайперская винтовка (SniperWeapon) 🎯

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём конкретный класс оружия `SniperWeapon` — снайперскую винтовку с оптическим прицелом, наследующую `BaseBulletWeapon`.

## Зачем это нужно?

- **Оптический прицел (scope)**: ПКМ включает/выключает прицел с уменьшенным FOV и пост-эффектом размытия.
- **Полуавтоматический огонь**: один выстрел на нажатие, с затворной анимацией (bolt action) после отпускания кнопки.
- **Чувствительность прицела**: при включённом скопе чувствительность мыши уменьшается (`ScopeSensitivity`).
- **Динамическое размытие**: при движении мыши или персонажа прицел размывается, имитируя дрожание рук.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `BaseBulletWeapon` | Базовый класс (не `IronSightsWeapon`) — снайперка не использует мушку. |
| `PrimaryFireRate` | `[Property]` — 1.2 сек между выстрелами (медленный). |
| `ScopedFov` | `[Property]` — FOV при прицеливании (20°). |
| `ScopeSensitivity` | `[Property]` — множитель чувствительности в прицеле (0.3). |
| `_isScoped` | Флаг состояния прицела. |
| `_hasFired` | Отслеживает, был ли произведён выстрел, для запуска анимации затвора. |
| `SecondaryAttack()` | Переключает прицел через `SetScoped()`. |
| `SetScoped()` | Создаёт/уничтожает `SniperScopeEffect` на камере сцены. |
| `OnControl()` | При отпускании ЛКМ после выстрела запускает анимацию затвора (`b_reload_bolt`). |
| `OnCameraSetup()` | Устанавливает `camera.FieldOfView = ScopedFov` при активном прицеле. |
| `OnCameraMove()` | Умножает углы мыши на `ScopeSensitivity` при прицеливании. |
| `DrawHud()` | При прицеле — рисует оверлей с размытием; без прицела — стандартный прицел-точку. |
| `DrawScopeOverlay()` | Рассчитывает размытие из дельты мыши и скорости игрока, передаёт в `SniperScopeEffect.BlurInput`. |

## Создай файл

**Путь:** `Code/Weapons/Sniper/SniperWeapon.cs`

```csharp
using Sandbox.Rendering;

public class SniperWeapon : BaseBulletWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 1.2f;
	[Property] public float ScopedFov { get; set; } = 20f;
	[Property] public float ScopeSensitivity { get; set; } = 0.3f;

	private bool _isScoped;
	private float _mouseDelta;
	private SniperScopeEffect _scopeEffect;
	private bool _hasFired;

	public bool IsScoped => _isScoped;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	protected override bool WantsSecondaryAttack()
	{
		return Input.Pressed( "attack2" );
	}

	public override bool CanSecondaryAttack()
	{
		return true;
	}

	public override void PrimaryAttack()
	{
		ShootBullet( PrimaryFireRate );
		_hasFired = true;
	}

	public override void SecondaryAttack()
	{
		SetScoped( !_isScoped );
	}

	private void SetScoped( bool scoped )
	{
		_isScoped = scoped;

		if ( _isScoped )
		{
			_scopeEffect = Scene.Camera.Components.GetOrCreate<SniperScopeEffect>();
			_scopeEffect.Flags |= ComponentFlags.NotNetworked;
		}
		else
		{
			_scopeEffect?.Destroy();
			_scopeEffect = default;
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( _isScoped )
			SetScoped( false );
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( _hasFired && Input.Released( "attack1" ) )
		{
			_hasFired = false;
			ViewModel?.RunEvent<ViewModel>( x => x.Renderer?.Set( "b_reload_bolt", true ) );
		}
	}

	public override void OnCameraSetup( Player player, CameraComponent camera )
	{
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		if ( _isScoped )
		{
			camera.FieldOfView = ScopedFov;
		}
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		_mouseDelta = new Vector2( angles.yaw, angles.pitch ).Length;

		if ( _isScoped )
		{
			angles *= ScopeSensitivity;
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( _isScoped )
		{
			DrawScopeOverlay( painter, crosshair );
			return;
		}

		DrawCrosshair( painter, crosshair );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawCircle( center, 5, Color.Black );
		hud.DrawCircle( center, 3, color );
	}

	private void DrawScopeOverlay( HudPainter hud, Vector2 center )
	{
		if ( !_scopeEffect.IsValid() )
			return;

		var mouseBlur = _mouseDelta * 0.25f;
		var velocityBlur = HasOwner ? (Owner.Controller.Velocity.Length / 300f).Clamp( 0f, 1f ) : 0f;

		_scopeEffect.BlurInput = MathF.Min( mouseBlur + velocityBlur, 1f );
	}
}
```

## Проверка

1. Возьмите снайперскую винтовку и нажмите ПКМ — должен включиться оптический прицел с уменьшенным FOV.
2. Повторное нажатие ПКМ — прицел выключается.
3. Проверьте, что при прицеливании чувствительность мыши снижена.
4. Стреляйте и отпустите ЛКМ — должна проиграться анимация затвора.
5. Двигайте мышь в прицеле — изображение должно слегка размываться при быстром движении.
6. Бегите с прицелом — размытие должно усиливаться.


---

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


---

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


---

## ➡️ Следующий шаг

Переходи к **[07.07 — Монтировка (CrowbarWeapon) 🔧](07_07_Crowbar.md)**.
