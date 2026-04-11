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
