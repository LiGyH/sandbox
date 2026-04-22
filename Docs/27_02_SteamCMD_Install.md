# 27.02 — Установка через SteamCMD и первый запуск

## Что мы делаем?

Ставим **s&box Dedicated Server** на отдельную машину (или хотя бы на отдельный процесс на своей же): через SteamCMD скачиваем сервер, делаем `.bat`/`.sh` скрипт запуска и убеждаемся, что в консоли пошли строчки про загрузку сцены.

> 📚 Источник: [Facepunch/sbox-docs → dedicated-servers/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/dedicated-servers/index.md). SteamDB-страница приложения сервера: [steamdb.info/app/1892930](https://steamdb.info/app/1892930/info/).

## Шаг 1. Поставить SteamCMD

SteamCMD — официальный CLI-клиент Steam для скачивания серверных приложений.

- Документация: <https://developer.valvesoftware.com/wiki/SteamCMD>.
- Под Windows — скачать ZIP, распаковать в, например, `C:\steamcmd\`.
- Под Linux — `sudo apt install steamcmd` (Ubuntu) или ручная установка из tar-архива.

Проверь, что работает:

```bat
cd C:\steamcmd
steamcmd.exe +quit
```

Первый запуск скачает обновления самого SteamCMD — это нормально.

## Шаг 2. Скачать s&box Dedicated Server

App ID s&box Dedicated Server — **`1892930`**. Скачиваем анонимно (логин в Steam не нужен):

**Windows:**

```bat
cd C:\steamcmd
steamcmd.exe +login anonymous +app_update 1892930 validate +quit
```

**Linux:**

```bash
steamcmd +login anonymous +app_update 1892930 validate +quit
```

После установки сервер ляжет в `C:\steamcmd\steamapps\common\Dedicated Server\` (Windows) или `~/.steam/steamapps/common/Dedicated Server/` (Linux). Внутри — `sbox-server.exe` (или `sbox-server` под Linux).

> 💡 Ту же команду используют для **обновления** сервера. Раз в неделю-две перезапускай SteamCMD с `+app_update 1892930 validate`, чтобы подхватить патчи. См. [27.10](27_10_Hosting_Operations.md).

### Staging-ветка

Если хочешь попробовать предрелизные сборки сервера:

```bat
steamcmd.exe +login anonymous +app_update 1892930 -beta staging validate +quit
```

Внимание: на staging-сервер смогут зайти **только** клиенты на staging-ветке s&box — это не для публики.

## Шаг 3. Создать `.bat`/`.sh` для запуска

В папке с сервером создай файл `Run-Server.bat` (Windows) или `run-server.sh` (Linux):

**Windows — `Run-Server.bat`:**

```bat
@echo off
sbox-server.exe ^
  +game facepunch.walker garry.scenemap ^
  +hostname "Мой первый Dedicated"
pause
```

**Linux — `run-server.sh`:**

```bash
#!/usr/bin/env bash
./sbox-server \
  +game facepunch.walker garry.scenemap \
  +hostname "Мой первый Dedicated"
```

`chmod +x run-server.sh` — и запускай.

Что мы передали:

| Параметр | Значение |
|---|---|
| `+game` | `<game_ident> [map_ident]` — ident игры из мастерской и (опционально) карты. `facepunch.walker` — официальный «эталонный» проект, на нём проще всего проверить, что сервер живой. |
| `+hostname` | как сервер будет называться в браузере серверов |

Полный список параметров — в [27.03](27_03_Server_Configuration.md).

## Шаг 4. Запустить и понять, что всё ок

Запускаешь `.bat`. В консоли должно появиться примерно следующее (формат меняется от версии к версии, но смысл такой):

```
Steamworks initialized
Mounting facepunch.walker ...
Loaded map garry.scenemap
Server is now ready and listening on port 27015
```

Признаки «всё хорошо»:

- ✅ Нет красных `Error: ...` в первые 10 секунд.
- ✅ Появилась строка про `listening on port`.
- ✅ Процесс `sbox-server.exe` стабильно держит ~80–200 МБ ОЗУ и < 5% CPU «в простое».

Признаки «что-то не так»:

- ❌ `Failed to load game package` — нет интернета или ident игры написан с опечаткой.
- ❌ `Port 27015 already in use` — уже запущен другой сервер. См. `+port` в [27.03](27_03_Server_Configuration.md).
- ❌ Окно мгновенно закрывается — добавь `pause` в конец `.bat`, чтобы увидеть ошибку.

## Шаг 5. Подключиться клиентом

Запусти s&box у себя на ПК. Открой консоль (`~`) и введи:

```
connect <ip>:<port>
```

Например, `connect 127.0.0.1:27015` если сервер на этом же ПК. Сервер должен в консоли написать `<ник> connecting...` → `<ник> connected`.

Если сервер на другой машине в локалке — `connect 192.168.x.y:27015`. Если в интернете — нужен публичный IP/DNS и проброшенный порт UDP 27015 (см. [27.10](27_10_Hosting_Operations.md)).

## Структура папки сервера (важно!)

```
Dedicated Server/
├── sbox-server.exe
├── steam_appid.txt
├── steamapps/                  ← скачанные пакеты игр и карт (кеш)
├── users/
│   └── config.json             ← права доступа (см. 27.05)
├── logs/                       ← логи запусков
└── ...
```

- `steamapps/` — всё, что сервер «примонтировал» из мастерской. Не трогай руками.
- `users/config.json` — список админов и их claim-ов; разберём в [27.05](27_05_User_Permissions.md).
- `logs/` — куда писать обращения, если что-то падает.

## Что **нельзя** забывать

- ⚠️ **Не запускай две копии сервера на одном порте.** `+port 27016` для второго.
- ⚠️ **Не клади `.sbproj` своей игры в папку сервера** «чтобы он подхватил». Передавай путь к `.sbproj` через `+game c:/path/to/your.sbproj` — см. [27.04](27_04_Local_Project_Serverside_Code.md).
- ⚠️ **Под Linux сервер требует glibc актуальной версии.** На совсем старых дистрибутивах (Ubuntu 18.04) могут быть проблемы.
- ⚠️ **Не запускай сервер от root.** Создай отдельного пользователя `sbox` под Linux и `services`-аккаунт под Windows.

## Что важно запомнить

- App ID s&box Dedicated Server — **1892930**.
- Команда установки/обновления: `steamcmd +login anonymous +app_update 1892930 validate +quit`.
- Минимальный запуск: `sbox-server.exe +game <ident> [map] +hostname "..."`.
- На опубликованной игре сервер просто крутит код из мастерской; на **локальном проекте** — твой код с диска (см. [27.04](27_04_Local_Project_Serverside_Code.md)).
- Подключение клиента — через консоль `connect <ip>:<port>`.

## Что дальше?

В [27.03](27_03_Server_Configuration.md) — детально разбираем все ключи запуска (`+port`, `+net_query_port`, `+net_game_server_token`, `+hostname` и пр.) и как через ConVar/ConCmd донастроить сервер на лету.
