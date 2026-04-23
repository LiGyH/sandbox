# 13.07 — TVEntity (Телевизор) 📺

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.12 — Component](00_12_Component.md)
> - [00.13 — Атрибуты компонентов](00_13_Component_Attributes.md)
> - [26.06 — Звук и материалы](26_06_Sound.md)

## Что мы делаем?

Создаём `TVEntity` — компонент-телевизор, который **показывает на экране картинку с прилинкованной фотокамеры** ([CameraWeapon](07_09_CameraScreen.md)). Связь устанавливается через [Linker](11_06_Linker.md) (или вручную через `ManualLink`): игрок наводит линкер на TV → потом на CameraWeapon → готово.

ТВ — это полноценный sent-энтити: префаб `Assets/entities/sents/tv/tv.prefab`, описание `tv.sent`, материал экрана `materials/tv_crt_screen.vmat` со специальным шейдером, имитирующим CRT-экран.

## Зачем это нужно?

- **RT-камеры в руках игроков**: связав TV с камерой, можно построить «камеры наблюдения», транслировать игровой POV на экран в комнате и т.п.
- **Плавный fade при потере сигнала**: если камера выключена / удалена, TV не моргает чёрным, а плавно гаснет за `TransitionDuration = 0.4 с`.
- **Distance culling**: вместо постоянного рендера RT-камеры мы её отключаем, когда TV слишком далеко от наблюдателя — `Scene.Camera.WorldPosition`. Это критично для производительности — каждая активная RT-камера рисует сцену второй раз.
- **«Снежок» / шум при отсутствии сигнала**: шейдер экрана (`tv_crt_screen.shader`) сам рисует характерные CRT-помехи, мы лишь передаём ему `HasSignal = 0`.

## Как это работает внутри движка?

| Поле / метод | Описание |
|---|---|
| `[Property] ScreenMaterialName` | Подстрока имени материала на модели, который мы будем заменять (по умолчанию `"screen"`). |
| `[Property, ClientEditable, Group("Screen")] Brightness` | Множитель яркости передаётся в шейдер. Диапазон 0.5…10. |
| `[Property, ClientEditable, Group("Screen")] On` | Виртуальная «кнопка питания». Когда `false` — TV сразу теряет сигнал и переходит в transition. |
| `MaxRenderDistance = 1024` | За этой дистанцией RT-камера полностью выключается. Fade начинается с `FadeStartFraction = 0.75` от этой дистанции. |
| `HasLinkedCamera` (геттер) | Удобный публичный флаг — есть ли валидная привязка к работающей `CameraWeapon`. |
| `OnStart()` | Получает `ModelRenderer` из дочерних объектов и **выключает batching** (`SceneObject.Batchable = false`) — каждый TV должен иметь собственные attribute'ы (Color/Brightness/...). |
| `OnUpdate()` | Главный цикл: ищет привязанную камеру, считает distance fade, включает/выключает RT-камеру, обновляет атрибуты материала экрана. |
| `OnDestroy()` | Сбрасывает ссылки на материал и текстуру (саму текстуру не диспозит — она принадлежит CameraWeapon). |
| `FindLinkedTexture()` | Каждый кадр обходит `ManualLink`-компоненты на TV: если на конце линка лежит `CameraWeapon` с валидным `RenderTexture` — берём её как источник. |
| `EnsureMaterialSetup()` | Лениво создаёт **копию материала** через `Material.FromShader(ShaderPath)` и подменяет нужный слот в `_renderer.Materials`. Делается один раз при первом обновлении. |

### Поток данных

```
CameraWeapon._renderTexture  ──ManualLink──►  TVEntity._linkedTexture
        ▲                                              │
        │                                              ▼
   _rtCamera                                    ModelRenderer.Attributes
   (CameraComponent с                                  │
    RenderTarget = _renderTexture)                     ▼
                                          tv_crt_screen.shader
                                              ("Color", "HasSignal",
                                               "ScreenOn", "Brightness",
                                               "TimeSinceSignalChange",
                                               "DistanceFade")
```

### Логика fade и culling

