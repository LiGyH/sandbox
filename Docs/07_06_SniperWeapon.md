# 07.06 — Снайперская винтовка (SniperWeapon) 🎯

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
