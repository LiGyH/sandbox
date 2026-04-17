# 18_03 — Effects UI: интерфейс управления эффектами пост-обработки

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём набор UI-компонентов для вкладки «Effects» в меню спавна. Это три Razor-компонента:

- **EffectsHost** — корневой контейнер, который размещает список и панель свойств рядом друг с другом.
- **EffectsList** — список всех доступных эффектов с вкладками «Installed» и «Workshop», чекбоксами включения, предпросмотром при наведении и системой пресетов.
- **EffectsProperties** — панель редактирования свойств выбранного эффекта через `ControlSheet`.

Вместе они дают игроку полный контроль над визуальными эффектами: просмотр, переключение, тонкая настройка параметров, сохранение/загрузка пресетов.

## Как это работает внутри движка

UI в s&box строится на Razor-компонентах (`.razor`-файлы), стилизуемых через SCSS (`.razor.scss`-файлы). Каждый компонент наследует `Panel` и описывает свою разметку в HTML-подобном синтаксисе. Стили используют переменные темы из `Theme.scss`.

Компоненты общаются через `PostProcessManager` (см. урок 18_02):
- `EffectsList` вызывает методы `Toggle()`, `Select()`, `Preview()`, `Unpreview()`.
- `EffectsProperties` читает `SelectedPath` и `GetSelectedComponents()` для отображения редактора.

Razor поддерживает `@code`-блоки с C#-логикой, привязки событий (`@onclick`, `@onmouseenter`), условный рендеринг (`@if`) и циклы (`@foreach`).

## Путь к файлам

- `Code/UI/Effects/EffectsHost.razor`
- `Code/UI/Effects/EffectsHost.razor.scss`
- `Code/UI/Effects/EffectsList.razor`
- `Code/UI/Effects/EffectsList.razor.scss`
- `Code/UI/Effects/EffectsProperties.razor`
- `Code/UI/Effects/EffectsProperties.razor.scss`

---


## Три части урока

Урок разбит на три под-этапа — по компоненту на файл:

1. **[18.03a — EffectsHost](18_03a_EffectsHost.md)** — корневой контейнер (список + панель рядом).
2. **[18.03b — EffectsList](18_03b_EffectsList.md)** — список эффектов, вкладки Installed/Workshop, пресеты.
3. **[18.03c — EffectsProperties](18_03c_EffectsProperties.md)** — редактирование свойств через ControlSheet.

Читай строго по порядку — каждая часть опирается на предыдущую.

---

## ➡️ Следующий шаг

Начни с **[18.03a — EffectsHost](18_03a_EffectsHost.md)**.