```csharp
float distanceToCamera = Vector3.DistanceBetween( WorldPosition, Scene.Camera.WorldPosition );
float fadeStart = MaxRenderDistance * FadeStartFraction;   // 1024 * 0.75 = 768
float distanceFade = 1.0f - MathX.Clamp( ( distanceToCamera - fadeStart ) / ( MaxRenderDistance - fadeStart ), 0f, 1f );
bool tooFar = distanceFade <= 0f;

// Включаем/выключаем сам RT-camera, а не только её рендер на экран.
if ( _linkedWeapon is not null )
{
    var camera = _linkedWeapon.GetComponentInChildren<CameraComponent>( true );
    camera?.Enabled = !tooFar;
}
```

### Удержание последнего кадра во время transition

При потере сигнала шейдер должен корректно «затухнуть», а не моргнуть чёрным — поэтому мы держим `_lastTexture`, пока активен переход, **но только если CameraWeapon ещё существует**. Если камеру удалили (а вместе с ней её render target диспозен) — кэш сбрасываем, иначе попытаемся передать в шейдер уничтоженную `Texture` и упадём.

```csharp
if ( _linkedTexture is not null )
{
    _lastTexture = _linkedTexture;
}
else if ( _linkedWeapon is null || !_linkedWeapon.Enabled || _linkedWeapon.RenderTexture is null )
{
    _lastTexture = null;
}
```

## Создай файл

📄 `Code/Game/Entity/TVEntity.cs`

```csharp
/// <summary>
/// A TV screen entity that displays the feed from a linked <see cref="CameraEntity"/>.
/// Use the Linker tool to connect a Camera to this TV.
/// </summary>
public class TVEntity : Component
{
	[Property]
	public string ScreenMaterialName { get; set; } = "screen";

	[Property, Range( 0.5f, 10 ), Step( 0.5f ), ClientEditable, Group( "Screen" )]
	public float Brightness { get; set; } = 1f;

	[Property, ClientEditable, Group( "Screen" )]
	public bool On { get; set; } = true;

	public float MaxRenderDistance { get; set; } = 1024f;

	public bool HasLinkedCamera => _linkedWeapon is not null && _linkedWeapon.Enabled && _linkedWeapon.RenderTexture is not null;

	private Texture _linkedTexture;
	private Texture _lastTexture;
	private CameraWeapon _linkedWeapon;
	private Material _materialCopy;
	private ModelRenderer _renderer;
	private bool _hasSignal;
	private RealTimeSince _timeSinceSignalChange;

	private static readonly float TransitionDuration = 0.4f;
	private static readonly float FadeStartFraction = 0.75f;

	protected override void OnStart()
	{
		_renderer = GetComponentInChildren<ModelRenderer>( true );
		_renderer?.SceneObject.Batchable = false;
	}

	protected override void OnUpdate()
	{
		FindLinkedTexture();

		float distanceToCamera = Vector3.DistanceBetween( WorldPosition, Scene.Camera.WorldPosition );
		float fadeStart = MaxRenderDistance * FadeStartFraction;
		float distanceFade = 1.0f - MathX.Clamp( ( distanceToCamera - fadeStart ) / ( MaxRenderDistance - fadeStart ), 0f, 1f );
		bool tooFar = distanceFade <= 0f;

		if ( _linkedWeapon is not null )
		{
			var camera = _linkedWeapon.GetComponentInChildren<CameraComponent>( true );
			camera?.Enabled = !tooFar;
		}

		var newSignal = On && _linkedTexture is not null && !tooFar;

		if ( newSignal != _hasSignal )
		{
			_timeSinceSignalChange = 0;
			_hasSignal = newSignal;
		}

		if ( _linkedTexture is not null )
			_lastTexture = _linkedTexture;
		else if ( _linkedWeapon is null || !_linkedWeapon.Enabled || _linkedWeapon.RenderTexture is null )
			_lastTexture = null;

		EnsureMaterialSetup();

		if ( _materialCopy is null || _renderer is null ) return;

		var inTransition = _timeSinceSignalChange < TransitionDuration;
		var textureToUse = _linkedTexture ?? ( inTransition ? _lastTexture : null );

		_renderer.Attributes.Set( "Color", textureToUse is not null ? textureToUse : Texture.Black );

		if ( !_hasSignal && !inTransition )
			_lastTexture = null;

		_renderer.Attributes.Set( "HasSignal", _hasSignal ? 1.0f : 0.0f );
		_renderer.Attributes.Set( "ScreenOn", On ? 1.0f : 0.0f );
		_renderer.Attributes.Set( "TimeSinceSignalChange", (float)_timeSinceSignalChange );
		_renderer.Attributes.Set( "DistanceFade", distanceFade );
		_renderer.Attributes.Set( "Brightness", Brightness );
	}

	protected override void OnDestroy()
	{
		_materialCopy = null;
		_linkedTexture = null;
		base.OnDestroy();
	}

	private void FindLinkedTexture()
	{
		_linkedTexture = null;
		_linkedWeapon = null;

		foreach ( var link in GameObject.GetComponentsInChildren<ManualLink>() )
		{
			var target = link.Body?.Root;
			if ( target is null ) continue;

			if ( target.GetComponentInChildren<CameraWeapon>() is CameraWeapon weapon
				&& weapon.RenderTexture is not null )
			{
				_linkedTexture = weapon.RenderTexture;
				_linkedWeapon = weapon;
				return;
			}
		}
	}

	private static readonly string ShaderPath = "entities/sents/tv/materials/tv_crt_screen.shader";

	private void EnsureMaterialSetup()
	{
		if ( _materialCopy is not null && _renderer.IsValid() ) return;
		if ( _renderer is null ) return;

		var materials = _renderer.Model?.Materials;
		if ( materials is not { } mats ) return;

		for ( int i = 0; i < mats.Length; i++ )
		{
			if ( mats[i]?.Name?.Contains( ScreenMaterialName ) is true )
			{
				_materialCopy = Material.FromShader( ShaderPath );
				_renderer.Materials.SetOverride( i, _materialCopy );
				return;
			}
		}
	}
}
```

