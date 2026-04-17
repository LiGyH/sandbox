# 20_06 — HUD-компонент: Метка владельца (OwnerLabel) 👤

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём **OwnerLabel** — HUD-компонент, который показывает имя и аватар владельца объекта, когда игрок смотрит на него. Это небольшая плавающая метка в правой части экрана: аватар Steam + имя игрока. Появляется с анимацией при наведении взгляда и исчезает, когда игрок отводит взгляд.

## Как это работает внутри движка

- `OwnerLabel` наследуется от `PanelComponent` — компонент сцены с UI-панелью. Добавляется на объект HUD.
- В `OnUpdate()` (каждый кадр) выполняется рейкаст из камеры вперёд на 2048 юнитов (`Scene.Trace.Ray`), исключая объекты с тегами `"player"` и `"trigger"`.
- Если луч попал в объект, ищется компонент `Ownable` через `GetComponentInParent<Ownable>(true)` (ищет вверх по иерархии, включая неактивные).
- Если `Ownable` найден и у него есть `Owner` (объект `Connection`), берётся `DisplayName` и `SteamId` владельца.
- CSS-класс `"visible"` управляет видимостью: без него — `opacity: 0` и сдвиг вправо, с ним — `opacity: 1` и нормальная позиция. Переход анимируется через `transition: all 0.15s ease`.
- Аватар загружается через протокол `avatar:steamid` — движок автоматически загружает аватар Steam по ID.
- `BuildHash()` возвращает хеш от `_ownerName` и `_ownerId` — перерисовка UI происходит только при смене целевого объекта.

## Путь к файлам

```
Code/UI/OwnerLabel/OwnerLabel.razor
Code/UI/OwnerLabel/OwnerLabel.razor.scss
```

## Полный код

### `OwnerLabel.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

<root>
	<div class="owner-label hud-panel">
		<div class="avatar" style="background-image: url( avatar:@_ownerId )"></div>
		<label class="name">@_ownerName</label>
	</div>
</root>

@code
{
	string _ownerName;
	ulong _ownerId;

	protected override void OnUpdate()
	{
		var camera = Scene.Camera;
		if ( camera is null )
		{
			Panel.SetClass( "visible", false );
			return;
		}

		var tr = Scene.Trace
			.Ray( camera.Transform.World.ForwardRay, 2048 )
			.WithoutTags( "player", "trigger" )
			.Run();

		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			var ownable = tr.GameObject.GetComponentInParent<Ownable>( true );
			if ( ownable.IsValid() && ownable.Owner is not null )
			{
				_ownerName = ownable.Owner.DisplayName;
				_ownerId = ownable.Owner.SteamId;
				Panel.SetClass( "visible", true );
				return;
			}
		}

		Panel.SetClass( "visible", false );
	}

	protected override int BuildHash() => System.HashCode.Combine( _ownerName, _ownerId );
}
```

### `OwnerLabel.razor.scss`

```scss
@import "/UI/Theme.scss";

