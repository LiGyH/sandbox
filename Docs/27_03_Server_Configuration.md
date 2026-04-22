# 27.03 — Конфигурация сервера: командная строка, ConVar, ConCmd

## Что мы делаем?

Разбираем **все способы** настроить Dedicated Server: параметры командной строки (`+game`, `+port`, …), консольные команды и переменные (`ConVar`/`ConCmd` — см. [00.18](00_18_ConVar_ConCmd.md)), а заодно — где хранить пароль, токен и порты, чтобы не светить их в `.bat`.

> 📚 Источник: [Facepunch/sbox-docs → dedicated-servers/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/dedicated-servers/index.md), раздел **Configuration**.

## Главная идея

> **Любой `+что_то значение` в командной строке — это ConVar/ConCmd.** То есть `+hostname "X"` эквивалентно тому, что после старта ты бы вручную написал `hostname X` в консоли сервера. Эту же команду можно отдать клиентом по RCON-подобному каналу позже.

Это значит, что:

- Любой свой ConVar (см. [00.18](00_18_ConVar_ConCmd.md)) можно задать с командной строки.
- Любая своя ConCmd (например, `loadmap`) запускается тем же `+`.

## Стандартные ключи запуска

Из официальной документации Facepunch:

| Ключ | Аргументы | Что делает |
|---|---|---|
| `+game` | `<packageIdent>` `[mapPackageIdent]` | Какую игру (и опционально стартовую карту) загрузить. Можно передать **путь к локальному `.sbproj`** — см. [27.04](27_04_Local_Project_Serverside_Code.md). |
| `+hostname` | `<имя>` | Заголовок сервера в браузере. Ставь в кавычки, если есть пробелы. |
| `+port` | `<число>` | UDP-порт, на котором сервер принимает игроков. По умолчанию 27015. |
| `+net_query_port` | `<число>` | Порт для запроса метаданных (имя, карта, игроки) браузером серверов. По умолчанию 27016. |
| `+net_game_server_token` | `<токен>` | Game Server Login Token (GSLT) — фиксирует Steam ID сервера, чтобы он не менялся при каждом запуске. Получить: <https://steamcommunity.com/dev/managegameservers>. **Опционально, доступно только после релиза s&box.** |

## Минимальный шаблон запуска

```bat
@echo off
sbox-server.exe ^
  +game your.gameident your.mapident ^
  +hostname "RP Server #1 [RU]" ^
  +port 27015 ^
  +net_query_port 27016 ^
  +sv_password ""
```

Каждый `^` — продолжение строки в Windows-bat. Под Linux — обратная косая `\`.

## Свои ConVar и ConCmd

Любую свою настройку оформляй как **`ConVar.Server`**:

```csharp
public static class ServerSettings
{
    [ConVar( "rp_max_props", Help = "Сколько пропов разрешено каждому игроку" )]
    public static int MaxPropsPerPlayer { get; set; } = 50;

