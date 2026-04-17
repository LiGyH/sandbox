# 06.06 — Стрелковое оружие (BaseBulletWeapon) 🔫

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём класс `BaseBulletWeapon` — базу для огнестрельного оружия, стреляющего пулями. Он наследует `BaseWeapon` и добавляет систему трассировки лучей, отдачу, конус прицеливания и эффекты попаданий.

## Зачем это нужно?

- **Конфигурация пули**: `BulletConfiguration` — record struct со всеми параметрами: урон, радиус, дальность, разброс, отдача.
- **Конус прицеливания (Aim Cone)**: базовый разброс + динамический спред, восстанавливающийся со временем (`AimConeRecovery`).
- **Двойной режим**: при наличии владельца стреляет от глаз игрока с отдачей; без владельца — прямо от дула (для турелей / NPC).
- **Эффекты**: `ShootEffects()` (RPC Broadcast) — дульная вспышка, гильзы, трейсер, декаль попадания, привязка к костям.
- **Физика**: `ShootForce` — сила отдачи для автономного оружия.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `BulletConfiguration` | Record struct с полями: `Damage`, `BulletRadius`, `Range`, `AimConeBase/Spread/Recovery`, `RecoilPitch/Yaw`, `CameraRecoilStrength/Frequency`. |
| `ShootBullet(fireRate, config)` | Главный метод стрельбы: проверка патронов → расход → задержка → расчёт луча → `Scene.Trace.Ray()` → эффекты → `TraceAttack()` → отдача камеры. |
| `GetAimConeAmount()` | Возвращает 0–1 на основе `TimeSinceShoot` и `AimConeRecovery` — чем дольше не стрелял, тем точнее. |
| `WithAimCone()` | Метод расширения для вектора — добавляет случайное отклонение в конус. |
| `ShootEffects()` | `[Rpc.Broadcast]` — воспроизводит визуальные/звуковые эффекты на всех клиентах. Создаёт декаль из `Surface.PrefabCollection.BulletImpact`, привязывает к ближайшей кости. |
| `CameraNoise.Recoil` | Создаёт эффект тряски камеры при выстреле в первом лице. |
| Автономный режим | Без владельца: стреляет от `MuzzleTransform`, применяет физическую силу `ShootForce` к `Rigidbody` оружия. |
| Поиск ближайшей кости | При попадании в `SkinnedModelRenderer` — декаль привязывается к ближайшей кости для корректного движения. |

## Создай файл

**Путь:** `Code/Game/Weapon/BaseBulletWeapon/BaseBulletWeapon.cs`

