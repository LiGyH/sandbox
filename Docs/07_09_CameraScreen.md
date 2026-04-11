# 07.10 — Фотокамера (CameraWeapon) 📷

## Что мы делаем?

Создаём конкретный класс оружия `CameraWeapon` — фотокамеру, которая позволяет игроку делать снимки с настраиваемым FOV, креном и глубиной резкости.

## Зачем это нужно?

- **Фотомод**: игрок может зумировать (ПКМ + движение мыши), наклонять камеру (ПКМ + горизонтальное движение) и делать скриншоты (ЛКМ).
- **Глубина резкости (DOF)**: автоматическая фокусировка на объекте в центре экрана с размытием фона.
- **Статистика**: каждый снимок увеличивает счётчик `photos` через `Services.Stats`.
- **Скрытие HUD**: при активной камере HUD полностью скрывается (`WantsHideHud`).
- **Эффект дрожания**: лёгкий шум Перлина имитирует дрожание рук фотографа.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `BaseWeapon` | Базовый класс оружия (не IronSights, не Bullet). |
| `fov` | Текущий FOV (по умолчанию 50°). Изменяется ПКМ + pitch мыши. Зажат в [1, 150]. |
| `roll` | Текущий крен камеры. Изменяется ПКМ + yaw мыши. |
| `dof` | Компонент `DepthOfField`, создаётся при включении, уничтожается при выключении. |
| `focusing` | Флаг: ЛКМ зажата — камера фокусируется. |
| `WantsHideHud` | `true` — скрывает весь HUD. |
| `OnEnabled()` | Создаёт `DepthOfField` на камере сцены с флагом `NotNetworked`. |
| `OnDisabled()` | Уничтожает DOF-компонент. |
| `OnCameraSetup()` | Устанавливает FOV, крен и добавляет Perlin-шум для дрожания. |
| `OnCameraMove()` | При зажатом ПКМ обнуляет движение камеры (зум, а не поворот). Масштабирует чувствительность по FOV. |
| `OnControl()` | `Reload` — сброс FOV/крена. ПКМ — зум/крен. ЛКМ отпущена — `TakeScreenshot()` + `Stats.Increment`. |
| `UpdateDepthOfField()` | Если не фокусируемся — трассировка от камеры, поиск точки фокуса. `BlurSize` зависит от FOV, `FocalDistance` плавно интерполируется. |
| `DrawHud()` | Пустой — HUD не рисуется. |

## Создай файл

**Путь:** `Code/Weapons/CameraWeapon.cs`

```csharp
using Sandbox.Rendering;
using Sandbox.Utility;

public class CameraWeapon : BaseWeapon
{
	float fov = 50;
	float roll = 0;

	DepthOfField dof;
	bool focusing;
	Vector3 focusPoint;

	[Property] SoundEvent CameraShoot { get; set; }

	public override bool WantsHideHud => true;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		dof = Scene.Camera.Components.GetOrCreate<DepthOfField>();
		dof.Flags |= ComponentFlags.NotNetworked;

		focusing = false;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( IsProxy )
			return;

		dof?.Destroy();
		dof = default;
	}

	/// <summary>
	/// We want to control the camera fov
	/// </summary>
	public override void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
		//Log.Info( $"{player.Network.IsOwner} {Network.IsOwner}" );
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		camera.FieldOfView = fov;
		camera.WorldRotation = camera.WorldRotation * new Angles( 0, 0, roll );

		var t = 20.0f;
		var s = 1.0f;

		var x = Noise.Perlin( Time.Now * t, 3, 5 ).Remap( 0, 1, -1, 1 ) * s;
		var y = Noise.Perlin( Time.Now * t * 0.8f, 3, 4 ).Remap( 0, 1, -1, 1 ) * s;

		camera.WorldRotation *= new Angles( x, y, 0 );

	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		// We're zooming
		if ( Input.Down( "attack2" ) )
		{
			angles = default;
		}

		float sensitivity = fov.Remap( 1, 70, 0.01f, 1 );
		angles *= sensitivity;
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "reload" ) )
		{
			fov = 50;
			roll = 0;
		}

		if ( Input.Down( "attack2" ) )
		{
			fov += Input.AnalogLook.pitch;
			fov = fov.Clamp( 1, 150 );
			roll -= Input.AnalogLook.yaw;
		}

		if ( dof.IsValid() )
		{
			UpdateDepthOfField( dof );
		}

		if ( focusing && Input.Released( "attack1" ) )
		{
			Game.TakeScreenshot();
			Sandbox.Services.Stats.Increment( "photos", 1 );

			GameObject?.PlaySound( CameraShoot );
		}

		focusing = Input.Down( "attack1" );
	}

	private void UpdateDepthOfField( DepthOfField dof )
	{
		if ( !focusing )
		{
			dof.BlurSize = Scene.Camera.FieldOfView.Remap( 20, 80, 25, 5 );
			dof.FocusRange = 1024;
			dof.FrontBlur = false;

			var tr = Scene.Trace.Ray( Scene.Camera.Transform.World.ForwardRay, 5000 )
								.Radius( 8 )
								.IgnoreGameObjectHierarchy( GameObject.Root )
								.Run();

			focusPoint = tr.EndPosition;
		}

		var target = Scene.Camera.WorldPosition.Distance( focusPoint ) + 32;

		dof.FocalDistance = dof.FocalDistance.LerpTo( target, Time.Delta * 10.0f );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing!
	}
}
```

