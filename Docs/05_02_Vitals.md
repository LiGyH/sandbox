# Этап 05_02 — Vitals (Здоровье, броня и патроны)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что это такое?

Представьте приборную панель автомобиля: спидометр показывает скорость, указатель топлива — сколько бензина осталось. **Vitals** — это такая же «приборная панель» для игрока. Она показывает три жизненно важных показателя:

- 💚 **Здоровье** (Health) — сколько урона может выдержать игрок
- 🛡️ **Броня** (Armour) — дополнительная защита
- 🔫 **Патроны** (Ammo) — сколько патронов в оружии

---

## Файл `Vitals.razor`

Это главный файл компонента. Расширение `.razor` означает, что в нём смешаны HTML-разметка (внешний вид) и C#-код (логика).

### Полный исходный код

**Путь:** `Code/UI/Vitals/Vitals.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

@if ( !Player.IsValid() || Player.WantsHideHud ) 
    return;

<root>
    <div class="vitals">
        @if ( Player.Armour > 0 )
        {
            <div class="stat armour hud-panel">
                <Image class="icon" Texture=@ArmourIcon />
                <label class="value">@(ArmourPercent)</label>
            </div>
        }
        <div class="stat health hud-panel">
            <Image class="icon" Texture=@HealthIcon />
            <label class="value">@(HealthPercent)</label>
        </div>
    </div>

    @if ( Weapon.IsValid() && Weapon.UsesAmmo )
    {
        <div class="ammo hud-panel">
            <label class="value">@(Weapon.UsesClips ? Weapon.ClipContents : Weapon.ReserveAmmo)</label>
            @if ( Weapon.UsesClips )
            {
                <label class="alternate">@Weapon.ReserveAmmo</label>
            }
            <Image class="icon" Texture=@AmmoIcon />
        </div>
    }
</root>

@code
{
    [Property] public Texture HealthIcon { get; set; }
    [Property] public Texture ArmourIcon { get; set; }
    [Property] public Texture AmmoIcon { get; set; }

    Player Player => Player.FindLocalPlayer();
    BaseWeapon Weapon => Player?.GetComponent<PlayerInventory>()?.ActiveWeapon as BaseWeapon;

    int HealthPercent => (int)(Player.Health / Player.MaxHealth * 100);
    int ArmourPercent => (int)(Player.Armour / Player.MaxArmour * 100);

    bool IsSpawnMenuOpen()
    {
        var host = Game.ActiveScene.Get<SpawnMenuHost>();
        return host?.Panel?.HasClass( "open" ) ?? false;
    }

    protected override void OnUpdate()
    {
        Panel.SetClass( "spawnmenu-open", IsSpawnMenuOpen() );
    }

    protected override int BuildHash()
    {
        if ( !Player.IsValid() || Player.WantsHideHud ) return 0;

        return System.HashCode.Combine( Player.Health, Player.Armour, Weapon?.ClipContents, Weapon?.ReserveAmmo );
    }
}
```

### Разбор кода

**Шапка файла:**

- `@using Sandbox;` и `@using Sandbox.UI;` — подключение библиотек движка
- `@inherits PanelComponent` — компонент наследует базовый класс панели
- `@namespace Sandbox` — пространство имён

**Разметка (HTML-часть):**

```razor
@if ( !Player.IsValid() || Player.WantsHideHud ) 
    return;
```
Если игрока нет или он хочет скрыть интерфейс — ничего не показываем.

```razor
@if ( Player.Armour > 0 )
```
Панель брони показывается только если у игрока есть броня.

```razor
@if ( Weapon.IsValid() && Weapon.UsesAmmo )
```
Панель патронов показывается только если у игрока есть оружие с патронами.

```razor
@(Weapon.UsesClips ? Weapon.ClipContents : Weapon.ReserveAmmo)
```
Если оружие использует магазины — показываем патроны в магазине, иначе — общий запас.

**Код (C#-часть):**

| Элемент | Что делает |
|---------|-----------|
| `[Property] public Texture HealthIcon` | Иконка здоровья — настраивается в редакторе |
| `Player.FindLocalPlayer()` | Находит локального игрока (того, кто сидит за компьютером) |
| `HealthPercent` | Вычисляет здоровье в процентах (например, 75 из 100 = 75%) |
| `IsSpawnMenuOpen()` | Проверяет, открыто ли меню спавна |
| `OnUpdate()` | Вызывается каждый кадр — добавляет/убирает CSS-класс `spawnmenu-open` |
| `BuildHash()` | Возвращает хэш-код для оптимизации — интерфейс перерисовывается только если значения изменились |

---

## Файл `Vitals.razor.scss`

Стили для расположения элементов на экране.

### Полный исходный код

**Путь:** `Code/UI/Vitals/Vitals.razor.scss`

```scss
@import "/UI/Theme.scss";

Vitals
{
	position: absolute;
	width: 100%;
	height: 100%;
	pointer-events: none;

	.vitals
	{
		position: absolute;
		bottom: $deadzone-y;
		left: $deadzone-x;
		flex-direction: row;
		gap: $deadzone-x;
		height: 96px;
		transition: transform 0.2s ease, opacity 0.2s ease;

		.hud-panel
		{
			min-width: 150px;
			justify-content: center;
		}
	}

	.ammo
	{
		position: absolute;
		bottom: $deadzone-y;
		right: $deadzone-x;
		min-width: 150px;
		justify-content: center;
		transition: transform 0.2s ease, opacity 0.2s ease;
	}

	&.spawnmenu-open
	{
		.vitals
		{
			transform: translateX(-40px);
			opacity: 0;
		}

		.ammo
		{
			transform: translateX(40px);
			opacity: 0;
		}
	}
}
```

### Разбор кода

| Стиль | Что делает |
|-------|-----------|
| `Vitals` | Корневой элемент — растянут на весь экран, не перехватывает клики |
| `.vitals` | Блок здоровья/брони — прижат к **левому нижнему** углу экрана |
| `.ammo` | Блок патронов — прижат к **правому нижнему** углу экрана |
| `transition: transform 0.2s ease` | Плавная анимация при появлении/исчезновении (за 0.2 секунды) |
| `&.spawnmenu-open` | Когда открыто меню спавна — панели плавно уезжают за края экрана и становятся прозрачными |

---

## Как выглядит на экране

```
┌──────────────────────────────────────────────┐
│                                              │
│                                              │
│                                              │
│                                              │
│  🛡️ 50    ❤️ 100                    🔫 30 90 │
└──────────────────────────────────────────────┘
   Броня    Здоровье              Магазин Запас
```


---

## ➡️ Следующий шаг

Переходи к **[05.03 — Этап 05_03 — Chat (Чат)](05_03_Chat.md)**.
