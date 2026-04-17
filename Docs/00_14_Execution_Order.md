# 00.14 — Порядок выполнения (Execution Order)

## Что мы делаем?

Разбираем, **в каком порядке движок вызывает методы** компонентов в течение одного кадра. Понимание этого спасает от жутких багов класса «иногда работает, иногда нет».

## Порядок событий в одном кадре

Упрощённо, каждый кадр движок делает такое:

```
┌─────────────────────────────────────────────┐
│  1. Events / Input                          │
├─────────────────────────────────────────────┤
│  2. OnFixedUpdate (может быть 0+ раз)      │
│     → физический шаг                        │
├─────────────────────────────────────────────┤
│  3. OnUpdate — все компоненты               │
├─────────────────────────────────────────────┤
│  4. Анимации, bone transforms               │
├─────────────────────────────────────────────┤
│  5. OnPreRender — все компоненты            │
├─────────────────────────────────────────────┤
│  6. Отрисовка кадра                         │
└─────────────────────────────────────────────┘
```

Новые `OnAwake`/`OnStart` вызываются **между шагами**, когда появляются новые компоненты.

## Почему `OnFixedUpdate` может вызваться несколько раз?

Физика тикает со **стабильной** частотой (например, 50 раз/сек), а кадры — с переменной (60-200 fps). Если кадр занял 40 мс, значит прошло ~2 тика физики — `OnFixedUpdate` вызовется **дважды подряд**.

**Следствие:**
- Любое «движение, которое должно быть стабильным» — в `OnFixedUpdate`.
- Чтение инпута (мышь) — в `OnUpdate`, потому что мышь накапливает движение между кадрами.

## Порядок между разными компонентами — НЕ гарантирован

Самое важное предупреждение:

> ⚠️ **Ты НЕ знаешь, какой компонент сделает `OnUpdate` первым.**

Если у тебя `Enemy.OnUpdate` читает `Player.Position`, а `Player.OnUpdate` в этом же кадре эту `Position` меняет, — порядок может быть любой. В один кадр враг увидит старую позицию, в другой — новую.

### Как решать?

**Способ 1: `OnPreRender` для визуального «догоняющего»**

Движение — в `OnFixedUpdate`. Визуальные вещи, которые должны видеть финальные позиции — в `OnPreRender`.

**Способ 2: `GameObjectSystem`** (продвинутое)

Это отдельный механизм, который запускается в точно определённом месте кадра. См. [00.15 — Scene и GameObjectSystem](00_15_Scene_GameObjectSystem.md).

**Способ 3: явный порядок через события**

Твой компонент А вызывает метод В напрямую: `player.UpdateAim()`. Тогда порядок очевиден.

## Жизненный цикл нового GameObject

Когда ты делаешь `go.AddComponent<MyComp>()`, компонент проходит:

```
OnAwake    (1 раз)
    │
    ▼
OnEnabled  (если GameObject enabled)
    │
    ▼
OnStart    (1 раз, гарантированно ДО первого OnFixedUpdate)
    │
    ▼
OnUpdate / OnFixedUpdate / OnPreRender ...  (вечно)
    │
    ▼
OnDisabled (если выключили)
    │
    ▼
OnDestroy  (1 раз)
```

## Где ставить ссылки на другие компоненты?

**Не в `OnStart`, а в `OnAwake`** — потому что кто-то другой в своём `OnStart` уже может тебя спрашивать:

```csharp
public sealed class Weapon : Component
{
    [RequireComponent] public Rigidbody Body { get; set; }

    protected override void OnAwake()
    {
        // ссылки готовы здесь
    }

    protected override void OnStart()
    {
        // здесь можно полагаться, что все компоненты созданы
        // и другие могут уже вызывать твои методы
    }
}
```

## `OnValidate` — когда дизайнер крутит ползунки

Вызывается в **редакторе**, когда меняется любое `[Property]`. Используй, чтобы ограничить значения:

```csharp
protected override void OnValidate()
{
    MaxHealth = Math.Max( 1, MaxHealth );
    if ( MaxHealth < CurrentHealth )
        CurrentHealth = MaxHealth;
}
```

## `OnLoad` — тормозной loading screen

Если твой компонент делает что-то долгое при старте сцены (скачивает ассет, генерирует уровень), переопредели `OnLoad` — движок дождётся его завершения:

```csharp
protected override async Task OnLoad()
{
    LoadingScreen.Title = "Строим процедурный уровень...";
    await GenerateLevelAsync();
}
```

## Результат

После этого этапа ты знаешь:

- ✅ Порядок вызовов в одном кадре: FixedUpdate → Update → PreRender → Render.
- ✅ Что `OnFixedUpdate` может вызываться 0, 1 или несколько раз за кадр.
- ✅ Что **порядок между разными компонентами НЕ гарантирован**.
- ✅ Зачем `OnAwake` vs `OnStart` vs `OnEnabled`.
- ✅ Как `OnValidate` защищает от дурных значений в редакторе.

---

📚 **Facepunch docs:**
- [scene/components/execution-order.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/components/execution-order.md)
- [scene/components/component-methods.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/components/component-methods.md)

**Следующий шаг:** [00.15 — Scene и GameObjectSystem](00_15_Scene_GameObjectSystem.md)
