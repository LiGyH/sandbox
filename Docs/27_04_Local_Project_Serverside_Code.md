# 27.04 — Локальный проект на сервере и Serverside Code

## Что мы делаем?

Узнаём про **самый важный режим dedicated-сервера** — запуск с **локальным `.sbproj`**. Это даёт две суперсилы:

1. Можно тестировать игру **без публикации в мастерскую**.
2. Можно писать **настоящий серверный код** через `#if SERVER` и `*.Server.cs`, который **никогда не попадёт клиенту**.

> 📚 Источники: [running-local-projects.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/dedicated-servers/running-local-projects.md), [serverside-code.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/dedicated-servers/serverside-code.md).

## Запуск локального проекта

Вместо ident-а из мастерской — **путь к `.sbproj`**:

```bat
sbox-server.exe +game C:/git/sbox-mygame/mygame.sbproj
```

Что дальше делает движок:

1. Читает `.sbproj`, компилирует **код прямо на сервере**.
2. Поднимает сцену из проекта.
3. Когда подключается клиент:
   - Ассеты, которых у клиента нет, **сервер шлёт сам** (модели, материалы, звуки).
   - Код тоже шлётся клиенту → клиент **на лету подгружает и хотлоадит** его (см. [00.17](00_17_Hotloading.md)).

То есть клиенту даже **не нужно** подписываться в мастерской. Это прорывная штука: ты правишь код у себя в IDE → пересобираешь → подключённые клиенты видят изменения **без переустановки**.

> ⚠️ **Это единственный способ иметь серверный код, скрытый от клиента.** Для опубликованных игр Facepunch вырезает `#if SERVER` при публикации; ради «настоящего серверного» — поднимай свой dedicated с локальным проектом.

## `#if SERVER` — препроцессорный блок

```csharp
protected override void OnUpdate()
{
#if SERVER
    Log.Info( "Это серверный update" );
#else
    Log.Info( "Это клиентский update" );
#endif
}
```

Поведение:

| Где собирается | Что попадает в DLL |
|---|---|
| Клиент (запуск игры из мастерской) | только ветка `#else` |
| Dedicated с локальным проектом | только ветка `#if SERVER` |
| Опубликованная игра в мастерскую | **всё `#if SERVER` вырезано** на этапе публикации |

Поэтому в `#if SERVER` можно класть всё, что **никогда** не должно увидеть клиент:

- API-ключи к внешним сервисам.
- SQL-запросы к базе.
- Логику начисления валюты, выдачи лута.
- Античит-проверки (чем меньше клиент знает о них — тем лучше).
- Сверку покупок Steam, валидация инвентаря.

## `*.Server.cs` — синтаксический сахар

Вместо того чтобы заворачивать **весь файл** в `#if SERVER ... #endif`:

```csharp
// Player.cs           — общее поведение
public partial class Player : Component { ... }

// Player.Server.cs    — серверная часть, целиком вырезается из клиента
public partial class Player
{
    public async Task LoadFromDatabaseAsync()
    {
        var json = await Http.RequestStringAsync( $"https://api.mygame.com/players/{SteamId}" );
        ApplyState( JsonSerializer.Deserialize<PlayerSaveState>( json ) );
    }
}
```

Любой `*.Server.cs` файл движок **сам** оборачивает в `#if SERVER`. Это компактнее и снимает риск забыть `#endif`. Используй имена `*.Server.cs` для **всех** файлов с серверной-only логикой — это сразу видно по имени в IDE.

## Парный приём: `#if !SERVER` для UI

Аналогично можно скрыть UI-код от сервера, чтобы dedicated не тратил время на парсинг Razor:

```csharp
public partial class Player
{
#if !SERVER
    private MyHud _hud;
    protected override void OnStart() => _hud = ...;
#endif
}
```

Или через парный файл `Player.Client.cs`, обёрнутый в `#if !SERVER` (этот сахар нужно делать руками — стандартного `*.Client.cs` нет).

## Типовая разбивка проекта по уровням

