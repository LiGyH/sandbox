# Этап 06_09 — Inventory UI (Панель инвентаря / хотбар)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

<!-- toc -->
> **📑 Оглавление** (файл большой, используй ссылки для быстрой навигации)
>
> - [Что мы делаем?](#что-мы-делаем)
> - [Зачем это нужно?](#зачем-это-нужно)
> - [Как это работает внутри движка?](#как-это-работает-внутри-движка)
> - [Создай файл](#создай-файл)

## Что мы делаем?

Создаём UI-компоненты `Inventory` и `InventorySlot` — панель быстрого доступа к оружию в нижней части экрана. Это «хотбар» — горизонтальная полоска слотов, в каждом из которых может лежать оружие или инструмент.

## Зачем это нужно?

Представьте пояс с кармашками, в каждом — инструмент. Вы смотрите на пояс, видите иконки и подписи, быстро переключаетесь между ними нажатием клавиш 1–9 или колёсиком мыши. Это и есть хотбар:

- Показывает все слоты инвентаря с иконками оружия
- Подсвечивает активное оружие
- Позволяет переключаться клавишами 1–9, колёсиком мыши или перетаскиванием (drag & drop)
- Поддерживает перетаскивание оружия между слотами и выбрасывание
- Скрывается/смещается при открытом спавн-меню

## Как это работает внутри движка?

### Архитектура

```
Inventory (PanelComponent)
  ├── InventorySlot [0] — отдельный слот (Panel)
  ├── InventorySlot [1]
  ├── ...
  ├── InventorySlot [N]
  └── HotbarPresetsButton — кнопка пресетов
```

- **`Inventory`** — контейнер, создаёт слоты по количеству `inventory.MaxSlots`.
- **`InventorySlot`** — один слот: показывает иконку, название, обрабатывает drag & drop.
- **`HandleInput()`** — обработка клавиш 1–9, колёсика мыши и `invprev`.
- **`SelectSlot()`** — прямой выбор слота по номеру, с возможностью убрать оружие (holster).
- **`MoveSlot()`** — циклическое переключение вперёд/назад по списку доступных оружий.
- **Drag & Drop** — `InventorySlot` поддерживает перетаскивание: можно менять оружие местами или выбрасывать из хотбара.

### CSS-стили

- Хотбар абсолютно позиционирован внизу экрана, центрирован.
- Каждый слот 96×96px с фоном, иконкой и подписью.
- Состояния: `active`, `selected`, `hovered`, `empty`, `drag-hover` — каждое со своими стилями.
- При открытом спавн-меню хотбар становится полупрозрачным и смещается вниз.

## Создай файл

📁 `Code/UI/Inventory/Inventory.razor`

```razor
﻿@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@implements Global.IPlayerEvents

@if ( Player.IsValid() && Player.WantsHideHud )
    return;

@if ( !inventory.IsValid() )
    return;

<root>
	@for (int i = 0; i < inventory.MaxSlots; i++)
	{
		<InventorySlot Index=@i Inventory=@inventory Active=@(activeSlot == i)></InventorySlot>
	}
	<HotbarPresetsButton Inventory=@inventory></HotbarPresetsButton>
</root>

@code
{
    [Property, Group("Sound")] public SoundEvent SwitchSound { get; set; }

    Player Player => Player.FindLocalPlayer();

    PlayerInventory inventory;
    int activeSlot = -1;
    BaseCarryable prev;

    protected override int BuildHash() => HashCode.Combine( inventory, activeSlot, Player?.WantsHideHud );

    void Global.IPlayerEvents.OnPickup(BaseCarryable weapon)
    {
        StateHasChanged();
    }

    bool IsSpawnMenuOpen()
    {
	    var host = Game.ActiveScene.Get<SpawnMenuHost>();
	    return host?.Panel?.HasClass("open") ?? false;
    }

    protected override void OnUpdate()
    {
        inventory = Game.ActiveScene.GetAllComponents<PlayerInventory>().Where(x => x.Network.IsOwner).FirstOrDefault();
        activeSlot = inventory?.ActiveWeapon?.InventorySlot ?? -1;

        Panel.SetClass( "spawnmenu-open", IsSpawnMenuOpen() );
    }

    public void HandleInput()
    {
        if (inventory is null)
            return;

        MoveSlot(-(int)Input.MouseWheel.y);

        if ( Input.Pressed( "invprev" ) && prev.IsValid() )
        {
            var weapon = prev;
            prev = inventory.ActiveWeapon;
            inventory.SwitchWeapon( weapon );
        }

        if (Input.Pressed("SlotNext")) MoveSlot(1);
        if (Input.Pressed("SlotPrev")) MoveSlot(-1);

        if (Input.Pressed("Slot1")) SelectSlot(0);
        if (Input.Pressed("Slot2")) SelectSlot(1);
        if (Input.Pressed("Slot3")) SelectSlot(2);
        if (Input.Pressed("Slot4")) SelectSlot(3);
        if (Input.Pressed("Slot5")) SelectSlot(4);
        if (Input.Pressed("Slot6")) SelectSlot(5);
        if (Input.Pressed("Slot7")) SelectSlot(6);
        if (Input.Pressed("Slot8")) SelectSlot(7);
        if (Input.Pressed("Slot9")) SelectSlot(8);
    }

    /// <summary>
    /// Pressing a number key directly switches to that slot's weapon.
    /// If the slot is empty or already active, holsters the current weapon.
    /// </summary>
    public void SelectSlot( int slot )
    {
        var weapon = inventory.GetSlot( slot );

        if ( weapon.IsValid() && !weapon.CanSwitch() )
            return;

        prev = inventory.ActiveWeapon;
        inventory.SwitchWeapon( weapon, allowHolster: true );

        Sound.Play( SwitchSound );
    }

    public void MoveSlot( int delta )
    {
        if ( delta == 0 )
            return;

        var weapons = inventory.Weapons.Where( x => x.CanSwitch() ).ToList();

        if ( weapons.Count == 0 )
            return;

        // Find current position in the ordered weapon list
        BaseCarryable current = inventory.ActiveWeapon ?? weapons.FirstOrDefault();

        int currentIndex = weapons.IndexOf( current );
        currentIndex += delta;
        currentIndex %= weapons.Count;
        if ( currentIndex < 0 )
            currentIndex = weapons.Count + currentIndex;

        var target = weapons[currentIndex];

        prev = inventory.ActiveWeapon;
        inventory.SwitchWeapon( target );

        Sound.Play( SwitchSound );
    }
}
```

### Разбор ключевых частей

- **`@implements Global.IPlayerEvents`** — подписка на события локального игрока. Метод `OnPickup` вызывается при подборе оружия и обновляет UI.
- **`BuildHash()`** — Razor пересобирает панель только когда хеш изменился (инвентарь, активный слот, скрытие HUD).
- **`HandleInput()`** — центральный метод обработки ввода. Колёсико мыши (`MouseWheel.y`) переключает слоты, `invprev` возвращает к предыдущему оружию.
- **`SelectSlot(slot)`** — при нажатии числовой клавиши: переключает на оружие в слоте. Если слот пуст или уже активен — убирает оружие.
- **`MoveSlot(delta)`** — циклический перебор: фильтрует оружия с `CanSwitch()`, двигается по списку с оборотом через начало/конец.

---

📁 `Code/UI/Inventory/Inventory.razor.scss`

```scss
@import "/UI/Theme.scss";

Inventory
{
    position: absolute;
    bottom: $deadzone-y;
    left: 0;
    width: 100%;
    justify-content: center;
    font-size: 18px;
    color: $color-accent;
    gap: 4px;
    z-index: 1050;
    pointer-events: none;
    transition: all 0.05s linear;

    &.spawnmenu-open
    {
        opacity: 0.75;
        transform: translateY( 32px );

        HotbarPresetsButton
        {
            opacity: 1;
        }

        InventorySlot .index
        {
            opacity: 0;
        }
    }
}
```

### Разбор стилей Inventory

- **`bottom: $deadzone-y`** — прижат к низу экрана с отступом из переменной темы (safe zone).
- **`pointer-events: none`** — панель не перехватывает клики (слоты сами включают `pointer-events` при необходимости).
- **`&.spawnmenu-open`** — при открытом спавн-меню хотбар становится полупрозрачным (`opacity: 0.75`) и смещается вниз на 32px.

---

📁 `Code/UI/Inventory/InventorySlot.razor`

```razor
﻿@using Sandbox;
@using Sandbox.UI;
@inherits Panel

<root>
    @if ( !Input.UsingController )
    {
        <InputHint class="index" Action=@($"Slot{Index + 1}")></InputHint>
    }

    @{
        var weapon = GetWeapon();
    }

    @if (weapon.IsValid())
    {
        <div class="icon" @ref="IconPanel"></div>
        <div class="name">@weapon.DisplayName</div>
    }
</root>

@code
{
    public PlayerInventory Inventory { get; set; }
    public bool Active { get; set; }
    public bool Hovered { get; set; }

    public int Index { get; set; }

    Panel IconPanel { get; set; }

    static InventorySlot _hoveredDragSlot;

    public override bool WantsDrag => true;

    protected override void OnDragStart( DragEvent e )
    {
        var weapon = GetWeapon();
        if ( !weapon.IsValid() ) return;

        var icon = weapon.InventoryIconOverride ?? weapon.DisplayIcon?.ResourcePath;

        DragHandler.StartDragging( new DragData
        {
            Source = this,
            Icon = icon,
            Title = weapon.DisplayName
        } );
    }

    protected override void OnDragEnd( DragEvent e )
    {
        var data = DragHandler.Current;
        DragHandler.StopDragging();
        base.OnDragEnd( e );

        // OnDrop on the target already handled the swap.
        if ( data?.Handled == true )
        {
            if ( data.Source is not null ) data.Source.UserData = null;
            return;
        }

        // Mouse is hovering over a different slot — OnDrop will fire and handle the swap.
        // Don't clear UserData yet so OnDrop can still retrieve it via e.Target.UserData.
        if ( _hoveredDragSlot is not null && _hoveredDragSlot != this )
            return;

        // No slot handled it — clean up.
        if ( data?.Source is not null ) data.Source.UserData = null;

        // If the mouse is still over this slot the user dragged back to the source — no-op.
        var mousePos = Mouse.Position * ScaleFromScreen;
        if ( Box.RectOuter.IsInside( mousePos ) ) return;

        var weapon = GetWeapon();
        if ( !Inventory.Drop( weapon ) && weapon.IsValid() )
            Inventory.Remove( weapon );
    }

    BaseCarryable GetWeapon()
    {
        if (!Inventory.IsValid()) return null;

        return Inventory.GetSlot(Index);
    }

    bool IsSpawnMenuOpen()
    {
        var host = Game.ActiveScene.Get<SpawnMenuHost>();
        return host?.Panel?.HasClass("open") ?? false;
    }

    public override void Tick()
    {
        base.Tick();

        var weapon = GetWeapon();
        var hasWeapon = weapon.IsValid();

        var spawnMenuOpen = IsSpawnMenuOpen();
        SetClass("droppable", spawnMenuOpen);

        SetClass("active", Hovered || Active);
        SetClass("empty", !hasWeapon);
        SetClass("selected", Active && hasWeapon);
        SetClass("hovered", Hovered && hasWeapon);
        SetClass("switchable", hasWeapon && weapon.CanSwitch());
        SetClass("no-tint", hasWeapon && weapon is SpawnerWeapon);

        if (hasWeapon && IconPanel is not null)
        {
            var iconOverride = weapon.InventoryIconOverride;
            if (!string.IsNullOrEmpty(iconOverride))
            {
                IconPanel.Style.SetBackgroundImage(iconOverride);
            }
            else
            {
                IconPanel.Style.SetBackgroundImage(weapon.DisplayIcon);
            }
        }
    }

    protected override void OnDragEnter(PanelEvent e)
    {
        AddClass("drag-hover");
        _hoveredDragSlot = this;
    }

    protected override void OnDragLeave(PanelEvent e)
    {
        RemoveClass("drag-hover");
        if ( _hoveredDragSlot == this ) _hoveredDragSlot = null;
    }

    protected override void OnDrop( PanelEvent e )
    {
        RemoveClass( "drag-hover" );
        _hoveredDragSlot = null;

        // Use DragHandler.Current first; fall back to UserData for the case where
        // OnDragEnd fired first and cleared Current (but preserved UserData).
        var data = DragHandler.Current ?? e.Target.UserData as DragData;

        // Clean up UserData now that the drop is being processed.
        if ( data?.Source is not null ) data.Source.UserData = null;

        if ( data is null ) return;

        // Hotbar-to-hotbar: move or swap the dragged slot with this slot.
        if ( data.Source is InventorySlot sourceSlot && sourceSlot.Inventory == Inventory )
        {
            data.Handled = true;
            Inventory.MoveSlot( sourceSlot.Index, Index );
            return;
        }

        GameManager.GiveSpawnerWeaponAt( data.Type, data.Path, Index, data.Data as string, data.Icon, data.Title );
    }

    protected override int BuildHash() => HashCode.Combine(GetWeapon(), Hovered, Active, Input.UsingController);
}
```

### Разбор ключевых частей InventorySlot

- **`@inherits Panel`** — в отличие от `Inventory`, слот наследует `Panel` (не `PanelComponent`), поскольку создаётся как дочерний элемент.
- **`InputHint`** — подсказка клавиши (1–9). Скрывается при использовании контроллера (`Input.UsingController`).
- **`WantsDrag => true`** — включает поддержку drag & drop для этой панели.
- **`OnDragStart`** — начало перетаскивания: создаёт `DragData` с иконкой и названием оружия.
- **`OnDragEnd`** — конец перетаскивания: если оружие перетащили за пределы слотов — выбрасывает (`Drop`) или удаляет (`Remove`).
- **`OnDrop`** — при отпускании на этом слоте: меняет оружие местами (`MoveSlot`) или создаёт спавнер-оружие из спавн-меню.
- **`Tick()`** — каждый кадр обновляет CSS-классы и иконку. Класс `no-tint` убирает тонировку для `SpawnerWeapon`.

---

📁 `Code/UI/Inventory/InventorySlot.razor.scss`

```scss
@import "/UI/Theme.scss";

InventorySlot
{
    width: 96px;
    height: 96px;
    background-color: #0003;
    border-radius: $hud-radius;
    transition: all 0.1s ease;
    position: relative;

    &.droppable
    {
        pointer-events: all;
    }
}

InventorySlot .index
{
    position: absolute;
    bottom: -41px;
    left: 50%;
    transform: translateX( -50% );
    width: 35px;
    height: 35px;
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
    opacity: 0.3;
    margin: 0 auto;
    pointer-events: none;
}

InventorySlot .icon
{
    position: absolute;
    top: 16px;
    left: 4px;
    right: 4px;
    bottom: 18px;
    background-position: center;
    background-size: contain;
    background-repeat: no-repeat;
    background-tint: rgba( $color-accent, 0.6 );
    mix-blend-mode: lighten;
    pointer-events: none;
}

InventorySlot .name
{
    position: absolute;
    bottom: 4px;
    left: 4px;
    right: 4px;
    font-size: 12px;
    font-weight: 600;
    font-family: $subtitle-font;
    color: rgba( white, 0.6 );
    text-align: center;
    pointer-events: none;
    justify-content: center;
}

InventorySlot.no-tint .icon
{
    background-tint: white;
    opacity: 0.85;
}

InventorySlot.empty
{
    opacity: 1;

    .index
    {
        opacity: 0;
    }
}

InventorySlot:not( .empty )
{
    background-color: $hud-element-bg;
}

InventorySlot.drag-hover
{
    background-color: rgba( $color-accent, 0.15 );
    outline: 1px solid rgba( $color-accent, 0.5 );
    box-shadow: 0px 0px 16px rgba( $color-accent, 0.3 );

    .index
    {
        opacity: 1;
    }
}

InventorySlot.active.no-tint .icon
{
    opacity: 1;
    mix-blend-mode: normal;
    background-tint: white;
}

InventorySlot.active
{
    opacity: 1;
    background-color: $hud-element-bg;
    outline: 0.5px solid rgba( $color-accent, 0.3 );

    .index
    {
        color: $color-accent;
    }

    .icon
    {
        background-tint: $color-accent;
    }
}

InventorySlot.hovered
{
    background-color: $hud-element-bg;
    outline: 0.5px solid rgba( $color-accent, 0.2 );

    .icon
    {
        background-tint: $color-accent;
    }

    .name
    {
        color: rgba( white, 0.9 );
    }
}

InventorySlot.selected
{
    opacity: 1;
    background-color: rgba( $color-accent, 0.8 );
    box-shadow: 0px 0px 12px rgba( $color-accent, 0.2 );

    .index
    {
        color: $color-accent;
    }

    .icon
    {
        background-tint: $color-accent;
        top: 8px;
        left: 2px;
        right: 2px;
        bottom: 14px;
    }

    .name
    {
        color: white;
    }
}
```

### Разбор стилей InventorySlot

- **Размер слота** — 96×96px с полупрозрачным фоном (`#0003`).
- **`.index`** — подсказка клавиши расположена под слотом (`bottom: -41px`), центрирована.
- **`.icon`** — иконка оружия с тонировкой `$color-accent` и `mix-blend-mode: lighten` для красивого наложения.
- **`.name`** — название оружия внизу слота, мелким шрифтом.
- **Состояния**:
  - `empty` — пустой слот, индекс скрыт.
  - `active` — слот активен (обводка, яркая иконка).
  - `selected` — активный слот с оружием (яркий фон цвета акцента, увеличенная иконка).
  - `hovered` — курсор над слотом (мягкая обводка).
  - `drag-hover` — оружие перетаскивается над слотом (свечение и обводка).
  - `no-tint` — для SpawnerWeapon: иконка без тонировки (белая).


---


---

<!-- seealso -->
## 🔗 См. также

- [03.08 — PlayerInventory](03_08_PlayerInventory.md)
- [04.01 — GameManager](04_01_GameManager.md)

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[07.00 — Обзор всех видов оружия](07_00_Weapons_Overview.md)** — начало следующей фазы (прочитай обзор ДО отдельных файлов 07.XX).
