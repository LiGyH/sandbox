# Этап 05_05 — Scoreboard (Таблица счёта)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

<!-- toc -->
> **📑 Оглавление** (файл большой, используй ссылки для быстрой навигации)
>
> - [Что это такое?](#что-это-такое)
> - [Файл `Scoreboard.razor`](#файл-scoreboardrazor)
> - [Файл `ScoreboardRow.razor`](#файл-scoreboardrowrazor)
> - [Файл `Scoreboard.razor.scss`](#файл-scoreboardrazorscss)
> - [Как выглядит на экране](#как-выглядит-на-экране)

## Что это такое?

Представьте турнирную таблицу в спорте: список игроков, их очки, голы и статистика. **Scoreboard** — это такая же таблица в игре. Когда вы нажимаете клавишу Tab (Score), на экране появляется список всех игроков с их убийствами, смертями и пингом.

Компонент состоит из двух файлов: **Scoreboard** (сама таблица) и **ScoreboardRow** (одна строка — один игрок).

---

## Файл `Scoreboard.razor`

### Полный исходный код

**Путь:** `Code/UI/Scoreboard.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent

<root>
    <div class="body">
        <div class="title">
            <label class="big">sandbox</label>

            @if ( !string.IsNullOrEmpty( _mapTitle ) )
            {
                <label class="small">Playing on @(_mapTitle)</label>
            }
        </div>

        <div class="table">
            <div class="col">
                <div class="header row">
                    <div class="avatar-spacer"></div>
                    <div class="name"></div>
                    <div class="stat">Kills</div>
                    <div class="stat">Deaths</div>
                    <div class="stat">Ping</div>
                    <div class="mute-spacer"></div>
                </div>

                @foreach ( var entry in Scene.GetAll<PlayerData>().OrderByDescending( x => x.Kills ).ThenBy( x => x.DisplayName ) )
                {
                    <ScoreboardRow Entry="@entry" />
                }
            </div>
        </div>
    </div>
</root>

@code
{
    string _mapTitle = null;

    protected override void OnTreeFirstBuilt()
    {
        base.OnTreeFirstBuilt();
        FetchMap();
    }

    async void FetchMap()
    {
        var ident = Networking.MapName;

        var pkg = await Package.FetchAsync( ident, false );
        if ( pkg is not null )
        {
            _mapTitle = pkg.Title;
        }
    }

    protected override void OnUpdate()
    {
	    var host = Game.ActiveScene.Get<SpawnMenuHost>();
	    if ( host?.Panel?.HasClass( "open" ) ?? false )
	    {
		    Input.Clear( "Score" );
	    }

	    SetClass( "visible", Input.Down( "Score" ) );
    }

    /// <summary>
    /// update every second
    /// </summary>
    protected override int BuildHash() => System.HashCode.Combine( RealTime.Now.CeilToInt() );
}
```

### Разбор кода

**Разметка:**

- `<label class="big">sandbox</label>` — заголовок «sandbox» вверху таблицы
- `Playing on @(_mapTitle)` — название текущей карты
- Заголовки столбцов: Kills (Убийства), Deaths (Смерти), Ping (Пинг)
- `@foreach` — для каждого игрока создаётся компонент `ScoreboardRow`
- `.OrderByDescending( x => x.Kills )` — игроки сортируются по убийствам (больше = выше)
- `.ThenBy( x => x.DisplayName )` — при равных убийствах — по имени

**Код:**

| Элемент | Что делает |
|---------|-----------|
| `FetchMap()` | Асинхронно загружает название карты из пакета (пакеты в s&box — это карты, модели и т.д.) |
| `OnUpdate()` | Если открыто меню спавна — таблица не показывается. Иначе показывается при зажатой клавише Score (Tab) |
| `BuildHash()` | Обновляет таблицу каждую секунду (`RealTime.Now.CeilToInt()` меняется каждую секунду) |

---

## Файл `ScoreboardRow.razor`

Каждая строка таблицы — это отдельный компонент.

### Полный исходный код

**Путь:** `Code/UI/ScoreboardRow.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel

<root class="row player">

	@if ( Entry is not null && Entry.Connection is not null )
	{
		var steamId = Entry.Connection.SteamId;
		bool isMuted = SandboxVoice.IsMuted( steamId );

		<img class="avatar" src="avatar:@steamId" />
		<div class="name">@Entry.DisplayName</div>
		<div class="stat">@Entry.Kills</div>
		<div class="stat">@Entry.Deaths</div>
		<div class="stat">@(Entry.Ping.CeilToInt())</div>
		@if ( !Entry.IsMe )
		{
			<div class="mute-btn @(isMuted ? "muted" : "")" onclick="@( () => SandboxVoice.Mute( steamId ) )">
				<i>@(isMuted ? "volume_off" : "volume_up")</i>
			</div>
		}
		else
		{
			<div class="mute-spacer"></div>
		}
	}

</root>

@code
{
    public PlayerData Entry { get; set; }

    public override void Tick()
    {
        if ( Entry is null ) return;
        SetClass( "me", Entry.IsMe );
        if ( Entry.Connection is not null )
            SetClass( "friend", new Friend( Entry.Connection.SteamId ).IsFriend );
    }

    protected override void OnRightClick( MousePanelEvent e )
    {
        if ( Entry is null || Entry.Connection is null ) return;

        var steamId = Entry.Connection.SteamId;
        var menu = MenuPanel.Open( this );

        menu.AddOption( "content_copy", "Copy Steam ID", () =>
        {
            Clipboard.SetText( steamId.ToString() );
            Notices.AddNotice( "copy_all", Color.Cyan, $"Copied {Entry.Connection.DisplayName}'s SteamID to your clipboard", 5 );
        } );

		if ( !Entry.IsMe )
		{
			bool isMuted = SandboxVoice.IsMuted( steamId );
			menu.AddOption( isMuted ? "volume_up" : "volume_off", isMuted ? "Unmute" : "Mute", () => SandboxVoice.Mute( steamId ) );

			if ( Networking.IsHost )
			{
				menu.AddSpacer();
				menu.AddOption( "person_remove", "Kick", () => OpenKickConfirm( Entry ) );
				menu.AddOption( "gavel", "Ban", () => OpenBanConfirm( Entry ) );
			}
		}

		e.StopPropagation();
	}

	void OpenKickConfirm( PlayerData entry )
	{
		var popup = new StringQueryPopup
		{
			Title = "Kick Player",
			Prompt = $"Why do you want to kick {entry.DisplayName}?",
			ConfirmLabel = "Kick",
			OnConfirm = x =>
			{
				entry.Connection?.Kick( x );

                // only the host can run this so it's fine
				Game.ActiveScene.Get<Chat>()?.AddSystemText( $"{entry.DisplayName} was kicked: {x}", "🚪" );
			}
		};
		popup.Parent = FindPopupPanel();
	}

	void OpenBanConfirm( PlayerData entry )
	{
		var popup = new StringQueryPopup
		{
			Title = "Ban Player",
			Prompt = $"Why do you want to ban {entry.DisplayName}?",
			ConfirmLabel = "Ban",
			OnConfirm = x => BanSystem.Current?.Ban( entry.Connection, x )
		};
		popup.Parent = FindPopupPanel();
	}
}
```

### Разбор кода ScoreboardRow

**Разметка каждой строки:**

- `<img class="avatar" src="avatar:@steamId" />` — аватар игрока из Steam
- `@Entry.DisplayName` — имя игрока
- `@Entry.Kills`, `@Entry.Deaths`, `@Entry.Ping` — статистика
- Кнопка мьюта (🔇) — появляется только для **чужих** игроков

**Метод `Tick()`:**

- Добавляет класс `me` для вашей строки (выделение)
- Добавляет класс `friend` для друзей из Steam

**Контекстное меню (правый клик):**

| Пункт | Что делает |
|-------|-----------|
| Copy Steam ID | Копирует Steam ID в буфер обмена |
| Mute/Unmute | Заглушить/включить голос игрока |
| Kick (только хост) | Кикнуть игрока с указанием причины |
| Ban (только хост) | Забанить игрока с указанием причины |

---

## Файл `Scoreboard.razor.scss`

### Полный исходный код

**Путь:** `Code/UI/Scoreboard.razor.scss`

```scss
@import "/UI/Theme.scss";

Scoreboard
{
	position: absolute;
	justify-content: center;
	align-items: center;
	font-family: $body-font;
	font-size: 18px;
	opacity: 0;
	transform: translateY( -32px ) scale( 1.02 );
	transition: all 0.1s ease-in;
	background-color: rgba( 0, 0, 0, 0.5 );
	backdrop-filter: blur( 10px );
	margin: auto;
	width: 100%;
	height: 100%;
	margin: auto;
	z-index: 9199;
	gap: 16px;

	.voting
	{
		width: 20%;
	}

	.body
	{
		flex-direction: column;
	}

	.title
	{
		width: 100%;
		align-items: center;
		flex-direction: column;
		padding: 0px 32px;

		label
		{
			font-size: 32px;
			font-weight: 600;
			opacity: 1;
			height: 40px;
			text-shadow: 1px 1px 5px black;
		}

		.big
		{
			font-size: 40px;
			font-weight: 900;
			color: white;
			height: 58px;
			border-bottom: 5px solid $color-accent;
			margin-bottom: 16px;
		}

		> .small
		{
			font-size: 18px;
			color: linear-gradient( to right, #cdf, #8ea3ce );
		}
	}

	.avatar
	{
		margin-left: 4px;
		width: 28px;
		border-radius: 50%;
		height: 28px;
		box-shadow: 0px 0px 10px rgba( #cdf, 0.2 );
	}

	.avatar-spacer
	{
		margin-left: 4px;
		width: 28px;
	}

	.mute-spacer
	{
		width: 36px;
	}

	.table
	{
		overflow: hidden;
		flex-direction: column;
		width: 600px;
		color: #cdf;
		border-radius: 2px;
		min-height: 256px;
		max-height: 1024px;
		min-width: 768px;

		.center label
		{
			flex-grow: 1;
			text-align: center;
		}

		.col
		{
			flex-direction: column;
			padding: 8px;
			gap: 4px;
		}

		.row
		{
			width: 100%;
			white-space: nowrap;
			align-items: center;
			border-radius: 8px;
			padding: 4px 8px;
			gap: 8px;

			> .name
			{
				font-weight: 600;
				flex-grow: 1;
			}

			> .stat
			{
				width: 100px;
				justify-content: center;
				text-transform: lowercase;
				font-weight: 600;
			}
		}

		.header
		{
			align-items: flex-end;
			justify-content: flex-end;

			> .stat
			{
				font-size: 16px;
				font-weight: 600;
				opacity: 0.2;
				height: 20px;
			}
		}

		.mute-btn
		{
			width: 36px;
			justify-content: center;
			align-items: center;
			cursor: pointer;
			opacity: 0.3;
			transition: opacity 0.15s ease;
			font-size: 20px;

			&:hover
			{
				opacity: 0.8;
			}

			&.muted
			{
				opacity: 0.6;
				color: #e55;

				&:hover
				{
					opacity: 1;
				}
			}
		}

		.player
		{
			height: 50px;
			background-color: rgba( #0c202fcc, 0.7 );
			border: 0.25px solid rgba( black, 0.1 );
			text-shadow: 0px 0px 5px rgba( black, 0.75 );
			transition: all 0.1s ease;

			&.me
			{
				/*background-image: linear-gradient( to right, rgba( black, 0.5 ), rgba( black, 0 ));*/
			}

			&.friend
			{
				.avatar
				{
					box-shadow: 0px 0px 15px rgba( #70cf4f, 0.2 );
				}
			}

			&:hover
			{
				border: 1px solid rgba( black, 0.1 );
				background-color: rgba( #194769cc, 0.4 );
			}
		}
	}

	&.visible
	{
		opacity: 1.0;
		transform: scale( 1 );
		transition: all 0.1s ease-out;
		pointer-events: all;
	}
}
```

### Разбор стилей

| Элемент | Описание |
|---------|----------|
| `Scoreboard` | По умолчанию невидим (`opacity: 0`), занимает весь экран с тёмным размытым фоном |
| `&.visible` | При нажатии Tab — становится видимым, плавно появляясь |
| `.title .big` | Заголовок «sandbox» — крупный, жирный, с голубой линией снизу |
| `.player` | Строка игрока — тёмный фон, при наведении становится светлее |
| `.player.friend .avatar` | У друзей из Steam аватар подсвечивается зелёным |
| `.mute-btn` | Кнопка мьюта — по умолчанию еле видна, при наведении ярче. Красная если заглушен |

---

## Как выглядит на экране

```
┌──────────────────────────────────────────────┐
│                   sandbox                     │
│              Playing on gm_construct           │
│                                               │
│  👤  Имя              Kills  Deaths  Ping  🔊 │
│  ─────────────────────────────────────────── │
│  🟢  Игрок1            15      3      25   🔊 │
│  🔵  Игрок2            10      7      48   🔇 │
│  🔵  Игрок3             5     12      92   🔊 │
└──────────────────────────────────────────────┘
```


---

## ➡️ Следующий шаг

Переходи к **[05.06 — Этап 05_06 — Nameplate (Табличка с именем)](05_06_Nameplate.md)**.