    [ConVar( "rp_economy_enabled", Help = "Включена ли экономика" )]
    public static bool EconomyEnabled { get; set; } = true;
}
```

В `.bat`:

```bat
sbox-server.exe ... +rp_max_props 100 +rp_economy_enabled 1
```

Команды-действия (например, ребут карты по таймеру) — через `ConCmd.Server`:

```csharp
[ConCmd.Server( "rp_changemap" )]
public static void ChangeMap( string mapIdent )
{
    Game.ChangeMap( mapIdent );
}
```

> ⚠️ Помни: `[ConCmd.Server]` **может** вызвать клиент, если у него есть права. Без проверок это дыра. См. [27.05](27_05_User_Permissions.md) — `Connection.HasPermission`.

## Несколько серверов на одной машине

Каждому экземпляру нужен **свой набор портов**:

```bat
sbox-server.exe +game ... +hostname "Server #1" +port 27015 +net_query_port 27016
sbox-server.exe +game ... +hostname "Server #2" +port 27025 +net_query_port 27026
sbox-server.exe +game ... +hostname "Server #3" +port 27035 +net_query_port 27036
```

Шаг между серверами берём 10 — с запасом, чтобы не пересеклись.

## Хранение секретов: токен, пароли

❌ **Не клади** GSLT-токен и пароли в публичные `.bat` (особенно если кладёшь репозиторий с конфигами в Git).

Лучше:

- **Windows.** Заведи переменную окружения через `setx SBOX_TOKEN "..."` (один раз) и читай `%SBOX_TOKEN%`:

  ```bat
  sbox-server.exe ... +net_game_server_token %SBOX_TOKEN%
  ```

- **Linux.** Положи `export SBOX_TOKEN=...` в `/etc/sbox.env`, подключай через `EnvironmentFile=` в systemd-юните (см. [27.10](27_10_Hosting_Operations.md)).

- **Любая ОС.** Свой `secrets.cfg`, который не попадает в Git, и подгружай его командой `exec secrets` через `+exec secrets` (если игра реализует такой ConCmd — стандартный его пока нет, делай сам).

## Динамическая конфигурация после старта

Подключись клиентом-админом и набери в консоли:

```
hostname "Новое имя"
rp_max_props 200
rp_changemap your.othermap
```

Чтобы ConVar **переслать** клиентам (например, лимиты), помечай его флагом `Replicated`:

```csharp
[ConVar( "rp_max_props", Saved = true, Replicated = true )]
public static int MaxPropsPerPlayer { get; set; } = 50;
```

Тогда `Saved` сохранит значение между перезапусками, а `Replicated` — клиенты увидят актуальное значение и подстроят UI.

## Конфиг-файлы вместо длинных `.bat`

Когда параметров десятки, выноси их в `.cfg` и грузи через ConCmd `exec`:

`server.cfg` (в папке игры):

```
hostname "RP Server #1 [RU]"
sv_password ""
rp_max_props 100
rp_economy_enabled 1
```

В `.bat` — короче:

```bat
sbox-server.exe +game your.gameident +exec server
```

(Конкретное имя ConCmd для exec может отличаться по версии — проверь в консоли).

## Логи и трассировка

В рабочем сервере **обязательно** логируй критичные события через стандартный `Log` (см. [00.30](00_30_Log_Assert.md)):

```csharp
[ConCmd.Server( "rp_changemap" )]
public static void ChangeMap( string mapIdent )
{
    Log.Info( $"[Admin] {Rpc.Caller?.DisplayName} меняет карту на {mapIdent}" );
    Game.ChangeMap( mapIdent );
}
```

Под Linux запуск через systemd сам соберёт всё в `journalctl -u sbox-server` — никаких файлов руками вести не надо.

## Чек-лист «здорового» запуска

- ✅ Уникальные `+port` и `+net_query_port` для каждого экземпляра.
- ✅ `+hostname` с регионом и языком (`[RU]`, `[EU]` — людям удобнее).
- ✅ Секреты в переменных окружения, **не** в `.bat`-файлах в Git.
- ✅ Все админ-ConCmd проверяют `Connection.HasPermission` (см. [27.05](27_05_User_Permissions.md)).
- ✅ Все ConVar с `Replicated = true`, если их видит UI клиента.
- ✅ Логи всех админ-действий в `Log.Info`.

## Что важно запомнить

- Любая ConVar/ConCmd ставится через `+имя значение` в командной строке.
- Пять системных ключей: `+game`, `+hostname`, `+port`, `+net_query_port`, `+net_game_server_token`.
- Секреты — через переменные окружения, не через bat-файлы.
- Длинные конфиги — в `.cfg` + `+exec`.
- Любая своя серверная команда обязана проверять права (см. [27.05](27_05_User_Permissions.md)).

## Что дальше?

В [27.04](27_04_Local_Project_Serverside_Code.md) — самый «вкусный» режим: **локальный проект на сервере** и **серверный код** через `#if SERVER` / `*.Server.cs`.
