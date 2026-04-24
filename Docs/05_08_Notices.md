# Этап 05_08 — Notices (Уведомления и подсказки)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что это такое?

Представьте всплывающие уведомления на телефоне: они появляются сверху или снизу экрана, показывают сообщение и через несколько секунд исчезают. **Notices** — это система уведомлений в игре. Она показывает:

- 💡 Подсказки для новичков («Нажмите Q, чтобы открыть меню спавна»)
- ℹ️ Системные сообщения
- ⚠️ Предупреждения

Система состоит из трёх файлов:
- **Hints.cs** — очередь подсказок для новых игроков
- **NoticePanel.cs** — одно уведомление (с пружинной анимацией)
- **Notices.cs** — менеджер всех уведомлений

---

## Файл `Hints.cs`

Управляет подсказками, которые появляются через определённое время.

### Полный исходный код

**Путь:** `Code/UI/Notices/Hints.cs`

```csharp
namespace Sandbox.UI;

public class Hints : GameObjectSystem<Hints>
{
	[Title( "Show UI Hints" )]
	[ConVar( "cl_showhints", ConVarFlags.Saved | ConVarFlags.GameSetting, Help = "Whether to display popup hints." )]
	public static bool cl_showhints { get; set; } = true;

	record class Hint( string Name, string Icon, RealTimeUntil Delay )
	{
		public bool Ready => Delay < 0;
	}

	List<Hint> _queue = new();

	public Hints( Scene scene ) : base( scene )
	{
		Queue( "openspawnmenu", "ℹ️", 10 );
		Queue( "openinspectmenu", "ℹ️", 40 );
		Queue( "openpausemenu", "ℹ️", 70 );

		Listen( Stage.StartUpdate, 0, Tick, "UpdateHints" );
	}

	public void Queue( string hintName, string hintIcon, float delay )
	{
		var hint = new Hint( hintName, hintIcon, delay );
		_queue.Add( hint );
	}

	RealTimeSince timeSinceLast = 0;

	void Tick()
	{
		if ( timeSinceLast < 3 )
			return;

		if ( !cl_showhints )
			return;

		var next = _queue.Where( x => x.Ready ).FirstOrDefault();
		if ( next is null ) return;

		_queue.Remove( next );
		timeSinceLast = 0;

		var phrase = Game.Language.GetPhrase( $"hint.{next.Name}" );
		phrase = ReplaceSpecialTokens( phrase );

		Notices.AddNotice( next.Icon, Color.White, phrase, 5 );
	}

	public void Cancel( string hintName )
	{
		_queue.RemoveAll( x => x.Name.Equals( hintName, StringComparison.OrdinalIgnoreCase ) );
	}

	string ReplaceSpecialTokens( string input )
	{
		if ( !input.Contains( '{' ) ) return input;
		if ( !input.Contains( '}' ) ) return input;

		// replace {input:<inputname>} with the key bound to that input
		{
			input = System.Text.RegularExpressions.Regex.Replace( input,
				@"{input:([^}]+)}",
				match =>
				{
					string key = match.Groups[1].Value.Trim();
					return $"<span class=\"key\"> {Input.GetButtonOrigin( key )} </span>";
				} );
		}

		return input;
	}
}
```

### Разбор кода Hints

| Элемент | Что делает |
|---------|-----------|
| `GameObjectSystem<Hints>` | Система, которая работает на уровне сцены (не привязана к конкретному объекту) |
| `cl_showhints` | Консольная переменная — можно отключить подсказки в настройках |
| `Hint` (record) | Одна подсказка: имя, иконка, задержка перед показом |
| Конструктор | Добавляет 3 подсказки: через 10 сек, 40 сек и 70 сек после старта |
| `Queue()` | Добавляет подсказку в очередь |
| `Tick()` | Каждый кадр проверяет: прошло ли 3 секунды с последней подсказки? Есть ли готовая? Показывает через `Notices.AddNotice()` |
| `Cancel()` | Удаляет подсказку из очереди (например, если игрок уже нашёл меню сам) |
| `ReplaceSpecialTokens()` | Заменяет `{input:SpawnMenu}` на реальную клавишу (например, «Q») |

---

## Файл `NoticePanel.cs`

Одна панель уведомления. Имеет пружинную анимацию — плавно «пружинит» к своей позиции.

### Полный исходный код

**Путь:** `Code/UI/Notices/NoticePanel.cs`

