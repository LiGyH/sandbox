# 01.01 — Глобальные пространства имён (Assembly)

## Что мы делаем?

Этот файл — отправная точка всего проекта. Он подключает **глобальные using-директивы**, чтобы каждый `.cs`-файл в проекте автоматически «видел» базовые библиотеки, и вам не приходилось писать `using System;` в каждом файле вручную.

Представьте это как «общий набор инструментов», который лежит на столе — любой файл в проекте может просто протянуть руку и взять нужный инструмент, не спрашивая разрешения.

## Как это работает внутри движка?

Ключевое слово `global using` появилось в C# 10. Когда компилятор видит `global using`, он **автоматически добавляет** это пространство имён ко всем файлам в проекте.

- **System** — базовые типы: `string`, `int`, `Console`, `Math`, исключения и т.д.
- **Sandbox** — главное пространство имён движка s&box. Содержит `GameObject`, `Component`, `Game` и прочее.
- **System.Linq** — LINQ-запросы: `.Where()`, `.Select()`, `.FirstOrDefault()` и другие методы для работы с коллекциями.
- **System.Threading.Tasks** — асинхронность: `Task`, `async`/`await`.
- **System.Collections.Generic** — коллекции: `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>`.
- **Sandbox.Diagnostics** — инструменты для логирования и отладки внутри движка.

## Создай файл

Путь: `Code/Assembly.cs`

```csharp
global using System;
global using Sandbox;
global using System.Linq;
global using System.Threading.Tasks;
global using System.Collections.Generic;
global using Sandbox.Diagnostics;
```

## Разбор кода

| Строка | Что делает |
|---|---|
| `global using System;` | Подключает базовые типы C# (`string`, `int`, `Exception` и т.д.) глобально — для всех файлов. |
| `global using Sandbox;` | Подключает ядро движка s&box (`GameObject`, `Component`, `Game`) ко всем файлам. |
| `global using System.Linq;` | Добавляет LINQ-методы (`.Where()`, `.Select()`, `.Any()`) для удобной работы с коллекциями. |
| `global using System.Threading.Tasks;` | Позволяет использовать `async` / `await` и `Task` без дополнительного `using`. |
| `global using System.Collections.Generic;` | Подключает обобщённые коллекции: `List<T>`, `Dictionary<TKey,TValue>`. |
| `global using Sandbox.Diagnostics;` | Подключает диагностические инструменты движка: `Assert`, логирование. |

### Почему именно `global`?

Без `global` вам пришлось бы в **каждом** файле писать:

```csharp
using System;
using Sandbox;
using System.Linq;
// ...
```

С `global using` вы пишете это **один раз** — и забываете. Это уменьшает дублирование и делает код чище.

## Результат

После создания этого файла:
- Все файлы проекта автоматически имеют доступ к базовым типам C#, движку s&box, LINQ, коллекциям и асинхронности.
- Вам не нужно добавлять `using`-директивы в каждый новый файл.
- Проект готов к написанию игровой логики.

---

Следующий шаг: [01.02 — Дополнения к движку (EngineAdditions)](01_02_EngineAdditions.md)