OwnerLabel
{
	position: absolute;
	right: $deadzone-x;
	top: 50%;
	pointer-events: none;
	opacity: 0;
	transform: translate(32px) scale(0.9);
	transition: all 0.15s ease;

	&.visible
	{
		opacity: 1;
		transform: translate(0) scale(1);
	}

	.owner-label
	{
		flex-direction: row;
		align-items: center;
		gap: 10px;
		padding: 10px 14px;

		.avatar
		{
			width: 20px;
			height: 20px;
			border-radius: 50%;
			background-size: cover;
			background-repeat: no-repeat;
			background-position: center;
			flex-shrink: 0;
		}

		.name
		{
			font-size: 14px;
			font-weight: 600;
			font-family: $title-font;
			color: $hud-text;
		}
	}
}
```

## Разбор кода

### OwnerLabel.razor

| Элемент | Описание |
|---------|----------|
| `@inherits PanelComponent` | Компонент сцены с привязанной UI-панелью. Размещается на HUD-объекте. |
| `_ownerName` / `_ownerId` | Приватные поля: имя владельца и его Steam ID. Обновляются каждый кадр. |
| `OnUpdate()` | Вызывается каждый кадр. Основная логика — рейкаст и проверка владельца. |
| `Scene.Camera` | Ссылка на активную камеру сцены. Если камеры нет — скрываем метку. |
| `Scene.Trace.Ray(...)` | Рейкаст — лучевая трассировка. `camera.Transform.World.ForwardRay` — луч из позиции камеры в направлении взгляда. `2048` — максимальная дистанция в юнитах (примерно 50 метров). |
| `.WithoutTags("player", "trigger")` | Исключает из трассировки объекты с тегами «player» (игроки) и «trigger» (триггер-зоны). Иначе метка мерцала бы при взгляде на других игроков или невидимые триггеры. |
| `.Run()` | Выполняет трассировку и возвращает результат `SceneTraceResult`. |
| `tr.Hit` | `true`, если луч попал в объект. |
| `tr.GameObject.IsValid()` | Проверяет, что объект-попадание существует и не удалён. |
| `GetComponentInParent<Ownable>(true)` | Ищет компонент `Ownable` на объекте или его родителях. `true` — включая неактивные компоненты. Важно, потому что `Ownable` может быть на корневом объекте конструкции, а луч попадает в дочерний проп. |
| `ownable.IsValid()` | Дополнительная проверка валидности компонента. |
| `ownable.Owner` | Свойство типа `Connection` — ссылка на подключение игрока-владельца. |
| `DisplayName` | Отображаемое имя игрока (Steam-никнейм). |
| `SteamId` | 64-битный идентификатор Steam. Используется для загрузки аватара. |
| `Panel.SetClass("visible", true/false)` | Добавляет/убирает CSS-класс на корневой панели. Управляет видимостью через CSS-анимацию. |
| `avatar:@_ownerId` | Протокол движка для загрузки аватара Steam. Движок кеширует аватары. |
| `class="hud-panel"` | Общий CSS-класс HUD-панелей из темы — даёт полупрозрачный фон, скруглённые углы. |
| `BuildHash()` | `HashCode.Combine(_ownerName, _ownerId)` — UI перерисовывается только при смене владельца (имя или ID). Если игрок смотрит на тот же объект — перерисовки нет. |

### OwnerLabel.razor.scss

| Селектор | Описание |
|----------|----------|
| `OwnerLabel` | Абсолютное позиционирование: правый край (`right: $deadzone-x`), вертикальный центр (`top: 50%`). `pointer-events: none` — не перехватывает клики. |
| `$deadzone-x` | Переменная из Theme.scss — отступ от края экрана (безопасная зона). |
| `opacity: 0` + `transform: translate(32px) scale(0.9)` | Начальное скрытое состояние: невидимо и сдвинуто вправо на 32px с небольшим уменьшением. |
| `transition: all 0.15s ease` | Плавный переход 150мс — создаёт эффект «выезжания» метки при появлении. |
| `&.visible` | Видимое состояние: полная непрозрачность, нормальная позиция и размер. |
| `.owner-label` | Горизонтальная раскладка: аватар + имя, с отступами. |
| `.avatar` | Круглый аватар 20×20px. `border-radius: 50%` делает его круглым. `background-size: cover` заполняет всю площадь. |
| `.name` | Имя игрока: 14px, жирный, шрифт заголовков из темы (`$title-font`), цвет HUD-текста (`$hud-text`). |

## Что проверить

1. Убедитесь, что `OwnerLabel` добавлен на HUD-объект сцены.
2. Заспавните объект (проп) — он должен получить `Ownable`-компонент с вашим ID.
3. Посмотрите на объект — в правой части экрана должна плавно появиться метка с вашим аватаром и именем.
4. Отведите взгляд — метка должна плавно исчезнуть.
5. Посмотрите на объект другого игрока — должно показаться его имя и аватар.
6. Посмотрите на объект без владельца — метка не должна появляться.
7. Посмотрите на игрока — метка не должна появляться (тег `"player"` исключён).


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[21.01 — Редактор одежды (DresserEditor)](21_01_DresserEditor.md)** — начало следующей фазы.