```csharp

namespace Sandbox.UI;

public class NoticePanel : Panel
{
	bool initialized;
	Vector3.SpringDamped _springy;

	public RealTimeUntil TimeUntilDie;

	/// <summary>
	/// If true, the notice won't auto-dismiss. Call <see cref="Dismiss"/> to remove it.
	/// </summary>
	public bool Manual { get; set; }

	public bool IsDead => !Manual && TimeUntilDie < 0;
	public bool wasDead = false;

	/// <summary>
	/// Dismiss a manual notice, causing it to slide out and be deleted.
	/// </summary>
	public void Dismiss()
	{
		Manual = false;
		TimeUntilDie = 0;
	}

	internal void UpdatePosition( Vector2 vector2 )
	{
		if ( initialized == false )
		{
			_springy = new Vector3.SpringDamped( new Vector3( Screen.Width + 50, vector2.y + Random.Shared.Float( -10, 10 ), 0 ), 0.0f );
			_springy.Velocity = Vector3.Random * 1000;
			initialized = true;
		}

		if ( !Manual && TimeUntilDie < 0.4f )
		{
			vector2.x -= 50;
		}

		// we're dead, push us out to rhe right
		if ( IsDead )
		{
			vector2.x = Screen.Width + 50;

			// we've been dead for 2 seconds, get rid of us
			if ( TimeUntilDie < -2 )
			{
				Delete();
				return;
			}

			wasDead = true;
		}

		_springy.Target = new Vector3( vector2.x, vector2.y, 0 );
		_springy.Frequency = 4;
		_springy.Damping = 0.5f;
		_springy.Update( RealTime.Delta * 1.0f );

		Style.Left = _springy.Current.x * ScaleFromScreen;
		Style.Top = _springy.Current.y * ScaleFromScreen;
	}
}
```

### Разбор кода NoticePanel

| Элемент | Что делает |
|---------|-----------|
| `_springy` | Пружинная физика для плавной анимации. Панель «пружинит» к целевой позиции |
| `TimeUntilDie` | Обратный таймер: когда дойдёт до 0 — панель «умирает» |
| `Manual` | Если `true`, панель не исчезнет автоматически (нужно вызвать `Dismiss()`) |
| `IsDead` | Панель «мертва», если не ручная и таймер истёк |
| `Dismiss()` | Принудительно убирает ручную панель |
| `UpdatePosition()` | Обновляет позицию с пружинной анимацией |

**Логика анимации:**
1. При создании — панель появляется за правым краем экрана (`Screen.Width + 50`) со случайной скоростью
2. Пружина тянет панель к целевой позиции (плавное появление)
3. За 0.4 секунды до исчезновения — панель начинает сдвигаться влево
4. После «смерти» — панель уезжает вправо за экран
5. Через 2 секунды после «смерти» — панель удаляется из памяти

---

## Файл `NoticePanel.cs.scss`

### Полный исходный код

**Путь:** `Code/UI/Notices/NoticePanel.cs.scss`

```scss
@import "/UI/Theme.scss";

NoticePanel
{
	position: absolute;
	left: 100%;            // start fully off-screen to the right; UpdatePosition() animates it in
	backdrop-filter: blur( 4px ) brightness( 0.4 );
	padding: 0.75rem 1.5rem;
	border-radius: 10px;
	font-family: $body-font;
	font-weight: 600;
	font-size: 18px;
	color: $color-text;
	gap: 10px;
	justify-content: center;
	align-items: center;
	overflow: hidden;

	.icon
	{
		font-size: 24px;
		font-family: "Material Icons";
	}

	&.progress::after
	{
		content: "";
		position: absolute;
		left: 0;
		bottom: 0;
		height: 3px;
		width: 100%;
		background-color: #08f;
		animation: progress-sweep 2s ease-in-out infinite;
	}
}

@keyframes progress-sweep
{
	0%
	{
		transform: translateX( -100% );
		opacity: 1;
	}

	50%
	{
		transform: translateX( 0% );
		opacity: 1;
	}

	85%
	{
		transform: translateX( 100% );
		opacity: 1;
	}

	100%
	{
		transform: translateX( 100% );
		opacity: 0;
	}
}

span.key
{
	font-weight: 500;
	color: #fff;
	background-color: #08f;
}
```

### Разбор стилей NoticePanel

| Элемент | Описание |
|---------|----------|
| `NoticePanel` | Размытый затемнённый фон, скруглённые углы, белый текст. **`left: 100%`** — стартовая позиция за правым краем экрана: пружина в `UpdatePosition()` сразу после первого кадра «всосёт» панель внутрь. Без этой строки уведомление мигало бы в верхнем-левом углу один кадр, пока спринг не отработал. |
| `.icon` | Иконка слева — используется шрифт Material Icons |
| `&.progress::after` | Полоса прогресса внизу уведомления — бегущая анимация |
| `@keyframes progress-sweep` | Анимация полосы: слева направо, затем исчезает |
| `span.key` | Стиль для клавиш в подсказках (голубой фон, белый текст) — например, «Q» |

