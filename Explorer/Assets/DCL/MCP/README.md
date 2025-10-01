# DCL MCP WebSocket Integration

Интеграция WebSocket сервера для коммуникации между Unity клиентом и MCP Server (Model Context Protocol).

## 🎯 Назначение

Позволяет внешним инструментам (например, Claude Desktop через MCP Server) подключаться к работающему Unity приложению и получать информацию в реальном времени:
- FPS (frames per second)
- Использование памяти
- Информация о сценах
- Системная информация

## 📁 Структура

```
Assets/DCL/MCP/
├── MCPWebSocketServer.cs      # Серверная часть на базе Fleck
├── MCPPlugin.cs               # Глобальный плагин для автоматического запуска
├── DCL.MCP.asmdef             # Assembly Definition
└── README.md                  # Документация
```

## 🚀 Быстрый старт

### Unity (Сервер)

1. **Плагин запускается автоматически**:
   - `MCPPlugin` регистрируется как глобальный плагин в `DynamicWorldContainer`
   - Запускается автоматически при старте приложения
   - Не требует добавления на сцену - работает как часть системы плагинов

2. **Запустите приложение**:
   - При старте в логах появится:
     ```
     [MCP Plugin] MCP WebSocket Server successfully started on port 7777
     ```

### MCP Server (Клиент)

1. **Установите зависимости**:
   ```bash
   cd c:\DCL\MCPServers\explorer-mcp-server
   npm install ws
   ```

2. **Запустите MCP Server**:
   ```bash
   npm start
   ```

3. **Используйте в Claude Desktop**:
   
   Сценарий 1 - Запустить Unity и подключиться:
   ```
   User: Запусти Decentraland Explorer и покажи FPS
   
   Claude использует tools:
   1. start_unity
   2. (ждёт 3-5 секунд)
   3. connect_to_unity_ws
   4. get_unity_fps
   ```

   Сценарий 2 - Подключиться к уже работающему:
   ```
   User: Подключись к работающему Unity и покажи FPS
   
   Claude использует tools:
   1. connect_to_unity_ws
   2. get_unity_fps
   ```

## 🔧 Доступные команды MCP

### Управление подключением

- **`connect_to_unity_ws`** - Подключиться к Unity WebSocket
  ```json
  {
    "host": "localhost",  // optional
    "port": 7777          // optional
  }
  ```

- **`disconnect_from_unity_ws`** - Отключиться от Unity

### Запросы информации

- **`get_unity_fps`** - Получить FPS
  ```
  Returns:
  - fps: мгновенный FPS
  - smoothedFps: сглаженный FPS
  - frameTime: время кадра в мс
  - targetFrameRate: целевой FPS
  - vsyncCount: VSync
  ```

- **`get_unity_scene_info`** - Информация о сценах
  ```
  Returns:
  - activeScene: активная сцена
  - loadedScenes: список загруженных сцен
  ```

- **`get_unity_memory`** - Использование памяти
  ```
  Returns:
  - totalReservedMemoryMB
  - totalAllocatedMemoryMB
  - monoHeapSizeMB
  - monoUsedSizeMB
  ```

- **`get_unity_system_info`** - Системная информация
  ```
  Returns:
  - OS, CPU, GPU
  - Unity version
  - Platform
  ```

### Управление скриншотами

- **`take_screenshot`** - Сделать скриншот (полный автоматический процесс)
  ```json
  {
    "waitBeforeCapture": 1500,  // ms перед захватом (optional, default: 1500)
    "waitAfterCapture": 1000,    // ms после захвата (optional, default: 1000)
    "source": "MCP"              // источник (optional, default: "MCP")
  }
  ```
  
  **Автоматически выполняет полный флоу:**
  1. Открывает InWorld Camera
  2. Ждёт стабилизации (1.5 сек по умолчанию)
  3. Делает скриншот
  4. Ждёт проигрывания эффектов (1 сек по умолчанию)
  5. Закрывает камеру
  
  Скриншот сохраняется в Camera Reel с метаданными (профили в кадре, локация, и т.д.)

## 📡 Протокол WebSocket

Используется JSON-RPC 2.0 формат:

### Запрос (Client → Unity):
```json
{
  "id": 1,
  "method": "getFPS",
  "params": {}
}
```

### Ответ (Unity → Client):
```json
{
  "id": 1,
  "result": {
    "fps": 60.5,
    "smoothedFps": 59.8,
    "frameTime": 16.6,
    "targetFrameRate": -1,
    "vsyncCount": 1,
    "timestamp": "2025-09-30T12:34:56.789Z"
  }
}
```

