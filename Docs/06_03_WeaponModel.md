# 06.03 — Модель оружия (WeaponModel) 🔧

## Что мы делаем?

Создаём абстрактный базовый класс `WeaponModel` — общую логику для `ViewModel` (первое лицо) и `WorldModel` (третье лицо). Он управляет рендерером, трансформами для дула/гильзоприёмника и создаёт визуальные эффекты выстрела.

## Зачем это нужно?

- **Общий интерфейс**: и вью-модель, и мировая модель имеют одинаковые точки (дуло, гильзоприёмник) и одинаковые методы для создания эффектов.
- **Дульная вспышка**: `DoMuzzleEffect()` — клонирует эффект в точку дула.
- **Трассер**: `DoTracerEffect()` — клонирует эффект трассера и задаёт конечную точку.
- **Выброс гильзы**: `DoEjectBrass()` — клонирует гильзу, задаёт направление и вращение.
- **Развёртывание**: `Deploy()` — устанавливает анимационный параметр `b_deploy` для анимации доставания оружия.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `Renderer` | `SkinnedModelRenderer` — основной рендерер модели. |
| `MuzzleTransform` | `GameObject` — точка откуда вылетают пули / вспышка. |
| `EjectTransform` | `GameObject` — точка откуда вылетают гильзы. |
| `MuzzleEffect` | Префаб дульной вспышки. |
| `EjectBrass` | Префаб гильзы. |
| `TracerEffect` | Префаб трассера. |
| `Deploy()` | Устанавливает `b_deploy = true` на рендерере. |
| `GetTracerOrigin()` | Возвращает трансформ дула или объекта по умолчанию. |
| `DoTracerEffect()` | Клонирует трассер, ищет компонент `Tracer` и задаёт `EndPoint`. |
| `DoEjectBrass()` | Клонирует гильзу, вычисляет направление выброса (вперёд + вправо + случайность), задаёт скорость и угловую скорость `Rigidbody`. |
| `DoMuzzleEffect()` | Клонирует вспышку как дочерний объект `MuzzleTransform` с нулевым трансформом. |

## Создай файл

**Путь:** `Code/Game/Weapon/WeaponModel/WeaponModel.cs`

