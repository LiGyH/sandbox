# 25.01 — Editor/Assembly.cs (глобальные using для редактора)

## Что мы делаем?

Создаём файл `Editor/Assembly.cs` — он аналогичен `Code/Assembly.cs`, но работает **только для кода редактора**. Он подключает глобальные пространства имён, чтобы в файлах редактора не нужно было писать `using` вручную.

## Как это работает внутри движка

В s&box существуют **две сборки** (assembly):
1. **Game** (`Code/`) — код самой игры, выполняется на сервере и клиенте
2. **Editor** (`Editor/`) — код для инструментов редактора s&box, выполняется только в редакторе

Файл `Assembly.cs` в папке `Editor/` определяет `global using` директивы, которые автоматически подключаются ко **всем** `.cs` файлам в этой сборке.

## Путь к файлу

```
Editor/Assembly.cs
```

## Полный код

```csharp
global using Editor;
global using Sandbox;
global using Sandbox.UI;
global using System.Collections.Generic;
global using System.Linq;
```

## Разбор кода

| Строка | Что делает |
|--------|-----------|
| `global using Editor;` | Подключает пространство имён `Editor` — базовые классы и утилиты редактора s&box |
| `global using Sandbox;` | Подключает основное пространство имён движка — `GameObject`, `Component`, `Vector3` и т.д. |
| `global using Sandbox.UI;` | Подключает UI-систему — `Panel`, `Label` и другие элементы интерфейса |
| `global using System.Collections.Generic;` | Подключает коллекции C# — `List<T>`, `Dictionary<K,V>` и т.д. |
| `global using System.Linq;` | Подключает LINQ — `.Where()`, `.Select()`, `.FirstOrDefault()` и другие методы запросов |

## Что проверить

1. Файл `Editor/Assembly.cs` существует
2. Если в будущем вы создадите файлы в папке `Editor/`, они смогут использовать классы из `Editor`, `Sandbox` и `Sandbox.UI` без дополнительных `using`
