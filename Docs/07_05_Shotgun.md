# 07.05 — Дробовик (ShotgunWeapon) 🔫

## Что мы делаем?

Создаём конкретный класс оружия `ShotgunWeapon` — дробовик, стреляющий множественными пеллетами (дробинками), наследующий `IronSightsWeapon`.

## Зачем это нужно?

- **Множественные пеллеты**: один выстрел порождает несколько лучей (`PelletCount`), каждый с собственным случайным отклонением.
- **Полуавтоматический огонь**: стреляет при нажатии, а не при удержании.
- **Полная логика выстрела**: в отличие от пистолетов, дробовик полностью переопределяет `PrimaryAttack()`, вручную управляя проверкой боезапаса, задержкой, трассировкой каждого пеллета, отдачей и эффектами.
- **Круговой прицел**: динамический круг, радиус которого зависит от текущего разброса.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `PrimaryFireRate` | `[Property]` — 0.8 сек между выстрелами (медленный). |
| `PelletCount` | `[Property]` — количество дробинок (по умолчанию 8). |
| `WantsPrimaryAttack()` | `Input.Pressed` — полуавтомат. |
| `PrimaryAttack()` | Полностью кастомная логика: проверка боезапаса → авто-перезарядка → расход патрона → цикл по `PelletCount` пеллетам → для каждого: отклонение через `WithAimCone` → трассировка с хитбоксами → эффекты и урон. |
| Владелец vs. предмет | Дробовик может стрелять и без владельца (например, упавший на землю): тогда луч идёт от дула, а отдача прикладывается как физическая сила через `Rigidbody`. |
| `ShootEffects()` | Первый пеллет (`i == 0`) запускает все эффекты, остальные — без событий (`noEvents: true`). |
| `TraceAttack()` | Каждый пеллет наносит урон независимо. |
| Отдача | Добавляет случайный угол к `EyeAngles` владельца + камерный recoil-шейк. |
| `DrawCrosshair()` | Рисует окружность из 32 сегментов + точку в центре. Радиус = 20 + разброс × 40. |

## Создай файл

**Путь:** `Code/Weapons/Shotgun/ShotgunWeapon.cs`

```csharp
using Sandbox.Rendering;

public class ShotgunWeapon : IronSightsWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 0.8f;
	[Property] public int PelletCount { get; set; } = 8;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		if ( HasOwner && ( !HasAmmo() || IsReloading() ) )
		{
			TryAutoReload();
			return;
		}

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( PrimaryFireRate );

		Vector3 eyeForward;
		Ray eyeRay;
		GameObject ignoreRoot;

		if ( HasOwner )
		{
			eyeForward = Owner.EyeTransform.Rotation.Forward;
			eyeRay = Owner.EyeTransform.ForwardRay;
			ignoreRoot = Owner.GameObject;
		}
		else
		{
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
			eyeForward = muzzle.Rotation.Forward;
			eyeRay = new Ray( muzzle.Position, eyeForward );
			ignoreRoot = GameObject;
		}

		for ( var i = 0; i < PelletCount; i++ )
		{
			var aimConeAmount = GetAimConeAmount();
			var forward = eyeForward
				.WithAimCone(
					Bullet.AimConeBase.x + aimConeAmount * Bullet.AimConeSpread.x,
					Bullet.AimConeBase.y + aimConeAmount * Bullet.AimConeSpread.y
				);

			var tr = Scene.Trace.Ray( eyeRay with { Forward = forward }, Bullet.Range )
				.IgnoreGameObjectHierarchy( ignoreRoot )
				.WithoutTags( "playercontroller" )
				.Radius( Bullet.BulletRadius )
				.UseHitboxes()
				.Run();

			ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface, noEvents: i > 0 );
			TraceAttack( TraceAttackInfo.From( tr, Bullet.Damage ) );
		}

		TimeSinceShoot = 0;

		if ( !HasOwner )
		{
			if ( ShootForce > 0f && GetComponent<Rigidbody>( true ) is var rb )
			{
				var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
				rb.ApplyForce( muzzle.Rotation.Up * ShootForce );
			}
			return;
		}

		Owner.Controller.EyeAngles += new Angles(
			Random.Shared.Float( Bullet.RecoilPitch.x, Bullet.RecoilPitch.y ),
			Random.Shared.Float( Bullet.RecoilYaw.x, Bullet.RecoilYaw.y ),
			0
		);

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			_ = new Sandbox.CameraNoise.Recoil( Bullet.CameraRecoilStrength, Bullet.CameraRecoilFrequency );
		}
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var spread = GetAimConeAmount();
		var radius = 20 + spread * 40;

		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );

		const int segments = 32;
		for ( var i = 0; i < segments; i++ )
		{
			var a1 = MathF.PI * 2f * i / segments;
			var a2 = MathF.PI * 2f * (i + 1) / segments;
			var p1 = center + new Vector2( MathF.Cos( a1 ), MathF.Sin( a1 ) ) * radius;
			var p2 = center + new Vector2( MathF.Cos( a2 ), MathF.Sin( a2 ) ) * radius;
			hud.DrawLine( p1, p2, 2f, color );
		}

		hud.DrawCircle( center, 3, color );
	}
}
```

## Проверка

1. Возьмите дробовик и убедитесь, что один выстрел порождает 8 следов попаданий (на стене видны несколько отметок).
2. Проверьте, что стрельба полуавтоматическая — одно нажатие = один залп.
3. Убедитесь, что круговой прицел расширяется при стрельбе и сужается в покое.
4. Проверьте авто-перезарядку при попытке стрелять с пустым магазином.
5. Проверьте, что отдача ощущается: камера дёргается вверх при каждом выстреле.
