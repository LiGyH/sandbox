# 24.02 — Свободная камера (FreeCam) 🎥

## Что мы делаем?

Создаём **FreeCamGameObjectSystem** — систему свободной камеры (noclip-камера), позволяющую летать по уровню без привязки к персонажу. Активируется нажатием клавиши J.

## Как это работает?

`FreeCamGameObjectSystem` — это `GameObjectSystem`, который:
1. Перехватывает нажатие J для включения/отключения режима
2. В активном режиме подавляет ввод игрока (`Input.Suppressed`)
3. Управляет камерой напрямую: WASD + мышь
4. Создаёт UI-оверлей (`FreeCamOverlay`) с настройками

### Управление

| Клавиша | Действие |
|---------|----------|
| J | Вкл/выкл режим |
| WASD | Движение |
| Мышь | Поворот камеры |
| Ctrl (duck) | Медленное движение (5 ед/с) |
| Shift (run) | Быстрое движение (300 ед/с) |
| ПКМ + мышь | Изменение FOV (1-120) |

## Создай файл

Путь: `Code/FreeCam/FreeCamGameObjectSystem.cs`

```csharp
using Sandbox.Utility;
using static Sandbox.Component;

public sealed class FreeCamGameObjectSystem : GameObjectSystem<FreeCamGameObjectSystem>, ISceneStage
{
	public bool IsActive { get; set; }

	Vector3 position;
	Vector3 smoothPosition;

	Angles angles;
	Angles smoothAngles;

	float fov = 80;
	float smoothFov = 80;

	GameObject _ui;

	public FreeCamGameObjectSystem( Scene scene ) : base( scene )
	{

	}

	void ISceneStage.Start()
	{
		if ( !IsActive )
			return;

		Input.Suppressed = true;
	}

	void StartFreeCamMode()
	{
		if ( !_ui.IsValid() )
		{
			_ui = new GameObject( true, "freecam_overlay" );
			_ui.Flags = GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
			var fc = _ui.AddComponent<FreeCamOverlay>();
			var sp = _ui.AddComponent<ScreenPanel>();
		}

		if ( _ui.IsValid() )
		{
			_ui.Enabled = true;
		}

		smoothPosition = Scene.Camera.WorldPosition;
		position = smoothPosition + Scene.Camera.WorldRotation.Backward * 50;
		angles = smoothAngles = Scene.Camera.WorldRotation;
		smoothFov = fov = Scene.Camera.FieldOfView;

		Scene.Camera.RenderExcludeTags.Add( "firstperson" );
		Scene.Camera.RenderExcludeTags.Add( "ui" );
	}

	void EndFreeCamMode()
	{
		if ( _ui.IsValid() )
		{
			_ui.Enabled = false;
		}

		Scene.TimeScale = 1;
		Scene.Camera.RenderExcludeTags.Remove( "firstperson" );
		Scene.Camera.RenderExcludeTags.Remove( "ui" );
	}

	void ISceneStage.End()
	{
		if ( IsActive )
		{
			Input.Suppressed = false;
		}

		if ( Input.Keyboard.Pressed( "J" ) )
		{
			IsActive = !IsActive;

			if ( IsActive )
			{
				StartFreeCamMode();
			}
			else
			{
				EndFreeCamMode();
			}
		}

		if ( !IsActive )
			return;

		if ( _ui.IsValid() )
		{
			var fc = _ui.GetOrAddComponent<FreeCamOverlay>();
			fc.Update( Input.Down( "score" ) );

			UpdateCameraPosition( fc );
		}
	}

	void UpdateCameraPosition( FreeCamOverlay overlay )
	{
		var speed = 50;
		if ( Input.Down( "duck" ) ) speed = 5;
		if ( Input.Down( "run" ) ) speed = 300;

		if ( Input.Down( "attack2" ) )
		{
			fov += Input.MouseDelta.y * 0.1f;
			fov = fov.Clamp( 1, 120 );
		}
		else
		{
			angles += Input.AnalogLook * fov.Remap( 1, 100, 0.1f, 1 );
		}

		var velocity = angles.ToRotation() * Input.AnalogMove * speed;

		position += velocity * RealTime.SmoothDelta;

		smoothPosition = smoothPosition.LerpTo( position, MathF.Pow( RealTime.SmoothDelta * 16.0f, overlay.CameraSmoothing * 2.0f ) );
		Scene.Camera.WorldPosition = smoothPosition;

		smoothAngles = smoothAngles.LerpTo( angles, MathF.Pow( RealTime.SmoothDelta * 16.0f, overlay.CameraSmoothing * 2.0f ) );
		Scene.Camera.WorldRotation = smoothAngles + new Angles( Noise.Fbm( 2, Time.Now * 40, 1 ) * 2, Noise.Fbm( 2, Time.Now * 30, 60 ) * 2, 0 );

		smoothFov = smoothFov.LerpTo( fov, RealTime.SmoothDelta * 20.0f );
		Scene.Camera.FieldOfView = smoothFov;

		Scene.Camera.RenderExcludeTags.Remove( "viewer" );
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `ISceneStage` | Интерфейс с методами Start/End — вызывается каждый кадр |
| `Input.Suppressed = true` | Блокирует ввод для игрового персонажа |
| `smoothPosition.LerpTo` | Плавное следование камеры за позицией |
| `Noise.Fbm` | Лёгкая «тряска» камеры (кинематографический эффект) |
| `RenderExcludeTags` | Скрывает UI и firstperson-модель при свободной камере |
| `GameObjectFlags.NotSaved | NotNetworked` | UI оверлей не сохраняется и не синхронизируется |

## Плавность камеры

Камера использует `LerpTo` с экспоненциальным сглаживанием. Значение `CameraSmoothing` из оверлея определяет степень сглаживания:
- `0` — мгновенное следование
- `1` — очень плавное (кинематографическое)

---

Это последняя фаза кодовых систем! Далее — обновлённый INDEX.md.


---

## ➡️ Следующий шаг

Переходи к **[24.03 — Базовый класс страницы утилит (UtilityPage) 🧰](24_03_UtilityPage.md)**.
