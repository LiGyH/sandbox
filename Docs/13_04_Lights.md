# Этап 13_04 — Lights (Управляемые источники света)

## Что это такое?

`PointLightEntity` и `SpotLightEntity` — это компоненты, превращающие объект в управляемый источник света. Представьте настольную лампу с выключателем: вы можете её включить, выключить, изменить яркость и цвет. Эти компоненты работают так же — игрок может управлять светом прямо в игре.

**Два типа:**
- 🔵 **PointLight** — точечный свет, как лампочка: светит во все стороны
- 🔦 **SpotLight** — прожектор: светит конусом в одном направлении

## Как это работает?

```
PointLightEntity / SpotLightEntity
├── On           → включён/выключен
├── Shadows      → отбрасывать ли тени
├── Color        → цвет света
├── Brightness   → яркость (0–50)
├── Radius       → радиус (дальность) света
├── Attenuation  → затухание (мягкость краёв)
├── Angle        → (только SpotLight) угол конуса
│
├── TurnOn       → клавиша «включить»
├── TurnOff      → клавиша «выключить»
├── Toggle       → клавиша «переключить»
│
├── OnGameObject  → объект, видимый когда свет ВКЛ
├── OffGameObject → объект, видимый когда свет ВЫКЛ
│
└── UpdateLight() → применяет все настройки к реальному компоненту света
```

## Исходный код

### PointLightEntity (Точечный свет)

📁 `Code/Game/Entity/PointLightEntity.cs`

```csharp
public class PointLightEntity : Component, IPlayerControllable
{
	[Property, ClientEditable, Group( "Light" )]
	public bool On { get; set { field = value; UpdateLight(); } } = true;

	[Property, ClientEditable, Group( "Light" )]
	public bool Shadows { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 1 ), ClientEditable, Group( "Light" )]
	public Color Color { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 50 ), ClientEditable, Group( "Light" )]
	public float Brightness { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 1000 ), ClientEditable, Group( "Light" )]
	public float Radius { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 16 ), ClientEditable, Group( "Light" )]
	public float Attenuation { get; set { field = value; UpdateLight(); } } = 2.4f;


	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput TurnOn { get; set; }

	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput TurnOff { get; set; }

	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput Toggle { get; set; }

	[Property]
	public GameObject OnGameObject { get; set; }

	[Property]
	public GameObject OffGameObject { get; set; }

	void IPlayerControllable.OnControl()
	{

		if ( Toggle.Pressed() )
		{
			On = !On;
		}

		if ( TurnOn.Pressed() )
		{
			On = true;
		}

		if ( TurnOff.Pressed() )
		{
			On = false;
		}
	}

	void IPlayerControllable.OnEndControl()
	{

	}

	void IPlayerControllable.OnStartControl()
	{

	}

	void UpdateLight()
	{
		OnGameObject?.Enabled = On;
		OffGameObject?.Enabled = !On;

		if ( GetComponentInChildren<PointLight>( true ) is not PointLight light )
			return;

		light.Enabled = On;

		var color = Color;
		color.r *= Brightness;
		color.g *= Brightness;
		color.b *= Brightness;

		light.Shadows = Shadows;
		light.LightColor = color;
		light.Radius = Radius;
		light.Attenuation = Attenuation;

		Network.Refresh();
	}
}
```

### SpotLightEntity (Прожектор)

📁 `Code/Game/Entity/SpotLightEntity.cs`

```csharp
public class SpotLightEntity : Component, IPlayerControllable
{
	[Property, ClientEditable, Group( "Light" )]
	public bool On { get; set { field = value; UpdateLight(); } } = true;

	[Property, ClientEditable, Group( "Light" )]
	public bool Shadows { get; set { field = value; UpdateLight(); } } = true;

	[Property, Range( 0, 1 ), ClientEditable, Group( "Light" )]
	public Color Color { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 50 ), ClientEditable, Group( "Light" )]
	public float Brightness { get; set { field = value; UpdateLight(); } } = 2;

	[Property, Range( 0, 1000 ), ClientEditable, Group( "Light" )]
	public float Radius { get; set { field = value; UpdateLight(); } } = 500;

	[Property, Range( 0, 90 ), ClientEditable, Group( "Light" )]
	public float Angle { get; set { field = value; UpdateLight(); } } = 35;

	[Property, Range( 0, 16 ), ClientEditable, Group( "Light" )]
	public float Attenuation { get; set { field = value; UpdateLight(); } } = 2.4f;


	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput TurnOn { get; set; }

	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput TurnOff { get; set; }

	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput Toggle { get; set; }

	[Property]
	public GameObject OnGameObject { get; set; }

	[Property]
	public GameObject OffGameObject { get; set; }

	void IPlayerControllable.OnControl()
	{

		if ( Toggle.Pressed() )
		{
			On = !On;
		}

		if ( TurnOn.Pressed() )
		{
			On = true;
		}

		if ( TurnOff.Pressed() )
		{
			On = false;
		}
	}

	void IPlayerControllable.OnEndControl()
	{

	}

	void IPlayerControllable.OnStartControl()
	{

	}

	void UpdateLight()
	{
		OnGameObject?.Enabled = On;
		OffGameObject?.Enabled = !On;

		if ( GetComponentInChildren<SpotLight>( true ) is not SpotLight light )
			return;

		light.Enabled = On;

		var color = Color;
		color.r *= Brightness;
		color.g *= Brightness;
		color.b *= Brightness;

		light.Shadows = Shadows;
		light.LightColor = color;
		light.Radius = Radius;
		light.Attenuation = Attenuation;
		light.ConeOuter = Angle;
		light.ConeInner = Angle * 0.5f;

		Network.Refresh();
	}
}
```

