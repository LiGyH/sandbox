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
