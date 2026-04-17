# 00.15 — Scene и GameObjectSystem

## Что мы делаем?

Разбираемся, **что такое сцена** (`Scene`) и как в ней искать объекты. Также знакомимся с `GameObjectSystem` — механизмом, который запускается в точно определённой точке кадра и работает сразу со всеми объектами нужного типа.

## Что такое Scene?

`Scene` — это **контейнер всех `GameObject` текущего уровня**. Один уровень = одна сцена. Каждый `GameObject` принадлежит ровно одной сцене.

```
Scene
├── Map (статика карты)
├── PlayerSpawn_1
├── PlayerSpawn_2
├── Light_Point
├── Player (если игрок уже в игре)
│   ├── Camera
│   └── Weapon
├── Pickup_Ammo
└── Prop_Barrel (×20)
```

## Как получить текущую сцену?

Из компонента:

```csharp
Scene scene = this.Scene;   // сокращение от GameObject.Scene
```

Глобально, откуда угодно:

```csharp
Scene active = Game.ActiveScene;
```

## Загрузка другой сцены

```csharp
// Заменить текущую сцену
Scene.Load( mySceneResource );
Scene.LoadFromFile( "scenes/game.scene" );

// Или аддитивно — поверх существующей
var load = new SceneLoadOptions();
load.SetScene( mySceneResource );
load.IsAdditive = true;
Scene.Load( load );
```

## Поиск GameObject/Component в сцене

### По GUID

У каждого `GameObject` есть `Guid` (уникальный 128-битный идентификатор). Быстрый поиск:

```csharp
var obj = Scene.Directory.FindByGuid( guid );
```

### Все компоненты определённого типа

Внутри сцена держит **быстрый индекс** по типам. Не надо бежать по всем `GameObject`:

```csharp
// Все ModelRenderer в сцене — мгновенно
foreach ( var mr in Scene.GetAll<ModelRenderer>() )
{
    mr.Tint = Color.Green;
}

// Один, первый попавшийся (часто для singleton-менеджеров)
var game = Scene.Get<GameManager>();
```

Это базовый способ **доставать GameManager, настройки и прочие "одиночки"** в Sandbox.

### Все GameObject

```csharp
foreach ( var go in Scene.GetAllObjects( true /* включая выключенные */ ) )
{
    // ...
}
```

## GameObjectSystem — что это?

Бывают задачи, которые **неправильно делать в `OnUpdate`** — либо потому что нужен строгий порядок, либо потому что выгоднее обработать все объекты одним пакетом.

Пример из движка: `SceneAnimationSystem` рассчитывает анимации **всех** `SkinnedModelRenderer` параллельно в одном месте кадра. Это быстрее и даёт гарантию, что к `OnPreRender` все кости уже посчитаны.

Ты можешь написать свой системный класс:

```csharp
public class MyGameSystem : GameObjectSystem
{
    public MyGameSystem( Scene scene ) : base( scene )
    {
        // В какой момент кадра, с каким приоритетом, и что делать
        Listen( Stage.PhysicsStep, 10, DoSomething, "DoingSomething" );
    }

    void DoSomething()
    {
        var allThings = Scene.GetAllComponents<MyThing>();
        // сделать что-то со всеми
    }
}
```

Движок сам создаст копию системы для каждой сцены — ничего регистрировать не нужно.

## Singleton через GameObjectSystem

Очень удобный приём:

```csharp
public class AchievementSystem : GameObjectSystem<AchievementSystem>
{
    public AchievementSystem( Scene scene ) : base( scene ) { }

    public void Unlock( string id )
    {
        Log.Info( $"Unlocked: {id}" );
    }
}
```

Использование из любого места:

```csharp
AchievementSystem.Current.Unlock( "first_kill" );
```

`GameObjectSystem<T>` даёт статический `T Current`, который указывает на текущую сцену — **не нужно искать его через `Scene.Get`**.

В Sandbox именно так сделаны: `UndoSystem`, `SaveSystem`, `CleanupSystem`, `BanSystem`, `FreeCamGameObjectSystem` — см. Фазы 23–24.

## Stage — когда в кадре запускается твой код

```csharp
Listen( Stage.StartUpdate,      order, fn );   // перед OnUpdate
Listen( Stage.PhysicsStep,      order, fn );   // во время FixedUpdate
Listen( Stage.FinishUpdate,     order, fn );   // после OnUpdate
Listen( Stage.SceneLoaded,      order, fn );   // после загрузки сцены
Listen( Stage.UpdateBones,      order, fn );   // перед рендером
```

`order` — целое число. Меньше = раньше. Если двух систем `order` одинаковы, порядок не гарантирован.

## Когда выбирать что?

| Задача | Используй |
|---|---|
| Каждый кадр на одном объекте | `OnUpdate` в компоненте |
| Движение/физика на одном объекте | `OnFixedUpdate` в компоненте |
| Посчитать что-то один раз за кадр по ВСЕМ объектам типа | `GameObjectSystem` |
| Глобальный менеджер (один на сцену) | `GameObjectSystem<T>` |
| Достать менеджер из другого кода | `AchievementSystem.Current` или `Scene.Get<T>()` |

## Результат

После этого этапа ты знаешь:

- ✅ Что такое `Scene` и как получить текущую.
- ✅ Как грузить сцены (включая аддитивно).
- ✅ Как искать компоненты по всей сцене быстро.
- ✅ Что такое `GameObjectSystem` и зачем он.
- ✅ Приём singleton через `GameObjectSystem<T>.Current` — он используется в Sandbox часто.

---

📚 **Facepunch docs:**
- [scene/scenes/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/scenes/index.md)
- [scene/gameobjectsystem.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/gameobjectsystem.md)

**Следующий шаг:** [00.16 — Prefabs (префабы)](00_16_Prefabs.md)

---

<!-- seealso -->
## 🔗 См. также

- [04.01 — GameManager](04_01_GameManager.md)
- [23.02 — SaveSystem](23_02_SaveSystem.md)

