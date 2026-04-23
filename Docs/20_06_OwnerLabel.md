# 20_06 — HUD-компонент: Метка владельца (OwnerLabel) 👤

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём **OwnerLabel** — простой HUD-компонент, который показывает текстовую метку «Owned by <имя>», когда игрок смотрит на принадлежащий кому-то объект. Метка плавно появляется в правой части экрана при наведении взгляда и плавно исчезает, когда игрок отводит взгляд. Если игрок включил «скрыть HUD» (`WantsHideHud`), метка не отрисовывается вовсе.

## Как это работает внутри движка

- `OwnerLabel` наследуется от `PanelComponent` — компонент сцены с UI-панелью. Добавляется на объект HUD.
- В `OnUpdate()` (каждый кадр) выполняется рейкаст из камеры вперёд на 2048 юнитов (`Scene.Trace.Ray`), исключая объекты с тегами `"player"` и `"trigger"`.
- Если луч попал в объект, ищется компонент `Ownable` через `GetComponentInParent<Ownable>(true)` (ищет вверх по иерархии, включая неактивные).
- Если `Ownable` найден и у него есть `Owner` (объект `Connection`), берётся `DisplayName` владельца и показывается метка вида «Owned by <имя>».
- CSS-класс `"visible"` управляет видимостью: без него — `opacity: 0`, с ним — `opacity: 1`. Переход анимируется через `transition: all 0.15s ease`.
- В самом начале `OnUpdate()` стоит ранний выход: если `Scene.Camera` отсутствует **или** у локального игрока установлен флаг `WantsHideHud`, метод снимает класс `visible` и не запускает трассу. Это позволяет плавно скрывать панель через CSS-анимацию класса `.visible`, не разрушая саму DOM-ноду.
- `BuildHash()` зависит только от `_ownerName` — UI перерисовывается лишь при смене целевого объекта.

> **Изменено в апстриме:** в актуальной версии метка стала проще — без аватара Steam и без ID. Теперь это именно текстовая подпись «Owned by <имя>», плюс реакция на скрытие HUD. Раньше отрисовывался ещё и круглый аватар по `avatar:steamid`, и `_ownerId` участвовал в хеше — этого больше нет.

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
		<label class="name">Owned by @_ownerName</label>
	</div>
</root>

@code
{
    string _ownerName;

    Player Player => Player.FindLocalPlayer();

	protected override void OnUpdate()
	{
		var camera = Scene.Camera;
        if (camera is null || (Player.IsValid() && Player.WantsHideHud) )
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
				Panel.SetClass( "visible", true );
				return;
			}
		}

		Panel.SetClass( "visible", false );
	}

	protected override int BuildHash() => System.HashCode.Combine( _ownerName );
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
	transition: all 0.15s ease;

	&.visible
	{
		opacity: 1;
	}

	.owner-label
	{
		flex-direction: row;
		align-items: center;
		padding: 10px 14px;

		.name
		{
			font-size: 11px;
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
| `if ( camera is null \|\| (Player.IsValid() && Player.WantsHideHud) )` | Ранний выход в цикле обновления: если `Scene.Camera` ещё не существует, **или** локальный игрок попросил скрыть HUD (например, держит CameraWeapon с `WantsHideHud = true`) — снимаем класс `visible` и не делаем трассу. Раньше выход по `WantsHideHud` стоял прямо в шаблоне Razor, но это убирало возможность плавно скрывать панель через CSS-классы. |
| `Player Player => Player.FindLocalPlayer();` | Свойство-обёртка: берёт ссылку на локального игрока. Используется только чтобы прочитать `WantsHideHud`. |
| `_ownerName` | Приватное поле: имя владельца. Обновляется каждый кадр. |
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
| `Panel.SetClass("visible", true/false)` | Добавляет/убирает CSS-класс на корневой панели. Управляет видимостью через CSS-анимацию. |
| `class="hud-panel"` | Общий CSS-класс HUD-панелей из темы — даёт полупрозрачный фон, скруглённые углы. |
| `BuildHash()` | `HashCode.Combine(_ownerName)` — UI перерисовывается только при смене имени владельца. Если игрок смотрит на тот же объект — перерисовки нет. |

### OwnerLabel.razor.scss

| Селектор | Описание |
|----------|----------|
| `OwnerLabel` | Абсолютное позиционирование: правый край (`right: $deadzone-x`), вертикальный центр (`top: 50%`). `pointer-events: none` — не перехватывает клики. |
| `$deadzone-x` | Переменная из Theme.scss — отступ от края экрана (безопасная зона). |
| `opacity: 0` | Начальное скрытое состояние: невидимо. |
| `transition: all 0.15s ease` | Плавный переход 150мс — создаёт мягкое появление/исчезновение метки. |
| `&.visible` | Видимое состояние: полная непрозрачность. |
| `.owner-label` | Горизонтальная раскладка с отступами вокруг текста. |
| `.name` | Имя владельца: 11px, жирный, шрифт заголовков из темы (`$title-font`), цвет HUD-текста (`$hud-text`). |

## Что проверить

1. Убедитесь, что `OwnerLabel` добавлен на HUD-объект сцены.
2. Заспавните объект (проп) — он должен получить `Ownable`-компонент с вашим ID.
3. Посмотрите на объект — в правой части экрана должна плавно появиться метка «Owned by <ваше имя>».
4. Отведите взгляд — метка должна плавно исчезнуть.
5. Посмотрите на объект другого игрока — должно показаться его имя.
6. Посмотрите на объект без владельца — метка не должна появляться.
7. Посмотрите на игрока — метка не должна появляться (тег `"player"` исключён).
8. Включите «скрыть HUD» (`WantsHideHud`) — метка должна полностью пропасть, даже если вы смотрите на свой проп.


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[21.01 — Редактор одежды (DresserEditor)](21_01_DresserEditor.md)** — начало следующей фазы.