## Связанные ассеты

| Файл | Назначение |
|---|---|
| `Assets/entities/sents/tv/tv.prefab` | Сам префаб ТВ (модель + `TVEntity`). |
| `Assets/entities/sents/tv/tv.sent` | Дескриптор сущности для SpawnMenu (раздел Entities). |
| `Assets/entities/sents/tv/materials/tv_crt_screen.shader` / `.vmat` | Шейдер CRT-экрана. Использует атрибуты `Color`, `HasSignal`, `ScreenOn`, `TimeSinceSignalChange`, `DistanceFade`, `Brightness`. |

## Что проверить?

1. Заспавни TV из `Spawnmenu → Entities`.
2. Возьми CameraWeapon, заспавни его рядом, выйди из камеры (брось предмет).
3. Возьми Linker ([11.06](11_06_Linker.md)) → ЛКМ по TV → ЛКМ по CameraWeapon. Появится `ManualLink`.
4. На экране TV должна появиться картинка с камеры (FOV 40° в режиме «без хозяина»).
5. Возьми CameraWeapon в руки — TV покажет твой POV. Бросаешь обратно — снова статичный план сцены.
6. Отойди от TV дальше 1024 юнитов — изображение плавно гаснет, RT-камера выключается (проверь в `Stats` / профайлере, что нагрузка падает).
7. Поверни ползунок `Brightness` в инспекторе — экран становится ярче/тусклее.
8. Сбрось `On = false` — TV выключается с плавным fade.
9. Удали камеру — TV корректно затухает в чёрный, без падений.

---

## 🔗 См. также

- [07.09 — CameraWeapon](07_09_CameraScreen.md) — источник видеосигнала
- [11.06 — Linker / ManualLink](11_06_Linker.md) — как устанавливается связь
- [12.03 — ManualLink (метаданные)](12_03_ManualLink.md)

## ➡️ Следующий шаг

Переходи к **[14.01 — ISpawner](14_01_ISpawner.md)** — начало фазы меню спавна.