```csharp
﻿public abstract class WeaponModel : Component
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public GameObject MuzzleTransform { get; set; }
	[Property] public GameObject EjectTransform { get; set; }
	[Property] public GameObject MuzzleEffect { get; set; }
	[Property] public GameObject EjectBrass { get; set; }
	[Property] public GameObject TracerEffect { get; set; }

	public void Deploy()
	{
		Renderer?.Set( "b_deploy", true );
	}

	public Transform GetTracerOrigin()
	{
		if ( MuzzleTransform.IsValid() )
			return MuzzleTransform.WorldTransform;

		return WorldTransform;
	}

	public void DoTracerEffect( Vector3 hitPoint, Vector3? origin = null )
	{
		if ( !TracerEffect.IsValid() ) return;

		var tracerOrigin = GetTracerOrigin().WithScale( 1 );
		if ( origin.HasValue ) tracerOrigin = tracerOrigin.WithPosition( origin.Value );

		var effect = TracerEffect.Clone( new CloneConfig { Transform = tracerOrigin, StartEnabled = true } );

		if ( effect.GetComponentInChildren<Tracer>() is Tracer tracer )
		{
			tracer.EndPoint = hitPoint;
		}
	}

	public void DoEjectBrass()
	{
		if ( !EjectBrass.IsValid() ) return;
		if ( !EjectTransform.IsValid() ) return;

		var effect = EjectBrass.Clone( new CloneConfig { Transform = EjectTransform.WorldTransform.WithScale( 1 ), StartEnabled = true } );
		effect.WorldRotation = effect.WorldRotation * new Angles( 90, 0, 0 );

		var ejectDirection = (EjectTransform.WorldRotation.Forward * 250 + (EjectTransform.WorldRotation.Right + Vector3.Random * -0.35f) * 250);

		var rb = effect.GetComponentInChildren<Rigidbody>();
		rb.Velocity = ejectDirection;
		rb.AngularVelocity = EjectTransform.WorldRotation.Right * 50f;
	}

	public void DoMuzzleEffect()
	{
		if ( !MuzzleEffect.IsValid() ) return;
		if ( !MuzzleTransform.IsValid() ) return;

		MuzzleEffect.Clone( new CloneConfig { Parent = MuzzleTransform, Transform = global::Transform.Zero, StartEnabled = true } );
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `Deploy()` устанавливает параметр анимации `b_deploy`
- [ ] `DoTracerEffect()` клонирует трассер и задаёт конечную точку
- [ ] `DoEjectBrass()` клонирует гильзу с правильным направлением и вращением
- [ ] `DoMuzzleEffect()` клонирует вспышку в точку дула
- [ ] `GetTracerOrigin()` возвращает трансформ дула или объекта по умолчанию


---

# 06.11 — Вью-модель (ViewModel) 👀

## Что мы делаем?

Создаём класс `ViewModel` — модель оружия от первого лица. Она наследует `WeaponModel` и реализует `ICameraSetup` для привязки к камере. Управляет анимацией ходьбы, стрельбы, перезарядки и инерцией при повороте камеры.

## Зачем это нужно?

- **Первое лицо**: вью-модель следует за камерой и отображает оружие перед глазами игрока.
- **Инерция**: при повороте мыши оружие «отстаёт» — создаёт ощущение веса (`InertiaScale`).
- **Анимация**: множество параметров управляют анимацией: движение, прицеливание, атака, перезарядка.
- **Камера**: через `ICameraSetup` вью-модель может влиять на позицию/вращение камеры (кость `camera` на модели).
- **Перезарядка**: поддержка обычной и инкрементальной перезарядки с различными скоростями анимации.
- **Гранаты**: поддержка бросков (`IsThrowable`) через частичный класс.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `HideViewModel` (ConVar) | Читерский конвар для скрытия вью-модели. |
| `IsIncremental` | Режим инкрементальной перезарядки (как у дробовика). |
| `AnimationSpeed / IncrementalAnimationSpeed` | Скорости анимации для обычной и инкрементальной перезарядки. |
| `InertiaScale` | Масштаб инерции — насколько сильно оружие «отстаёт» от камеры. |
| `ApplyInertia()` | Вычисляет дельту pitch/yaw между кадрами для анимационных параметров `aim_pitch_inertia` / `aim_yaw_inertia`. |
| `ICameraSetup.Setup()` | Привязывает вью-модель к позиции/вращению камеры, вызывает `ApplyInertia()` и `ApplyAnimationTransform()`. |
| `ApplyAnimationTransform()` | Ищет кость `camera` на модели — если есть, сдвигает камеру (для анимации отдачи и т.д.). |
| `UpdateAnimation()` | Устанавливает параметры: `move_bob`, `aim_pitch`, `aim_yaw`, `move_speed`, `move_direction` и другие. |
| `OnAttack()` | Устанавливает `b_attack`, создаёт дульную вспышку и гильзу. Для бросков — `b_throw`. |
| `OnReloadStart/OnIncrementalReload/OnReloadFinish` | Управление анимацией перезарядки через параметры `b_reload`, `b_reloading`, `b_reloading_shell`, `speed_reload`. |

## Создай файл

**Путь:** `Code/Game/Weapon/WeaponModel/ViewModel.cs`

```csharp
public sealed partial class ViewModel : WeaponModel, ICameraSetup
{
	[ConVar( "sbdm.hideviewmodel", ConVarFlags.Cheat )]
	private static bool HideViewModel { get; set; } = false;

	/// <summary>
	/// Turns on incremental reloading parameters.
	/// </summary>
	[Property, Group( "Animation" )]
	public bool IsIncremental { get; set; } = false;

	/// <summary>
	/// Animation speed in general.
	/// </summary>
	[Property, Group( "Animation" )]
	public float AnimationSpeed { get; set; } = 1.0f;

	/// <summary>
	/// Animation speed for incremental reload sections.
	/// </summary>
	[Property, Group( "Animation" )]
	public float IncrementalAnimationSpeed { get; set; } = 3.0f;

	/// <summary>
	/// How much inertia should this weapon have?
	/// </summary>
	[Property, Group( "Inertia" )]
	Vector2 InertiaScale { get; set; } = new Vector2( 2, 2 );

	public bool IsAttacking { get; set; }

	TimeSince AttackDuration;

	bool _reloadFinishing;
	TimeSince _reloadFinishTimer;

	Vector2 lastInertia;
	Vector2 currentInertia;
	bool isFirstUpdate = true;

