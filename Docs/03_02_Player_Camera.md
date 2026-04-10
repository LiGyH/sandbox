# 03.02 — Камера игрока (Player.Camera) 🎥

## Что мы делаем?

Настраиваем **камеру игрока** — как она следит за персонажем, как реагирует на движение (view bobbing), и как работает в режиме «сидения в транспорте» (seated camera).

Это `partial class Player` — продолжение основного файла `Player.cs`.

## Как это работает?

### Два режима камеры

1. **От первого лица** — камера привязана к голове, добавляется лёгкий наклон при стрейфе (view bobbing)
2. **Seated (транспорт)** — камера вращается вокруг транспорта на расстоянии, рассчитанном по размеру конструкции

### View Bobbing (наклон при движении)

```csharp
var r = Controller.WishVelocity.Dot( EyeTransform.Left ) / -250.0f;
roll = MathX.Lerp( roll, r, Time.Delta * 10.0f, true );
camera.WorldRotation *= new Angles( 0, 0, roll );
```

- `Dot(Left)` — проекция скорости на «лево»: если идём влево, roll > 0 (наклон вправо)
- Делим на -250 — маленький угол, чтобы не тошнило
- `Lerp` с `Time.Delta * 10` — плавное сглаживание
- Отключается в третьем лице или если игрок выключил эту опцию

### Seated Camera (транспорт)

Когда игрок сидит в чём-то (`ISitTarget`):

1. **Рассчитываем размер конструкции** — `RebuildContraptionBounds()` собирает все связанные объекты через `LinkedGameObjectBuilder` и считает BBox
2. **Камера на расстоянии** — `_minCameraDistance` = max(200, размер конструкции)
3. **Скорость влияет на дистанцию** — чем быстрее едем, тем дальше камера
4. **Трейс от центра до камеры** — не даём камере пройти сквозь стены

### FOV (поле зрения)

```csharp
camera.FieldOfView = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );
```

Конвертирует пользовательскую настройку FOV в вертикальный угол для соотношения 16:9. Это стандартный подход — горизонтальный FOV меняется в зависимости от монитора, а вертикальный остаётся стабильным.

## Создай файл

Путь: `Code/Player/Player.Camera.cs`