## Проверка

1. Возьмите камеру — HUD должен исчезнуть.
2. Зажмите ПКМ и двигайте мышь вверх/вниз — должен меняться зум (FOV).
3. Зажмите ПКМ и двигайте мышь влево/вправо — должен меняться крен камеры.
4. Нажмите `R` (reload) — FOV и крен должны сброситься к значениям по умолчанию.
5. Нажмите и отпустите ЛКМ — должен быть сделан скриншот со звуком затвора.
6. Проверьте, что фон размыт (DOF), а объект в центре экрана — в фокусе.
7. Проверьте лёгкое дрожание камеры (Perlin noise).


---

# 07.13 — Оружие с экраном (ScreenWeapon) 📺

## Что мы делаем?

Создаём базовый класс `ScreenWeapon` — переносимый предмет с анимированным экраном на вью-модели, наследующий `BaseCarryable`.

## Зачем это нужно?

- **Экран на оружии**: позволяет рисовать динамический контент на экране модели (как на Toolgun).
- **Общая логика экрана**: рендер-таргет, материал экрана, обновление через `CommandList` — всё инкапсулировано.
- **Крутящаяся катушка (coil)**: визуальный эффект вращения элемента модели при стрельбе.
- **Джойстик**: обновление параметров `joystick_x` / `joystick_y` на основе движения мыши для анимации мини-джойстика на модели.
- **Переопределяемый контент**: метод `DrawScreenContent()` виртуальный — наследники рисуют свой уникальный контент.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `BaseCarryable` | Базовый класс переносимых предметов. |
| `ScreenMaterialName` | Виртуальное свойство — имя материала экрана на модели (`toolgun_screen`). |
| `ScreenMaterialPath` | Путь к .vmat файлу материала экрана. |
| `ScreenTextureSize` | Размер рендер-таргета (512×128 по умолчанию). |
| `ScreenRefreshInterval` | Минимальный интервал между перерисовками (0 = каждый кадр). |
| `SpinCoil()` | Добавляет энергию вращения катушки (например, при выстреле). |
| `ApplyCoilSpin()` | Каждый кадр затухает вращение (`LerpTo(0)`) и применяет к объекту `coil` на вью-модели. |
| `UpdateJoystick()` | Обновляет параметры джойстика на модели из дельты обзора игрока. |
| `SetIsUsingJoystick()` | Устанавливает булев параметр `b_joystick` на модели. |
| `UpdateViewmodelScreen()` | Находит материал экрана → создаёт рендер-таргет → подставляет копию материала с текстурой `Emissive` → перерисовывает через `CommandList`. |
| `UpdateViewScreenCommandList()` | Создаёт `CommandList`: очистка → `DrawScreenContent()` → генерация MIP-map'ов. |
| `DrawScreenContent()` | Виртуальный метод — наследники переопределяют для рисования своего контента. |

## Создай файл

**Путь:** `Code/Weapons/ScreenWeapon.cs`

