# ?? План реалізації Real-time Streaming MIDI Playback

## ?? Огляд архітектури

**Поточний стан:** Pre-rendering кожної ноти ? SoundEffect ? Play  
**Цільовий стан:** Real-time synthesis через DynamicSoundEffectInstance

---

## ?? Крок 1: Додати streaming поля

```csharp
public class MidiPlayer : ModSystem
{
    // Нові поля для streaming
    private const int BUFFER_SIZE = 4410; // 0.1s @ 44100Hz
    private DynamicSoundEffectInstance _streamingSound;
    private object _synthesizerLock = new object();
    private bool _syncWithTerraria = false;
    
    // Видалити ці поля (більше не потрібні):
    // - _noteCache (Dictionary<int, SoundEffect>)
    // - _activeNotes (Dictionary<(int, byte), ActiveNote>)
}
```

---

## ?? Крок 2: Змінити метод `Play()`

```csharp
public void Play()
{
    if (_currentMidi == null) return;
    
    EnsureInitialized();
    
    _isPlaying = true;
    _currentTime = 0.0;
    _currentEventIndex = 0;
    _playbackTimer.Restart();
    
    lock (_synthesizerLock)
    {
        _synthesizer.Reset();
    }
    
    // ? НОВЕ: Створити streaming audio
    _streamingSound = new DynamicSoundEffectInstance(SAMPLE_RATE, AudioChannels.Stereo);
    _streamingSound.Volume = 1.0f;
    _streamingSound.BufferNeeded += OnBufferNeeded; // Callback
    _streamingSound.Play();
    
    Mod.Logger.Info("Streaming MIDI playback started");
}
```

---

## ?? Крок 3: Додати callback `OnBufferNeeded`

```csharp
/// <summary>
/// Викликається автоматично коли потрібен новий audio буфер
/// </summary>
private void OnBufferNeeded(object sender, EventArgs e)
{
    if (!_isPlaying) return;
    
    float[] leftBuffer = new float[BUFFER_SIZE];
    float[] rightBuffer = new float[BUFFER_SIZE];
    
    // Thread-safe render
    lock (_synthesizerLock)
    {
        _synthesizer.Render(leftBuffer, rightBuffer);
    }
    
    // Конвертувати в byte[] PCM16
    byte[] pcmBuffer = ConvertToStereoPCM16(leftBuffer, rightBuffer);
    
    // Подати буфер в audio engine
    _streamingSound?.SubmitBuffer(pcmBuffer);
}
```

---

## ?? Крок 4: Змінити `ProcessPendingEvents()` ? Надсилати MIDI команди

```csharp
/// <summary>
/// Обробляє MIDI події - тепер просто надсилає команди в synthesizer
/// </summary>
private void ProcessPendingMidiEvents()
{
    lock (_synthesizerLock)
    {
        while (_currentEventIndex < _scheduledEvents.Count)
        {
            var scheduled = _scheduledEvents[_currentEventIndex];
            
            if (scheduled.TimeInSeconds > _currentTime)
                break;
            
            // ? НОВЕ: Безпосередньо надсилаємо MIDI команди
            ProcessMidiEvent(scheduled.Event);
            
            _currentEventIndex++;
        }
    }
}

/// <summary>
/// Надсилає MIDI команду в synthesizer
/// </summary>
private void ProcessMidiEvent(MidiEvent evt)
{
    switch (evt)
    {
        case NoteOnEvent noteOn:
            _synthesizer.ProcessMidiMessage(noteOn.Channel, 0x90, noteOn.Note, noteOn.Velocity);
            break;
            
        case NoteOffEvent noteOff:
            _synthesizer.ProcessMidiMessage(noteOff.Channel, 0x80, noteOff.Note, 0);
            break;
            
        case TempoChangeEvent tempo:
            _bpm = tempo.Bpm;
            break;
    }
}
```

---

## ?? Крок 5: Оновити `PostUpdateEverything()`

```csharp
public override void PostUpdateEverything()
{
    if (!_isPlaying || _synthesizer == null) return;
    
    // ? НОВЕ: Sync з Terraria музикою
    if (_syncWithTerraria && _streamingSound != null)
    {
        _streamingSound.Volume = Main.musicFade * (Main.musicVolume > 0 ? 1f : 0f);
    }
    
    _currentTime = _playbackTimer.Elapsed.TotalSeconds;
    
    // Thread-safe MIDI processing
    ProcessPendingMidiEvents();
    
    // Перевірка кінця
    if (_currentEventIndex >= _scheduledEvents.Count)
    {
        if (_currentTime > _scheduledEvents.Last().TimeInSeconds + 2.0)
        {
            Stop();
        }
    }
}
```

---

## ?? Крок 6: Оновити `Stop()`

```csharp
public void Stop()
{
    _isPlaying = false;
    _currentTime = 0.0;
    _currentEventIndex = 0;
    
    _playbackTimer?.Stop();
    
    // ? НОВЕ: Зупинити streaming
    _streamingSound?.Stop();
    _streamingSound?.Dispose();
    _streamingSound = null;
    
    lock (_synthesizerLock)
    {
        _synthesizer?.Reset();
    }
}
```

---

## ?? Крок 7: Видалити старі методи (більше не потрібні)

**Видалити ці методи:**
- `ProcessMidiEventWithPreRender()` ?
- `FindNoteDuration()` ?
- `PlayPreRenderedNote()` ?
- `GeneratePreRenderedNote()` ?
- `ClearCache()` ?

**Залишити тільки для окремих нот:**
- `PlayNote()` ? (для тестування)
- `GenerateSingleNote()` ?

---

## ?? Крок 8: Додати Seeking функціонал (опціонально)

