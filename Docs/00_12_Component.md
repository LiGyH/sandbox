# 00.12 — Component (компонент)

## Что мы делаем?

Разбираем **главное понятие движка** — `Component`. Если `GameObject` — это «коробка», то компоненты — это **детали внутри коробки**, которые делают её полезной. Каждый `.cs` файл в Фазах 1–25, который ты будешь писать, — это или компонент, или ресурс, или интерфейс к компоненту.

## Что такое Component?

Компонент — это C# класс, который **навешивается на `GameObject`** и добавляет ему какую-то способность:

| Компонент | Что делает |
|---|---|
| `ModelRenderer` | Рисует 3D-модель в позиции GameObject |
| `Rigidbody` | Превращает GameObject в физический объект |
| `Collider` | Добавляет форму столкновения |
| `CameraComponent` | Превращает GameObject в камеру |
| `Player` (твой) | Управление игроком |
| `BaseWeapon` (твой) | Логика оружия |

У одного `GameObject` может быть **много компонентов одновременно**. Игрок — это `GameObject`, на который навешаны: `Player`, `CharacterController`, `ModelRenderer`, `PlayerInventory`, ещё десяток — и все работают вместе.

## Как написать свой компонент?

```csharp
public sealed class MyBehaviour : Component
{
    [Property] public float Speed { get; set; } = 100f;

    protected override void OnUpdate()
    {
        WorldPosition += Vector3.Forward * Speed * Time.Delta;
    }
}
```

Что здесь важно:
- Наследуемся от `Component`.
- `sealed` — рекомендуется, если ты не собираешься от него наследоваться (быстрее hotload).
- `[Property]` — поле станет видно в инспекторе.
- Внутри компонента `WorldPosition` — это позиция `GameObject`, на котором он висит.

## Методы жизненного цикла

Это **самое важное** для понимания поведения компонента. Движок сам вызывает эти методы в нужный момент:

| Метод | Когда вызывается |
|---|---|
| `OnAwake()` | Один раз при создании, до `OnStart`. Здесь ставят ссылки на другие компоненты. |
| `OnStart()` | Один раз, при первом включении. Гарантированно **до** первого `OnFixedUpdate`. Здесь — стартовая инициализация. |
| `OnEnabled()` | Каждый раз, когда компонент включается. |
| `OnUpdate()` | **Каждый кадр.** Это ~60-200 раз в секунду. Используй для рендера, UI, курсора. |
| `OnFixedUpdate()` | **Каждый тик физики** (обычно 50 раз/сек, стабильно). Используй для движения и физики. |
| `OnPreRender()` | Перед отрисовкой кадра, после расчёта анимаций костей. |
| `OnDisabled()` | Когда выключают. |
| `OnDestroy()` | При уничтожении. |
| `OnValidate()` | Когда в редакторе меняют `[Property]`. |
| `async Task OnLoad()` | При загрузке сцены. Если вернёт незавершённый `Task`, loading screen будет висеть, пока не дождётся. |

```csharp
public sealed class Enemy : Component
{
    protected override void OnStart()
    {
        Log.Info( "Враг появился!" );
    }

    protected override void OnUpdate()
    {
        // каждый кадр — повернуться к игроку
    }

    protected override void OnFixedUpdate()
    {
        // каждый тик физики — двигаться к игроку
    }

    protected override void OnDestroy()
    {
        Log.Info( "Враг умер." );
    }
}
```

### Update vs FixedUpdate — когда что?

- **`OnUpdate`** — визуальные вещи: поворот модели, UI, эффекты, чтение инпута.
- **`OnFixedUpdate`** — движение физики, перемещение игрока, траектории.

Игрок двигается в `OnFixedUpdate`, потому что физика должна быть **детерминированной и стабильной**. Если двигать в `OnUpdate`, на мощных ПК скорость будет «дрожать».

## Добавить компонент на GameObject

В редакторе: выдели `GameObject` → **Add Component** → имя компонента.

В коде:

```csharp
// Создать новый и прицепить
var renderer = go.AddComponent<ModelRenderer>();
renderer.Model = Model.Load( "models/dev/box.vmdl" );
renderer.Tint = Color.Red;

// Взять существующий или создать, если нет
var body = go.GetOrAddComponent<Rigidbody>();

// Получить уже существующий
var player = go.GetComponent<Player>();
```

## Поиск компонентов

```csharp
// На этом же GameObject
var model = GameObject.GetComponent<ModelRenderer>();

// И на детях
var child = GameObject.GetComponentInChildren<Weapon>();

// Все сразу на детях
var all = GameObject.GetComponentsInChildren<Weapon>();

// На родителях
var parentCtrl = GameObject.Components.GetComponentInParent<Player>();

// По всей сцене
var allWeapons = Scene.GetAll<BaseWeapon>();
```

## Обращение к GameObject из компонента

Из любого компонента доступны:

```csharp
GameObject                 // твой GameObject
Transform.World            // == GameObject.WorldTransform
WorldPosition              // сокращение для GameObject.WorldPosition
WorldRotation
Scene                      // сцена, в которой находится
Components                 // менеджер компонентов твоего GameObject
```

## Удаление компонента

```csharp
var rb = GetComponent<Rigidbody>();
rb.Destroy();           // удалить только этот компонент

// Или удалить сам GameObject (вместе со всеми компонентами)
GameObject.Destroy();
DestroyGameObject();    // то же самое, короче
```

После `Destroy()` использовать компонент **нельзя** — он мёртв.

## Почему `sealed` и почему `partial`

- `sealed class Foo : Component` — никто не сможет унаследоваться от `Foo`. Hotload работает быстрее.
- `partial class Foo` — можно разбить один класс на несколько файлов. Sandbox использует это активно: `Player.cs`, `Player.Camera.cs`, `Player.Ammo.cs` — всё это **один класс `Player`**, просто разрезанный по темам, чтобы файлы не были огромными.

## Результат

После этого этапа ты знаешь:

- ✅ Что такое `Component` и как он соотносится с `GameObject`.
- ✅ Жизненный цикл: `OnAwake` → `OnStart` → `OnUpdate`/`OnFixedUpdate` → `OnDestroy`.
- ✅ Разницу между `OnUpdate` (каждый кадр) и `OnFixedUpdate` (каждый тик физики).
- ✅ Как добавить, найти и удалить компонент.
- ✅ Зачем в Sandbox повсюду `sealed partial class`.

---

📚 **Facepunch docs:**
- [scene/components/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/components/index.md)
- [scene/components/component-methods.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/components/component-methods.md)

**Следующий шаг:** [00.13 — Атрибуты компонентов (`[Property]`, `[Sync]`, `[RequireComponent]`)](00_13_Component_Attributes.md)