### Ошибка:
```json
{
  "id": 1,
  "error": {
    "code": -32601,
    "message": "Method not found: unknownMethod"
  }
}
```

## 🛠️ Расширение функциональности

### Добавление новой команды в Unity

В `MCPPlugin.cs`:

```csharp
private async UniTask<object> HandleCustomCommand(JObject parameters)
{
    string param = parameters["someParam"]?.ToString();
    
    // Ваша логика здесь
    
    return new
    {
        success = true,
        result = "Custom data"
    };
}

// В Initialize():
server.RegisterHandler("customCommand", HandleCustomCommand);
```

### Добавление команды в MCP Server

В `unityInstancesTools.ts`:

```typescript
function registerCustomTool(server: McpServer) {
    server.registerTool(
        "custom_unity_command",
        {
            title: "Custom Unity Command",
            description: "Does something custom",
            inputSchema: {
                type: "object",
                properties: {
                    someParam: { type: "string" }
                }
            }
        },
        async (args: any) => {
            const result = await sendUnityCommand("customCommand", {
                someParam: args.someParam
            });
            
            return {
                content: [{
                    type: "text",
                    text: `Result: ${result.result}`
                }]
            };
        }
    );
}

// В registerUnityInstanceTools():
registerCustomTool(server);
```

## 🔍 Отладка

### Unity логи
```csharp
// Включите категорию DEBUG в ReportHub
[MCP Plugin] MCP WebSocket Server successfully started on port 7777
[MCP WS] Client connected: 127.0.0.1:54321
[MCP WS] Received: method=getFPS, id=1
[MCP WS] Sent response: id=1
```

### MCP Server логи
```bash
# В stderr (видны в терминале)
[MCP] Connected to Unity WebSocket at ws://localhost:7777
[MCP] Unity Event: fpsChanged { fps: 60.5 }
```

## ⚠️ Требования

- **Unity**: 
  - Fleck.dll (уже есть в проекте)
  - Newtonsoft.Json.dll (уже есть)
  - Cysharp.UniTask (уже есть)

- **MCP Server**:
  - Node.js 18+
  - npm package: `ws`

## 🔒 Безопасность

- Сервер слушает только на localhost (0.0.0.0 = все интерфейсы, но можно изменить)
- Нет аутентификации (для production добавьте токены)
- WebSocket без SSL (для production используйте wss://)

## 📝 Примеры использования

### Пример 1: Мониторинг FPS в реальном времени

```typescript
// В вашем MCP tool
async function monitorFPS() {
    await connectToUnityWebSocket("localhost", 7777);
    
    setInterval(async () => {
        const fps = await sendUnityCommand("getFPS", {});
        console.log(`Current FPS: ${fps.fps}`);
    }, 1000);
}
```

### Пример 2: Автоматическая диагностика

```typescript
async function diagnosePerformance() {
    const fps = await sendUnityCommand("getFPS", {});
    const memory = await sendUnityCommand("getMemoryUsage", {});
    
    if (fps.fps < 30) {
        console.log("⚠️ Low FPS detected!");
        console.log(`Memory: ${memory.totalAllocatedMemoryMB} MB`);
    }
}
```

### Пример 3: Создание скриншота

```typescript
// Вызов через MCP tool (в Claude Desktop или другом MCP client)
async function takeScreenshot() {
    // Просто вызываем tool - всё остальное делается автоматически
    const result = await useTool("take_screenshot", {
        waitBeforeCapture: 1500,  // опционально
        waitAfterCapture: 1000,    // опционально
        source: "Claude"           // опционально
    });
    
    // Результат:
    // ✅ Screenshot taken successfully!
    // 📸 Screenshot saved to Camera Reel
    // ⏱️  Total time: 2500ms
    // 🏷️  Source: Claude
    // Process:
    //   ✓ Camera opened
    //   ✓ Stabilized (1500ms)
    //   ✓ Screenshot captured
    //   ✓ Effects played (1000ms)
    //   ✓ Camera closed
}

// Или через прямой вызов Unity команды (если используете WebSocket напрямую)
async function takeScreenshotDirect() {
    await connectToUnityWebSocket("localhost", 7777);
    
    // С кастомными таймингами
    const result = await sendUnityCommand("take_screenshot", {
        waitBeforeCapture: 2000,  // Больше времени для стабилизации
        waitAfterCapture: 1500,   // Больше времени для эффектов
        source: "AutoTest"
    });
    
    console.log("Screenshot result:", result);
}
```

## 📄 Лицензия

Part of Decentraland Explorer project.