```csharp
/// <summary>
/// Перемотати на конкретний час
/// </summary>
public void SeekToTime(double targetSeconds)
{
    if (!_isPlaying || _scheduledEvents == null) return;
    
    // Знайти події до target часу
    int targetIndex = _scheduledEvents.FindIndex(e => e.TimeInSeconds >= targetSeconds);
    if (targetIndex < 0) targetIndex = _scheduledEvents.Count;
    
    lock (_synthesizerLock)
    {
        // Reset synthesizer
        _synthesizer.Reset();
        
        // Застосувати стан до target часу
        for (int i = 0; i < targetIndex; i++)
        {
            var evt = _scheduledEvents[i];
            
            // Тільки NoteOn які ще грають
            if (evt.Event is NoteOnEvent noteOn && noteOn.Velocity > 0)
            {
                double noteEnd = FindNoteEnd(i);
                if (noteEnd > targetSeconds)
                {
                    _synthesizer.ProcessMidiMessage(noteOn.Channel, 0x90, noteOn.Note, noteOn.Velocity);
                }
            }
        }
    }
    
    _currentTime = targetSeconds;
    _currentEventIndex = targetIndex;
    
    // Reset timer
    _playbackTimer = System.Diagnostics.Stopwatch.StartNew();
}

public void Skip(double seconds) => SeekToTime(_currentTime + seconds);
public void Rewind(double seconds) => SeekToTime(Math.Max(0, _currentTime - seconds));
```

---

## ?? Крок 9: Додати Terraria sync (опціонально)

```csharp
public void EnableTerrariaSync(bool enable)
{
    _syncWithTerraria = enable;
}
```

---

## ? Переваги нової системи

| До | Після |
|---|---|
| ? Pre-render кожної ноти (~1-10 секунд аудіо) | ? Render 0.1s буферів в реальному часі |
| ? Велика пам'ять (кеш нот) | ? Тільки 2 буфери в пам'яті |
| ? Не можна змінювати темп | ? Легко змінювати швидкість |
| ? Складний seeking | ? Простий seeking |
| ? Не синхронізується з Terraria | ? Sync з музикою гри |

---

## ?? Фінальна структура класу

```
MidiPlayer
??? Fields
?   ??? _streamingSound (DynamicSoundEffectInstance) ? НОВЕ
?   ??? _synthesizerLock (object) ? НОВЕ
?   ??? _syncWithTerraria (bool) ? НОВЕ
?   ??? [видалено _noteCache, _activeNotes]
??? Methods
?   ??? Play() ? ЗМІНЕНО
?   ??? Stop() ? ЗМІНЕНО
?   ??? OnBufferNeeded() ? НОВЕ
?   ??? ProcessPendingMidiEvents() ? ЗМІНЕНО
?   ??? ProcessMidiEvent() ? НОВЕ
?   ??? SeekToTime() ? НОВЕ (опціонально)
?   ??? EnableTerrariaSync() ? НОВЕ (опціонально)
??? [видалено pre-render методи]
```

---

## ?? Порядок реалізації

1. ? Додати поля (`_streamingSound`, `_synthesizerLock`)
2. ? Змінити `Play()` - створити `DynamicSoundEffectInstance`
3. ? Додати `OnBufferNeeded()` callback
4. ? Змінити `ProcessPendingEvents()` ? `ProcessPendingMidiEvents()`
5. ? Додати `ProcessMidiEvent()` для надсилання MIDI команд
6. ? Оновити `Stop()`
7. ? Видалити старі pre-render методи
8. ?? (Опціонально) Додати seeking
9. ?? (Опціонально) Додати Terraria sync

---

## ?? Інтеграція з Terraria

### DynamicSoundEffectInstance - це офіційний XNA/FNA клас

```csharp
// Terraria використовує це для музики:
// Main.audioSystem містить LegacyAudioSystem
// який використовує SoundEffectInstance для звуків

// Ваш код буде працювати ТАК САМО як музика Terraria
```

### Як працює BufferNeeded callback

```
Game Loop (60 FPS)
     ?
PostUpdateEverything() ? Обробка MIDI events
     ?                    (надсилає команди в synthesizer)
     ?
Audio Thread ? Автоматично викликає OnBufferNeeded()
     ?         коли потрібен новий буфер (~10 разів на секунду)
     ?
OnBufferNeeded() ? Render 0.1s audio
     ?             (synthesizer.Render())
     ?
SubmitBuffer() ? Подати в Terraria audio engine
```

---

## ?? Важливі нюанси

### Thread Safety

```csharp
// PostUpdateEverything() викликається в Main Thread
// OnBufferNeeded() викликається в Audio Thread
// Тому потрібен lock(_synthesizerLock)!
```

### Volume Control

```csharp
// VOLUME_BOOST тепер застосовується в ConvertToStereoPCM16()
// Додатково можна контролювати через:
_streamingSound.Volume = Main.musicFade; // Sync з грою
```

### FPS Independence

```csharp
// Stopwatch забезпечує точний timing незалежно від FPS
_currentTime = _playbackTimer.Elapsed.TotalSeconds;
```

---

## ?? Чеклист реалізації

- [ ] Крок 1: Додати поля
- [ ] Крок 2: Змінити `Play()`
- [ ] Крок 3: Додати `OnBufferNeeded()`
- [ ] Крок 4: Змінити обробку MIDI events
- [ ] Крок 5: Оновити `PostUpdateEverything()`
- [ ] Крок 6: Оновити `Stop()`
- [ ] Крок 7: Видалити старі методи
- [ ] Крок 8: (Опціонально) Seeking
- [ ] Крок 9: (Опціонально) Terraria sync
- [ ] Тестування: Відтворення MIDI
- [ ] Тестування: Зупинка/Resume
- [ ] Тестування: Зміна темпу
- [ ] Тестування: FPS незалежність

---

**Готово до реалізації!** ???