```csharp
public partial class BaseBulletWeapon : BaseWeapon
{
	[Property]
	public SoundEvent ShootSound { get; set; }

	[Property, Group( "Bullet" )]
	public BulletConfiguration Bullet { get; set; } = new()
	{
		Damage = 12f,
		BulletRadius = 1f,
		Range = 4096f,
		AimConeBase = new Vector2( 0.5f, 0.25f ),
		AimConeSpread = new Vector2( 3f, 3f ),
		AimConeRecovery = 0.2f,
		RecoilPitch = new Vector2( -0.3f, -0.1f ),
		RecoilYaw = new Vector2( -0.1f, 0.1f ),
		CameraRecoilStrength = 1f,
		CameraRecoilFrequency = 1f,
	};

	[Property, Group( "Bullet" ), ClientEditable, Range( 0f, 500000f, 10f )]
	public float ShootForce { get; set; } = 100000f;

	protected TimeSince TimeSinceShoot = 0;

	/// <summary>
	/// Returns 0 for no aim spread, 1 for full aim cone, based on time since last shot.
	/// </summary>
	protected float GetAimConeAmount( float recovery )
	{
		return TimeSinceShoot.Relative.Remap( 0, recovery, 1, 0 );
	}

	/// <summary>
	/// Returns the aim cone amount using the configured recovery time
	/// </summary>
	protected float GetAimConeAmount()
	{
		return GetAimConeAmount( Bullet.AimConeRecovery );
	}

	/// <inheritdoc cref="ShootBullet(float, in BulletConfiguration)"/>
	protected void ShootBullet( float fireRate )
	{
		ShootBullet( fireRate, Bullet );
	}

	/// <summary>
	/// Shoot a bullet out of the front of the gun.
	/// When held by a player, fires from the player's eye with aim cone and recoil.
	/// When standalone (no owner), fires straight from the weapon's muzzle.
	/// </summary>
	protected void ShootBullet( float fireRate, in BulletConfiguration config )
	{
		if ( HasOwner && ( !HasAmmo() || IsReloading() ) )
		{
			TryAutoReload();
			return;
		}

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		// Only consume ammo when held by a player
		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( fireRate );

		Vector3 forward;
		Ray traceRay;
		GameObject ignoreRoot;

		if ( HasOwner )
		{
			// Player-held: shoot from eye with aim cone spread
			var aimConeAmount = GetAimConeAmount( config.AimConeRecovery );
			forward = Owner.EyeTransform.Rotation.Forward
				.WithAimCone(
					config.AimConeBase.x + aimConeAmount * config.AimConeSpread.x,
					config.AimConeBase.y + aimConeAmount * config.AimConeSpread.y
				);
			traceRay = Owner.EyeTransform.ForwardRay with { Forward = forward };
			ignoreRoot = Owner.GameObject;
		}
		else
		{
			// Standalone: shoot straight from the weapon's muzzle
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
			forward = muzzle.Rotation.Forward;
			traceRay = new Ray( muzzle.Position, forward );
			ignoreRoot = GameObject;
		}

		var tr = Scene.Trace.Ray( traceRay, config.Range )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.WithoutTags( "playercontroller" )
			.Radius( config.BulletRadius )
			.UseHitboxes()
			.Run();

		ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, config.Damage ) );
		TimeSinceShoot = 0;

		// Recoil only applies when held by a player
		if ( !HasOwner )
		{
			// Simulate physical recoil by pushing the weapon opposite to its fire direction
			if ( ShootForce > 0f && GetComponent<Rigidbody>( true ) is var rb )
			{
				var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
				rb.ApplyForce( muzzle.Rotation.Up * ShootForce );
			}
			return;
		}

		Owner.Controller.EyeAngles += new Angles(
			Random.Shared.Float( config.RecoilPitch.x, config.RecoilPitch.y ),
			Random.Shared.Float( config.RecoilYaw.x, config.RecoilYaw.y ),
			0
		);

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			_ = new Sandbox.CameraNoise.Recoil( config.CameraRecoilStrength, config.CameraRecoilFrequency );
		}
	}

	[Rpc.Broadcast]
	public void ShootEffects( Vector3 hitpoint, bool hit, Vector3 normal, GameObject hitObject, Surface hitSurface, Vector3? origin = null, bool noEvents = false )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !hitSurface.IsValid() ) return;

		Owner?.Controller.Renderer.Set( "b_attack", true );

		if ( !noEvents )
		{
			if ( ViewModel.IsValid() )
			{
				ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
				ViewModel.RunEvent<ViewModel>( x => x.CreateRangedEffects( this, hitpoint, origin ) );
			}
			else if ( WorldModel.IsValid() )
			{
				WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );
				WorldModel.RunEvent<WorldModel>( x => x.CreateRangedEffects( this, hitpoint, origin ) );
			}

			if ( ShootSound.IsValid() )
			{
				var snd = GameObject.PlaySound( ShootSound );

				// If we're shooting, the sound should not be spatialized
				if ( HasOwner && Owner.IsLocalPlayer && snd.IsValid() )
				{
					snd.SpacialBlend = 0;
				}
			}
		}

		if ( !hit || !hitObject.IsValid() )
			return;

		var prefab = hitSurface.PrefabCollection.BulletImpact ?? hitSurface.GetBaseSurface()?.PrefabCollection.BulletImpact;

		// Still null?
		if ( prefab is null )
			return;

		var fwd = Rotation.LookAt( normal * -1.0f, Vector3.Random );

		var impact = prefab.Clone();
		impact.WorldPosition = hitpoint;
		impact.WorldRotation = fwd;
		impact.SetParent( hitObject, true );

		if ( hitObject.GetComponentInChildren<SkinnedModelRenderer>() is not { CreateBoneObjects: true } skinned )
			return;

		// find closest bone
		var bones = skinned.GetBoneTransforms( true );

		var closestDist = float.MaxValue;

		for ( var i = 0; i < bones.Length; i++ )
		{
			var bone = bones[i];
			var dist = bone.Position.Distance( hitpoint );
			if ( dist < closestDist )
			{
				closestDist = dist;
				impact.SetParent( skinned.GetBoneObject( i ), true );
			}
		}
	}

	public record struct BulletConfiguration
	{
		public float Damage { get; set; }
		public float BulletRadius { get; set; }
		public Vector2 AimConeBase { get; set; }
		public Vector2 AimConeSpread { get; set; }
		public float AimConeRecovery { get; set; }
		public Vector2 RecoilPitch { get; set; }
		public Vector2 RecoilYaw { get; set; }
		public float CameraRecoilStrength { get; set; }
		public float CameraRecoilFrequency { get; set; }
		public float Range { get; set; }
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `ShootBullet()` корректно стреляет от глаз игрока или от дула (автономный режим)
- [ ] Конус прицеливания восстанавливается со временем
- [ ] Отдача применяется к `EyeAngles` и камере
- [ ] `ShootEffects()` создаёт дульную вспышку, звук, декаль попадания
- [ ] Декаль привязывается к ближайшей кости скелета
- [ ] `BulletConfiguration` содержит все необходимые параметры


---

## ➡️ Следующий шаг

Переходи к **[06.07 — Оружие с механическим прицелом (IronSightsWeapon) 🎯](06_07_IronSightsWeapon.md)**.
