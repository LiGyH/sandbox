# 26.04 — События физики: OnCollisionStart/Update/Stop

## Что мы делаем?

Раньше мы спрашивали мир активно (трассировкой). Теперь — **пассивно**: движок сам сообщает компоненту, что произошло столкновение. Это нужно везде, где «в момент удара случается что-то»: ящик ломается, бочка взрывается, мина детонирует, кнопка нажимается ногой.

## Три события Collider

Любой компонент, расположенный на том же `GameObject`, что и `Collider`, может реализовать методы:

```csharp
public class Bumper : Component, Component.ICollisionListener
{
    public void OnCollisionStart( Collision other )  { /* удар начался */ }
    public void OnCollisionUpdate( Collision other ) { /* контакт продолжается */ }
    public void OnCollisionStop( Collision other )   { /* контакт прекратился */ }
}
```

| Метод | Когда вызывается |
|---|---|
| `OnCollisionStart` | в кадре, когда два тела впервые коснулись |
| `OnCollisionUpdate` | каждый физический шаг, пока контакт продолжается |
| `OnCollisionStop` | когда тела разъехались |

> Реализовать нужно **интерфейс `Component.ICollisionListener`**. Без этого интерфейса методы не вызовутся, даже если по имени совпадают.

## Что лежит в `Collision`?

| Поле | Что значит |
|---|---|
| `Collision.Other.GameObject` | объект, с которым столкнулись |
| `Collision.Other.Body` | его `PhysicsBody` |
| `Collision.Other.Component` | его `Collider` |
| `Collision.Self.*` | то же самое, но про **наш** объект |
| `Collision.Contact.Point` | точка контакта в мире |
| `Collision.Contact.Normal` | нормаль (от другого к нам) |
| `Collision.Contact.Speed` | относительная скорость удара |
| `Collision.Contact.Impulse` | импульс удара |

## Пример: «бочка взрывается при сильном ударе»

```csharp
public class ExplosiveBarrel : Component, Component.ICollisionListener
{
    [Property] public float MinImpactSpeed { get; set; } = 600f;

    public void OnCollisionStart( Collision other )
    {
        if ( other.Contact.Speed.Length < MinImpactSpeed )
            return;

        // Достаточно сильный удар — бабах
        Sound.Play( "explosion.large", WorldPosition );
        Effects.Explosion( WorldPosition, 256f );
        GameObject.Destroy();
    }
}
```

## Триггеры — отдельная история

Если у `Collider` включён флаг **IsTrigger**, обычные `OnCollision*`-события **не вызываются**. Вместо них приходят `OnTriggerEnter` / `OnTriggerExit`. Им посвящён [26.05](26_05_Triggers.md).

## Особенность: события приходят на ВСЕХ участников

Если бочка А ударила бочку Б, событие приходит и на компоненты А, и на компоненты Б. В обоих случаях `Collision.Other` указывает на «другую сторону». Это удобно: достаточно реализовать логику только на одном из объектов.

## `IScenePhysicsEvents` — обёртка над физическим шагом

Это **глобальное** событие, не привязанное к конкретному `Collider`. Подписывается через `GameObjectSystem` или компонент:

```csharp
public class MyPhysicsHook : Component, Sandbox.IScenePhysicsEvents
{
    public void PrePhysicsStep()  { /* перед физическим шагом */ }
    public void PostPhysicsStep() { /* после физического шага */ }
}
```

Зачем это нужно?

- **PrePhysicsStep** — поправить силы/скорости *до* того, как движок их применит (например, толчок гравитационной пушки).
- **PostPhysicsStep** — синхронизировать что-то с уже посчитанной позицией (например, привязать к телу луч-указатель).

Эти методы вызываются **с фиксированной частотой** физики (обычно 60 Гц), не на каждый рендер-кадр. Не путай с `OnUpdate` (рендер) и `OnFixedUpdate` (логика, тоже фиксированная).

## Производительность

- Не делай в `OnCollisionUpdate` дорогих операций (трассировки, спавн префабов) — он зовётся **каждый шаг физики**, пока контакт жив.
- Тяжёлую логику (звук удара, эффекты) кидай в `OnCollisionStart`.
- Если объект рассыпается, не забудь `GameObject.Destroy()` — иначе события продолжат приходить.

## Что важно запомнить

- Реализуй интерфейс **`Component.ICollisionListener`**, иначе события не придут.
- `OnCollisionStart` — раз в начале касания, `OnCollisionUpdate` — каждый шаг физики, `OnCollisionStop` — в конце.
- Информация о ударе — в `Collision.Contact` (точка, нормаль, скорость, импульс).
- Триггеры — отдельные события, см. [26.05](26_05_Triggers.md).
- `IScenePhysicsEvents` — для логики, привязанной к шагу физики, а не к конкретному коллайдеру.

## Что дальше?

В [26.05](26_05_Triggers.md) разберём **триггеры** — невидимые зоны, в которые можно «войти/выйти».