---

## Файл `Notices.cs`

Менеджер уведомлений — создаёт и располагает все уведомления на экране.

### Полный исходный код

**Путь:** `Code/UI/Notices/Notices.cs`

```csharp
namespace Sandbox.UI;

public class Notices : PanelComponent
{
	public static Notices Current => Game.ActiveScene.Get<Notices>();

	public static NoticePanel AddNotice( string text, float seconds = 5 )
	{
		var current = Current;
		if ( current == null || current.Panel == null ) return null;

		var notice = new NoticePanel();

		notice.AddChild( new Label() { Text = text, Classes = "text", IsRich = true } );

		if ( seconds <= 0 )
			notice.Manual = true;
		else
			notice.TimeUntilDie = seconds;

		current.Panel.AddChild( notice );

		return notice;
	}

	public static NoticePanel AddNotice( string icon, Color iconColor, string text, float seconds = 5 )
	{
		var current = Current;
		if ( current == null || current.Panel == null ) return null;

		var notice = new NoticePanel();

		var iconPanel = new Label() { Text = icon, Classes = "icon" };
		iconPanel.Style.FontColor = iconColor;

		notice.AddChild( iconPanel );
		notice.AddChild( new Label() { Text = text, Classes = "text", IsRich = true } );

		if ( seconds <= 0 )
			notice.Manual = true;
		else
			notice.TimeUntilDie = seconds;

		current.Panel.AddChild( notice );

		return notice;
	}

	/// <summary>
	/// Send a notice to a specific connection. Must be called from the host.
	/// </summary>
	public static void SendNotice( Connection target, string icon, Color iconColor, string text, float seconds = 5 )
	{
		Assert.True( Networking.IsHost, "Must not be the host" );

		using ( Rpc.FilterInclude( target ) )
		{
			RpcAddNotice( icon, iconColor, text, seconds );
		}
	}

	[Rpc.Broadcast]
	private static void RpcAddNotice( string icon, Color iconColor, string text, float seconds )
	{
		AddNotice( icon, iconColor, text, seconds );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var innerBox = Panel.Box.RectInner;
		float y = 0;
		float gap = 5;
		foreach ( var p in Panel.Children.OfType<NoticePanel>().Reverse() )
		{
			var size = p.Box.RectOuter;

			var w = p.Box.RectOuter.Width;
			var h = p.Box.RectOuter.Height + gap;

			p.UpdatePosition( new Vector2( innerBox.Right - w, innerBox.Height - y - h ) );

			if ( !p.IsDead )
			{
				y += h;
			}
		}
	}
}
```

### Разбор кода Notices

| Элемент | Что делает |
|---------|-----------|
| `Current` | Статическое свойство — находит единственный экземпляр Notices в сцене |
| `AddNotice(text, seconds)` | Создаёт простое текстовое уведомление |
| `AddNotice(icon, color, text, seconds)` | Создаёт уведомление с иконкой |
| `SendNotice()` | Отправляет уведомление конкретному игроку (через сеть) |
| `RpcAddNotice()` | Сетевой метод — вызывается на компьютере получателя |
| `OnUpdate()` | Каждый кадр пересчитывает позиции всех уведомлений снизу вверх |

**Логика расположения (OnUpdate):**

Уведомления складываются стопкой снизу вверх:
1. Начинаем с нижнего правого угла
2. Для каждого уведомления: вычисляем позицию = правый край минус ширина, высота минус накопленная высота
3. Вызываем `UpdatePosition()` — пружинная анимация двигает панель к цели
4. Только «живые» панели занимают место в стопке

---

## Файл `Notices.cs.scss`

### Полный исходный код

**Путь:** `Code/UI/Notices/Notices.cs.scss`

```scss
Notices
{
	position: absolute;
	top: 0px;
	left: 0px;
	right: 0px;
	bottom: 0px;
	padding-right: 64px;
	padding-bottom: 300px;
}
```

Контейнер уведомлений занимает весь экран с отступами справа и снизу, чтобы уведомления не накладывались на другие элементы HUD.

---

## Как всё работает вместе

```
Hints (очередь подсказок)
  │
  │ через N секунд
  ▼
Notices.AddNotice("ℹ️", "Нажмите Q...")
  │
  ▼
Создаётся NoticePanel
  │
  ▼
NoticePanel появляется справа (пружинная анимация)
  │
  │ через 5 секунд
  ▼
NoticePanel уезжает вправо и удаляется
```


---

## ➡️ Следующий шаг

Переходи к **[05.09 — Этап 05_09 — PressableHud (Интерактивные объекты и подсказки)](05_09_PressableHud.md)**.