```csharp
using Sandbox.Movement;

public sealed partial class Player
{
	[Property, Group( "Camera" )] public float SeatedCameraDistance { get; set; } = 200f;
	[Property, Group( "Camera" )] public float SeatedCameraHeight { get; set; } = 40f;
	[Property, Group( "Camera" )] public float SeatedCameraPositionSpeed { get; set; } = 3f;
	[Property, Group( "Camera" )] public float SeatedCameraVelocityScale { get; set; } = 0.1f;

	private ISitTarget _cachedSeat;
	private float _minCameraDistance;
	private float _smoothedDistance;
	private Angles _seatedAngles;
	private Vector3 _lastSeatWorldPos;

	private float roll;

	void PlayerController.IEvents.OnEyeAngles( ref Angles ang )
	{
		var angles = ang;
		IPlayerEvent.Post( x => x.OnCameraMove( ref angles ) );
		ang = angles;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent camera )
	{
		camera.FovAxis = CameraComponent.Axis.Vertical;
		camera.FieldOfView = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );

		IPlayerEvent.Post( x => x.OnCameraSetup( camera ) );

		ApplyMovementCameraEffects( camera );
		ApplySeatedCameraSetup( camera );

		IPlayerEvent.Post( x => x.OnCameraPostSetup( camera ) );
	}

	private void ApplyMovementCameraEffects( CameraComponent camera )
	{
		if ( Controller.ThirdPerson ) return;
		if ( !GamePreferences.ViewBobbing ) return;

		var r = Controller.WishVelocity.Dot( EyeTransform.Left ) / -250.0f;
		roll = MathX.Lerp( roll, r, Time.Delta * 10.0f, true );

		camera.WorldRotation *= new Angles( 0, 0, roll );
	}

	private void ApplySeatedCameraSetup( CameraComponent camera )
	{
		if ( !Controller.ThirdPerson )
		{
			_cachedSeat = null;
			return;
		}

		var seat = GetComponentInParent<ISitTarget>( false );
		if ( seat is null )
		{
			_cachedSeat = null;
			return;
		}

		var seatGo = (seat as Component).GameObject;
		var seatPos = seatGo.WorldPosition + Vector3.Up * SeatedCameraHeight;

		if ( seat != _cachedSeat )
		{
			_cachedSeat = seat;
			_minCameraDistance = MathF.Max( SeatedCameraDistance, RebuildContraptionBounds( seatGo ) );
			_seatedAngles = camera.WorldRotation.Angles();
			_lastSeatWorldPos = seatPos;
			_smoothedDistance = _minCameraDistance;
		}

		_seatedAngles.yaw += Input.AnalogLook.yaw;
		_seatedAngles.pitch = (_seatedAngles.pitch + Input.AnalogLook.pitch).Clamp( -89, 89 );

		// Derive velocity from position delta and add it to the target distance
		var speed = (seatPos - _lastSeatWorldPos).Length / Time.Delta;
		_lastSeatWorldPos = seatPos;
		var targetDistance = _minCameraDistance + speed * SeatedCameraVelocityScale;

		// Smooth orbit distance
		_smoothedDistance = _smoothedDistance.LerpTo( targetDistance, Time.Delta * SeatedCameraPositionSpeed );

		// Compose rotation: yaw around world up, then pitch around local right, no gimbal lock
		var camRot = Rotation.FromYaw( _seatedAngles.yaw ) * Rotation.FromPitch( _seatedAngles.pitch );
		var desiredPos = seatPos + camRot.Backward * _smoothedDistance;

		// Trace from pivot to desired camera position; stop at walls so the camera doesn't clip through geometry
		var tr = Scene.Trace.FromTo( seatPos, desiredPos ).Radius( 8f ).WithoutTags( "player", "ragdoll", "effect" ).IgnoreGameObjectHierarchy( GameObject.Root ).Run();
		var camPos = tr.Hit ? tr.HitPosition + (seatPos - desiredPos).Normal * 4f : desiredPos;

		camera.WorldPosition = camPos;
		camera.WorldRotation = Rotation.LookAt( seatPos - camPos, Vector3.Up );
	}

	private float RebuildContraptionBounds( GameObject seatGo )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( seatGo );

		var totalBounds = new BBox();
		var initialized = false;
		foreach ( var obj in builder.Objects )
		{
			if ( obj.Tags.Has( "player" ) ) continue;
			var b = obj.GetBounds();
			totalBounds = initialized ? totalBounds.AddBBox( b ) : b;
			initialized = true;
		}

		return totalBounds.Size.Length;
	}
}
```

## Ключевые концепции для новичков

### Что такое `partial class`?

В C# можно разбить один класс на несколько файлов с помощью ключевого слова `partial`. Компилятор соберёт их в единый класс. Это удобно, когда класс большой — разделяем по ответственности.

### Зачем `[Property, Group("Camera")]`?

`[Property]` — показывает поле в инспекторе s&box Editor. `Group("Camera")` — группирует поля в свёрнутую секцию, чтобы инспектор не был загромождён.

### Трейс (Scene.Trace)

Трейс — это луч из точки A в точку B. Если луч сталкивается с геометрией, мы получаем точку столкновения. Используется для предотвращения прохождения камеры сквозь стены.

## Проверка

1. Зайди в игру → двигайся влево-вправо → камера слегка наклоняется
2. Выключи View Bobbing в настройках → наклон пропадает
3. Сядь в транспорт → камера переключается на орбитальный режим

## Следующий файл

Переходи к **03.03 — Патроны игрока (Player.Ammo)**.