	protected override void OnStart()
	{
		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			// Don't render shadows for viewmodels
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		}
	}

	protected override void OnUpdate()
	{
		UpdateAnimation();
	}

	void ApplyInertia()
	{
		var rot = Scene.Camera.WorldRotation.Angles();

		// Need to fetch data from the camera for the first frame
		if ( isFirstUpdate )
		{


			lastInertia = new Vector2( rot.pitch, rot.yaw );
			currentInertia = Vector2.Zero;
			isFirstUpdate = false;
		}

		var newPitch = rot.pitch;
		var newYaw = rot.yaw;

		currentInertia = new Vector2( Angles.NormalizeAngle( newPitch - lastInertia.x ), Angles.NormalizeAngle( lastInertia.y - newYaw ) );
		lastInertia = new( newPitch, newYaw );
	}

	void ICameraSetup.Setup( CameraComponent cc )
	{
		Renderer.Enabled = !HideViewModel;

		WorldPosition = cc.WorldPosition;
		WorldRotation = cc.WorldRotation;

		ApplyInertia();
		ApplyAnimationTransform( cc );
	}

	void ApplyAnimationTransform( CameraComponent cc )
	{
		if ( !Renderer.IsValid() ) return;

		if ( Renderer.TryGetBoneTransformLocal( "camera", out var bone ) )
		{
			var scale = 0.5f;
			// Применяем смещение/поворот «камера»-кости в мировом пространстве.
			// До перехода на WorldPosition/WorldRotation использовались LocalPosition/LocalRotation,
			// но это давало неправильное смещение, если у держателя viewmodel был ненулевой
			// мировой поворот (например, при наклонной камере / dead-камере).
			cc.WorldPosition += cc.WorldRotation * bone.Position * scale;
			cc.WorldRotation *= bone.Rotation * scale;
		}
	}

	void UpdateAnimation()
	{
		var playerController = GetComponentInParent<PlayerController>();
		if ( !playerController.IsValid() ) return;

		var rot = Scene.Camera.WorldRotation.Angles();

		Renderer.Set( "b_twohanded", true );
		Renderer.Set( "b_grounded", playerController.IsOnGround );
		Renderer.Set( "move_bob", GamePreferences.ViewBobbing ? playerController.Velocity.Length.Remap( 0, playerController.RunSpeed * 2f ) : 0 );

		Renderer.Set( "aim_pitch", rot.pitch );
		Renderer.Set( "aim_pitch_inertia", currentInertia.x * InertiaScale.x );

		Renderer.Set( "aim_yaw", rot.yaw );
		Renderer.Set( "aim_yaw_inertia", currentInertia.y * InertiaScale.y );

		Renderer.Set( "attack_hold", IsAttacking ? AttackDuration.Relative.Clamp( 0f, 1f ) : 0f );

		if ( _reloadFinishing && _reloadFinishTimer >= 0.5f )
		{
			_reloadFinishing = false;
			Renderer.Set( "speed_reload", AnimationSpeed );
			Renderer.Set( "b_reloading", false );
		}

		var velocity = playerController.Velocity;

		var dir = velocity;
		var forward = Scene.Camera.WorldRotation.Forward.Dot( dir );
		var sideward = Scene.Camera.WorldRotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Renderer.Set( "move_direction", angle );
		Renderer.Set( "move_speed", velocity.Length );
		Renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		Renderer.Set( "move_y", sideward );
		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_z", velocity.z );
	}

	public void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();

		if ( IsThrowable )
		{
			Renderer?.Set( "b_throw", true );

			Invoke( 0.5f, () =>
			{
				Renderer?.Set( "b_deploy_new", true );
				Renderer?.Set( "b_pull", false );
			} );
		}
	}

	public void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		DoTracerEffect( hitPoint, origin );
	}

	/// <summary>
	/// Called when starting to reload a weapon.
	/// </summary>
	public void OnReloadStart()
	{
		_reloadFinishing = false; // cancel any pending incremental finish from a previous reload
		Renderer?.Set( "speed_reload", AnimationSpeed );
		Renderer?.Set( IsIncremental ? "b_reloading" : "b_reload", true );
	}

	/// <summary>
	/// Called when incrementally reloading a weapon.
	/// </summary>
	public void OnIncrementalReload()
	{
		Renderer?.Set( "speed_reload", IncrementalAnimationSpeed );
		Renderer?.Set( "b_reloading_shell", true );
	}

	public void OnReloadFinish()
	{
		if ( IsIncremental )
		{
			_reloadFinishing = true;
			_reloadFinishTimer = 0;
		}
		else
		{
			Renderer?.Set( "b_reload", false );
		}
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] Вью-модель привязана к камере через `ICameraSetup.Setup()`
- [ ] Инерция вычисляется корректно между кадрами
- [ ] Анимация движения обновляется каждый кадр (`UpdateAnimation`)
- [ ] Тени отключены для вью-моделей (`ShadowRenderType.Off`)
- [ ] Перезарядка поддерживает обычный и инкрементальный режимы
- [ ] `OnAttack()` создаёт дульную вспышку, гильзу, и обрабатывает гранаты


---

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


---

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


---

## ➡️ Следующий шаг

Переходи к **[06.04 — Базовое оружие (BaseWeapon) ⚔️](06_04_BaseWeapon.md)**.
