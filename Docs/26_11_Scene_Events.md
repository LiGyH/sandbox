# 26.11 — Сценовые события: ISceneStartup, IGameObjectNetworkEvents

## Что мы делаем?

Помимо обычного жизненного цикла компонента (`OnAwake`/`OnStart`/`OnEnabled`/`OnUpdate`), есть **сценовые события** — глобальные точки входа в жизнь сцены и сетевых объектов. Этот этап перечисляет важные интерфейсы из официальной документации и объясняет, *когда* они срабатывают.

## `ISceneStartup` — старт сцены

Реализуется на `Component` или `GameObjectSystem` и даёт три точки:

```csharp
public interface ISceneStartup : ISceneEvent<ISceneStartup>
{
    void OnHostPreInitialize( SceneFile scene );  // до загрузки сцены, на хосте
    void OnHostInitialize();                      // сцена загружена, только хост
    void OnClientInitialize();                    // сцена загружена, хост и клиент
}
```

| Событие | Когда вызывается | Где использовать |
|---|---|---|
| `OnHostPreInitialize` | до того, как объекты сцены созданы. Видят только `GameObjectSystem`'ы, потому что `Component`'ов ещё нет. | подготовка сетевого окружения, регистрация фабрик |
| `OnHostInitialize` | сцена загружена на хосте. | спавн «общих» объектов, например, общей камеры или менеджера правил |
| `OnClientInitialize` | сцена загружена и у хоста, и у клиента (но **не** на dedicated server). | спавн локальных HUD/эффектов |

> 🧭 «Хост» в s&box — это компьютер, отвечающий за состояние игры: одиночный игрок, лобби-хост, dedicated server. Все остальные — «клиенты».

### Пример: при загрузке сцены создать лобби

```csharp
public sealed class GameStartup : GameObjectSystem<GameStartup>, ISceneStartup
{
    public GameStartup( Scene scene ) : base( scene ) { }

    void ISceneStartup.OnHostInitialize()
    {
        if ( !Networking.IsActive )
            Networking.CreateLobby();
    }
}
```

## `IGameObjectNetworkEvents` — изменения сетевого владельца

Этот интерфейс реализуется на `Component`-ах конкретного объекта и срабатывает при смене владельца:

```csharp
public interface IGameObjectNetworkEvents : ISceneEvent<IGameObjectNetworkEvents>
{
    void NetworkOwnerChanged( Connection newOwner, Connection previousOwner );
    void StartControl();   // мы стали владельцем (перестали быть прокси)
    void StopControl();    // мы перестали быть владельцем (стали прокси)
}
```

| Событие | Когда |
|---|---|
| `NetworkOwnerChanged` | сменился `Network.Owner` объекта |
| `StartControl` | этот клиент теперь **управляет** объектом (не прокси) |
| `StopControl` | этот клиент стал **прокси** (управляет кто-то другой) |

Идеальное место для:

- включить/отключить локальный input (камеру, мышь);
- показать/скрыть viewmodel;
- начать/остановить фоновую логику (подписки, периодические таски).

## `IScenePhysicsEvents` — обёртка вокруг физического шага

Уже упомянутый в [26.04](26_04_Physics_Events.md):

```csharp
public interface IScenePhysicsEvents : ISceneEvent<IScenePhysicsEvents>
{
    void PrePhysicsStep();
    void PostPhysicsStep();
}
```

`PrePhysicsStep` идеален для применения сил (гравитационная пушка, толчки), `PostPhysicsStep` — для синхронизации невизуальных систем с уже посчитанной физикой.

## `Component`-интерфейсы для коллизий и триггеров

Уже встречались, но напомним для полноты:

- **`Component.ICollisionListener`** → `OnCollisionStart/Update/Stop` ([26.04](26_04_Physics_Events.md))
- **`Component.ITriggerListener`** → `OnTriggerEnter/Exit` ([26.05](26_05_Triggers.md))
- **`Component.INetworkListener`** → события подключения/отключения игроков ([00.25](00_25_Network_Events.md))
- **`Component.INetworkVisible`** → решает видимость объекта для конкретного игрока ([26.14](26_14_Network_Visibility.md))

## Шаблон `ISceneEvent<T>` — что это вообще?

Это «чистая» система событий sceneside. Любой компонент или система, реализующий интерфейс `ISceneEvent<X>`, **автоматически подписан** на событие — без `+=` и регистраций. Когда движок раскидывает событие, он перебирает все живые `ISceneEvent<X>`-объекты сцены.

Свой кастомный сценовый ивент сделать тоже легко:

```csharp
public interface IMyDayCycleEvents : ISceneEvent<IMyDayCycleEvents>
{
    void OnSunrise();
    void OnSunset();
}

// Где-то в логике:
Scene.RunEvent<IMyDayCycleEvents>( x => x.OnSunrise() );
```

Любой компонент, реализующий `IMyDayCycleEvents`, получит вызов.

## Что НЕ делать в `OnHostPreInitialize`

- ❌ Не пытайся искать компоненты в сцене — её ещё нет.
- ❌ Не спавни через `prefab.Clone()` — некуда.
- ✅ Регистрируй колбэки/фабрики, изменяй сетевые настройки, читай `SceneFile`.

## Что важно запомнить

- `ISceneStartup` — три точки входа в загрузку сцены (host pre/init/client init).
- `IGameObjectNetworkEvents` — реакция на смену владельца сетевого объекта.
- `IScenePhysicsEvents` — хуки до/после физического шага.
- `ISceneEvent<T>` — самоподписка через интерфейс, без явной регистрации.
- Свои сценовые события создаются через `interface IMyEvents : ISceneEvent<IMyEvents>` + `Scene.RunEvent<IMyEvents>(...)`.

## Что дальше?

В [26.12](26_12_Http_Requests.md) — HTTP-запросы из игры (для лидербордов, статистики, кастомных API).