```csharp
using Sandbox.Rendering;

public partial class ScreenWeapon : BaseCarryable
{
	private Material _screenMaterialCopy;
	private Texture _screenTexture;
	private float _coilSpin;
	private float _joystickX;
	private float _joystickY;
	private TimeSince _lastScreenUpdate;

	/// <summary>
	/// Override to match a different model's screen material.
	/// </summary>
	protected virtual string ScreenMaterialName => "toolgun_screen";

	/// <summary>
	/// Override to use a different screen material
	/// </summary>
	protected virtual string ScreenMaterialPath => "weapons/toolgun/toolgun-screen.vmat";

	protected virtual Vector2Int ScreenTextureSize => new( 512, 128 );

	/// <summary>
	/// Minimum time in seconds between screen redraws
	/// </summary>
	protected virtual float ScreenRefreshInterval => 0f;

	/// <summary>
	/// Add energy to the coil spin (e.g. on fire).
	/// </summary>
	public void SpinCoil()
	{
		_coilSpin += 10;
	}

	/// <summary>
	/// Smoothly decays the coil spin and applies it to the viewmodel's "coil" object.
	/// </summary>
	protected void ApplyCoilSpin()
	{
		_coilSpin = _coilSpin.LerpTo( 0, Time.Delta * 1 );

		if ( !ViewModel.IsValid() ) return;

		var coil = ViewModel.GetAllObjects( true ).FirstOrDefault( x => x.Name == "coil" );
		if ( coil.IsValid() )
		{
			coil.WorldRotation *= Rotation.From( 0, 0, _coilSpin );
		}
	}

	/// <summary>
	/// Updates joystick_x and joystick_y on the viewmodel based on a per-frame look delta.
	/// </summary>
	public void UpdateJoystick( Angles lookDelta )
	{
		_joystickX = _joystickX.LerpTo( lookDelta.yaw.Clamp( -1f, 1f ), Time.Delta * 10f );
		_joystickY = _joystickY.LerpTo( lookDelta.pitch.Clamp( -1f, 1f ), Time.Delta * 10f );

		WeaponModel?.Renderer?.Set( "joystick_x", _joystickX );
		WeaponModel?.Renderer?.Set( "joystick_y", _joystickY );
	}

	public void SetIsUsingJoystick( bool isUsing )
	{
		WeaponModel?.Renderer?.Set( "b_joystick", isUsing );
	}

	/// <summary>
	/// Updates the viewmodel screen render target and redraws it.
	/// </summary>
	protected void UpdateViewmodelScreen()
	{
		if ( ScreenRefreshInterval > 0f && _lastScreenUpdate < ScreenRefreshInterval )
			return;

		_lastScreenUpdate = 0;

		if ( !ViewModel.IsValid() ) return;

		var modelRenderer = ViewModel.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !modelRenderer.IsValid() ) return;

		var oldMaterial = modelRenderer.Model.Materials.FirstOrDefault( x => x.Name.Contains( ScreenMaterialName ) );
		var index = modelRenderer.Model.Materials.IndexOf( oldMaterial );
		if ( index < 0 ) return;

		_screenTexture ??= Texture.CreateRenderTarget().WithSize( ScreenTextureSize.x, ScreenTextureSize.y ).WithInitialColor( Color.Red ).WithMips()
			.Create();
		_screenTexture.Clear( Color.Random );

		_screenMaterialCopy ??= Material.Load( ScreenMaterialPath ).CreateCopy();
		_screenMaterialCopy.Attributes.Set( "Emissive", _screenTexture );
		modelRenderer.SceneObject.Attributes.Set( "Emissive", _screenTexture );

		modelRenderer.Materials.SetOverride( index, _screenMaterialCopy );

		UpdateViewScreenCommandList( modelRenderer );
	}

	private void UpdateViewScreenCommandList( SkinnedModelRenderer renderer )
	{
		var rt = RenderTarget.From( _screenTexture );

		var cl = new CommandList();
		renderer.ExecuteBefore = cl;

		cl.SetRenderTarget( rt );
		cl.Clear( Color.Black );

		DrawScreenContent( new Rect( 0, _screenTexture.Size ), cl.Paint );

		cl.ClearRenderTarget();
		cl.GenerateMipMaps( rt );
	}

	/// <summary>
	/// Override this to draw custom content onto the viewmodel screen.
	/// </summary>
	protected virtual void DrawScreenContent( Rect rect, HudPainter paint )
	{
	}
}
```

## Проверка

1. Создайте наследника `ScreenWeapon` и переопределите `DrawScreenContent()` — текст должен появиться на экране модели.
2. Проверьте, что катушка (`coil`) вращается при вызове `SpinCoil()` и плавно останавливается.
3. Двигайте мышь — джойстик на модели должен отклоняться в соответствии с движением.
4. Проверьте, что экран обновляется каждый кадр при `ScreenRefreshInterval = 0`.
5. Установите `ScreenRefreshInterval > 0` и убедитесь, что экран обновляется не чаще заданного интервала.