| Файл | Что внутри | Где живёт |
|---|---|---|
| `Player.cs` | Общая логика, `[Sync]`, `[Property]` | везде |
| `Player.Server.cs` | Загрузка из БД, начисление валюты | только сервер |
| `Player.Client.cs` (или `#if !SERVER`) | HUD, эффекты, ввод | только клиент |
| `EconomyService.Server.cs` | Цены, баланс, транзакции | только сервер |
| `EconomyApi.cs` | DTO/контракты, общие модели | везде |

## Когда использовать `#if SERVER`, а когда — `Game.IsServer` / `Networking.IsHost`?

| Случай | Что писать |
|---|---|
| Код **есть и там, и там**, но ветвится по контексту | `if ( Networking.IsHost ) ...` (см. [00.20](00_20_Networking_Basics.md)) |
| Код, который **не должен попасть** в клиент по соображениям безопасности | `#if SERVER` или `*.Server.cs` |
| Код, который **физически не работает** на сервере (Razor, камера) | `#if !SERVER` или проверка `if ( Game.IsServer ) return;` |

Правило большого пальца: **`#if` — про *компиляцию* (что попадает в бинарник)**, **`if (Networking.IsHost)` — про *выполнение*** (что выполняется в рантайме).

## Hotload и локальный проект

Когда ты пересохраняешь `.cs` в IDE:

1. Сервер пересобирает сборку.
2. На сервере срабатывает hotload (см. [00.17](00_17_Hotloading.md)).
3. Сервер шлёт обновлённую сборку всем подключённым клиентам.
4. У клиентов тоже hotload.

⚠️ Hotload **сохраняет** только сериализуемое состояние компонентов. Несериализуемые поля, открытые сокеты, фоновые `Task` — обнулятся. Учитывай это в серверных сервисах: храни состояние в полях с `[Property]` или в постоянном хранилище (см. [27.10](27_10_Hosting_Operations.md)).

## Что **нельзя** делать в `#if SERVER`

- ❌ Трогать UI (`Sandbox.UI`, Razor) — на сервере его нет, упадёт.
- ❌ Использовать `Camera` / ввод (`Input`).
- ❌ Запускать звуки локально через `Sound.Play` — некому слушать. Шли клиентам через `[Rpc.Broadcast]`.
- ❌ Делать API-запросы синхронно — блокируешь весь tick. Только `await Http.RequestStringAsync` (см. [26.12](26_12_Http_Requests.md)).

## Пример: безопасное начисление валюты

```csharp
public partial class Player
{
    [Sync] public int Coins { get; set; }
}

// Player.Server.cs
public partial class Player
{
    public async Task GrantCoinsAsync( int amount, string reason )
    {
        // Логируем сервером
        Log.Info( $"[Economy] +{amount} {Name} ({reason})" );

        // Сначала валидируем серверно
        if ( amount <= 0 || amount > 10_000 ) return;

        // Затем синхронно меняем — клиенты увидят через [Sync]
        Coins += amount;

        // И асинхронно фиксируем в БД, чтобы не потерять при рестарте
        await Database.SavePlayerCoinsAsync( SteamId, Coins );
    }
}
```

Клиент не может вызвать `GrantCoinsAsync` напрямую — этого метода в его сборке **физически нет**. Самое крепкое разделение, которое существует.

## Что важно запомнить

- `+game path/to/.sbproj` — единственный способ запустить **свой** код на dedicated до публикации.
- Сервер сам шлёт код и недостающие ассеты клиентам, hotload работает обоюдно.
- `#if SERVER` и `*.Server.cs` — компилируются **только** на dedicated с локальным проектом.
- В опубликованной игре `#if SERVER`-код **вырезается** Facepunch — это by design.
- Для логики, не имеющей права утечь к клиенту (БД, валюта, античит) — обязательно `#if SERVER`/`*.Server.cs`.

## Что дальше?

В [27.05](27_05_User_Permissions.md) — права администраторов, файл `users/config.json`, claims и `Connection.HasPermission`.
