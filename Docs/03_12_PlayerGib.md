# 03.12 — Гибы игрока (PlayerGib) 💥

## Что мы делаем?

Создаём компонент **PlayerGib** — кусочек тела, который отлетает при взрывной смерти. Вместо рэгдолла (целого тела) игрок «разлетается» на несколько частей: голова, руки, ноги, торс.

## Как это работает?

### Структура префаба

На объекте игрока есть несколько дочерних объектов с компонентом `PlayerGib`:
```
Player
  ├── Gib_Head    (PlayerGib + Rigidbody)
  ├── Gib_Torso   (PlayerGib + Rigidbody)
  ├── Gib_LeftArm (PlayerGib + Rigidbody)
  └── ...
```

Каждый гиб привязан к кости модели через свойство `Bone`. В обычном режиме гибы **отключены** (`Enabled = false`).

### Процесс гибания

```csharp
public void Gib( Vector3 origin, Vector3 hitPos, bool noShrink = false )
```

1. **Включаем GameObject** — `GameObject.Enabled = true`
2. **Тег "effect"** — чтобы гибы не блокировали трейсы оружия
3. **Скрываем кость** — `Bone.WorldScale = 0` (кость становится невидимой, гиб заменяет её)
4. **Отсоединяем от игрока** — `SetParent( null, true )`
5. **TemporaryEffect** — гиб самоуничтожится через 10 секунд
6. **Физика** — включаем Rigidbody и прикладываем силу от точки попадания
7. **Эффект** — спавним частицы крови

### Направление разлёта

```csharp
var force = (origin - hitPos).Normal * 4096f;
rb.ApplyForce( force * 100f );
```

- `origin` — откуда прилетел урон (например, позиция ракеты)
- `hitPos` — куда попал
- Направление: от точки попадания в сторону, противоположную источнику урона
- Сила: 4096 × 100 = ~400,000 единиц (сильный толчок)

## Создай файл

Путь: `Code/Player/PlayerGib.cs`

```csharp
/// <summary>
/// Describes a player gib.
/// </summary>
public sealed class PlayerGib : Component
{
	[Property] public TagSet GibTags { get; set; }
	[Property] public GameObject Bone { get; set; }
	[Property] public GameObject Effect { get; set; }

	[Button]
	public void Gib( Vector3 origin, Vector3 hitPos, bool noShrink = false )
	{
		if ( !Game.IsPlaying ) return;

		GameObject.Enabled = true;
		GameObject.Tags.Add( "effect" );

		if ( !noShrink && Bone.IsValid() )
		{
			Bone.Flags = GameObject.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
			Bone.WorldScale = 0;
			WorldPosition = Bone.WorldPosition + Vector3.Down * 64f;
		}


		// Unparent
		GameObject.SetParent( null, true );

		var effect = GameObject.AddComponent<TemporaryEffect>();
		effect.DestroyAfterSeconds = 10;

		var rb = GetComponent<Rigidbody>( true );
		rb.Enabled = true;

		var force = (origin - hitPos).Normal * 4096f;
		rb.ApplyForce( force * 100f );

		if ( Effect.IsValid() )
		{
			Effect?.Clone( hitPos );
		}
	}
}
```

## Ключевые концепции

### [Button] — кнопка в инспекторе

```csharp
[Button]
public void Gib( ... )
```

Атрибут `[Button]` добавляет кнопку «Gib» в инспекторе s&box Editor. Это удобно для тестирования — можно вызвать метод без запуска игры.

### ProceduralBone

```csharp
Bone.Flags = GameObject.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
Bone.WorldScale = 0;
```

Пометка `ProceduralBone` говорит движку: «эта кость управляется кодом, а не анимацией». Без этого флага анимация перезаписала бы наш `WorldScale = 0`.

### TemporaryEffect

```csharp
var effect = GameObject.AddComponent<TemporaryEffect>();
effect.DestroyAfterSeconds = 10;
```

Встроенный компонент s&box, который уничтожает объект через заданное время. Без него гибы копились бы вечно, съедая память.

### noShrink

Параметр `noShrink` используется когда весь игрок превращается в гибы (`Player.Gib()` вызывает с `noShrink: true`). В этом случае кости не уменьшаются, потому что модель игрока уже уничтожается целиком.

## Проверка

1. Получи урон от взрыва → тело разлетается на части
2. Части отлетают в сторону, противоположную взрыву
3. Через 10 секунд гибы исчезают

## Следующий файл

Переходи к **03.12 — Индикаторы урона (PlayerDamageIndicators)**.