## Разбор кода

### Общая структура

Оба класса почти идентичны — наследуют `Component` и реализуют `IPlayerControllable`. Разница только в типе источника света и дополнительном свойстве `Angle` у прожектора.

### Реактивные свойства (сеттеры с автообновлением)

```csharp
public bool On { get; set { field = value; UpdateLight(); } } = true;
```

Это ключевая особенность: каждое свойство при изменении автоматически вызывает `UpdateLight()`. Конструкция `set { field = value; UpdateLight(); }` — это кастомный сеттер:
1. `field = value` — записывает новое значение в backing field.
2. `UpdateLight()` — сразу применяет изменение к реальному свету.

Это значит, что при изменении **любого** параметра (цвет, яркость, радиус) свет мгновенно обновляется — без ожидания следующего кадра.

### Параметры света

| Свойство | Диапазон | Описание |
|----------|----------|----------|
| `On` | true/false | Включён ли свет |
| `Shadows` | true/false | Отбрасывает ли тени |
| `Color` | 0–1 (RGB) | Цвет света |
| `Brightness` | 0–50 | Яркость (множитель цвета) |
| `Radius` | 0–1000 | Дальность освещения |
| `Attenuation` | 0–16 | Затухание (чем больше — тем резче граница) |
| `Angle` | 0–90° | *(Только SpotLight)* Угол конуса прожектора |

### Ввод игрока

```csharp
[Property, Sync, ClientEditable, Group( "State" )]
public ClientInput TurnOn { get; set; }

[Property, Sync, ClientEditable, Group( "State" )]
public ClientInput TurnOff { get; set; }

[Property, Sync, ClientEditable, Group( "State" )]
public ClientInput Toggle { get; set; }
```

Три независимых действия — каждое можно назначить на свою клавишу:
- **Toggle** — переключает (вкл → выкл → вкл...)
- **TurnOn** — только включает
- **TurnOff** — только выключает

Зачем отдельные `TurnOn`/`TurnOff`? Для сложных механизмов: например, одна кнопка включает все лампы, другая — выключает.

### Визуальные объекты состояния

```csharp
[Property]
public GameObject OnGameObject { get; set; }

[Property]
public GameObject OffGameObject { get; set; }
```

Два необязательных объекта, которые отображаются в зависимости от состояния:
- `OnGameObject` — виден, когда свет включён (например, светящийся корпус лампы).
- `OffGameObject` — виден, когда свет выключен (например, тёмный корпус).

### Метод `UpdateLight()` — применение настроек

```csharp
void UpdateLight()
{
    OnGameObject?.Enabled = On;
    OffGameObject?.Enabled = !On;
```

Сначала переключает визуальные объекты состояния.

```csharp
    if ( GetComponentInChildren<PointLight>( true ) is not PointLight light )
        return;
```

Ищет реальный компонент `PointLight` (или `SpotLight`) в дочерних объектах. Параметр `true` означает «искать даже в выключенных объектах». Если не найден — выходим.

```csharp
    light.Enabled = On;

    var color = Color;
    color.r *= Brightness;
    color.g *= Brightness;
    color.b *= Brightness;
```

Включает/выключает свет и вычисляет финальный цвет: базовый цвет умножается на яркость. Например, белый свет `(1, 1, 1)` с `Brightness = 5` станет `(5, 5, 5)` — ярче стандартного.

```csharp
    light.Shadows = Shadows;
    light.LightColor = color;
    light.Radius = Radius;
    light.Attenuation = Attenuation;
```

Применяет все параметры к движковому компоненту.

Для `SpotLightEntity` добавляются параметры конуса:
```csharp
    light.ConeOuter = Angle;
    light.ConeInner = Angle * 0.5f;
```

- `ConeOuter` — внешний угол конуса (полная ширина луча).
- `ConeInner` — внутренний угол (зона максимальной яркости) = половина внешнего. Это создаёт мягкий переход от яркого центра к краям луча.

```csharp
    Network.Refresh();
}
```

`Network.Refresh()` отправляет обновлённое состояние всем клиентам в сети — свет меняется одинаково для всех игроков.

### Отличия PointLight от SpotLight

| Аспект | PointLightEntity | SpotLightEntity |
|--------|-----------------|-----------------|
| Тип света | `PointLight` | `SpotLight` |
| Направление | Во все стороны (сфера) | Конусом в одну сторону |
| Тени по умолчанию | Выключены | Включены |
| Яркость по умолчанию | Не задана | 2 |
| Радиус по умолчанию | Не задан | 500 |
| Угол конуса | — | 35° |
| Свойство `Angle` | Нет | Есть (0–90°) |
