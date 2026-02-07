# MyIOT — IoT Backend Platform

Платформа для сбора телеметрии и атрибутов от IoT-устройств (аналог ThingsBoard).

## Архитектура

- **Backend**: ASP.NET Core 9 (Minimal APIs)
- **Database**: PostgreSQL 16 + TimescaleDB (для телеметрии)
- **Cache**: Redis (последние значения телеметрии)
- **Transport**: MQTT (встроенный брокер MQTTnet) + HTTP REST API
- **Auth**: JWT токены для HTTP, AccessToken для MQTT

## Структура проекта

```
MyIOT/
├── src/
│   ├── MyIOT.Shared/        # Shared-библиотека (DTO, enums, constants)
│   └── MyIOT.Api/           # Основной сервер
├── tests/
│   └── MyIOT.Tests/         # Юнит-тесты (17 тестов)
├── samples/
│   └── MqttTestClient/      # MQTT тестовый клиент
└── docker-compose.yml       # TimescaleDB + Redis
```

## Быстрый старт (WSL/Linux)

### Предварительные требования

```bash
# Проверка установленных компонентов
dotnet --version    # Должно быть 9.0+
docker --version
docker-compose --version
```

### 1. Запуск инфраструктуры

```bash
# Поднять PostgreSQL/TimescaleDB + Redis
docker-compose up -d

# Проверка состояния
docker-compose ps

# Логи (опционально)
docker-compose logs -f
```

**Подключения:**
- PostgreSQL: `localhost:5432` (user: `myiot`, pass: `myiot_secret`, db: `myiot_db`)
- Redis: `localhost:6379`

### 2. Создание и применение миграций

```bash
# Установка EF Core CLI (один раз)
dotnet tool install --global dotnet-ef

# Создание начальной миграции
dotnet ef migrations add InitialCreate --project src/MyIOT.Api

# Применение миграций (автоматически при старте приложения)
# Или вручную:
dotnet ef database update --project src/MyIOT.Api
```

### 3. Запуск приложения

```bash
# Режим разработки
dotnet run --project src/MyIOT.Api

# Или с hot-reload
dotnet watch run --project src/MyIOT.Api
```

**Приложение будет доступно:**
- HTTP API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- MQTT Broker: `localhost:1883`

### 4. Создание устройства

#### Через Swagger UI
1. Открыть http://localhost:5000/swagger
2. `POST /api/devices` → Выполнить с телом: `{ "name": "TestDevice" }`
3. Скопировать `accessToken` из ответа

#### Через curl
```bash
curl -X POST http://localhost:5000/api/devices \
  -H "Content-Type: application/json" \
  -d '{"name": "Sensor1"}'

# Ответ:
# {
#   "id": "...",
#   "name": "Sensor1",
#   "accessToken": "ABC123..."
# }
```

### 5. Получение JWT-токена

```bash
curl -X POST http://localhost:5000/api/auth/device/login \
  -H "Content-Type: application/json" \
  -d '{"accessToken": "YOUR_ACCESS_TOKEN"}'

# Ответ:
# {
#   "token": "eyJhbGciOi...",
#   "expiresAt": "2026-02-08T..."
# }
```

### 6. Отправка телеметрии через HTTP

```bash
JWT_TOKEN="eyJhbGciOi..."

curl -X POST http://localhost:5000/api/telemetry \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -d '{"values": {"temperature": 25.5, "humidity": 60.0}}'
```

### 7. Отправка данных через MQTT

```bash
# Запуск тестового клиента
cd samples/MqttTestClient

# 1. Вставить accessToken в Program.cs (строка 18)
# 2. Запустить
dotnet run

# Клиент подключится к localhost:1883 и отправит:
# - 5 записей телеметрии (temperature, humidity, pressure)
# - Атрибуты устройства (firmware, model, serial_number, location)
```

### 8. Получение данных

```bash
DEVICE_ID="..."  # Взять из ответа POST /api/devices

# Последние значения телеметрии
curl -H "Authorization: Bearer $JWT_TOKEN" \
  http://localhost:5000/api/devices/$DEVICE_ID/telemetry/latest

# История телеметрии за период
curl -H "Authorization: Bearer $JWT_TOKEN" \
  "http://localhost:5000/api/devices/$DEVICE_ID/telemetry?key=temperature&from=2026-02-07T00:00:00Z&to=2026-02-08T00:00:00Z"

# Атрибуты устройства
curl -H "Authorization: Bearer $JWT_TOKEN" \
  http://localhost:5000/api/devices/$DEVICE_ID/attributes
```

## Запуск тестов

```bash
# Все тесты (17)
dotnet test

# С подробным выводом
dotnet test --verbosity detailed

# Только конкретный класс
dotnet test --filter FullyQualifiedName~DeviceServiceTests
```

## Структура данных

### Device (Устройство)
```json
{
  "id": "uuid",
  "name": "Sensor1",
  "accessToken": "crypto-random-token",
  "createdAt": "2026-02-07T..."
}
```

### Telemetry (Телеметрия)
- Хранится в TimescaleDB hypertable
- Ключ-значение-время: `(deviceId, key, timestamp, value)`
- Кэшируется в Redis: hash `telemetry:latest:{deviceId}`

### Attributes (Атрибуты)
- 3 scope: `Client` (от устройства), `Server` (от платформы), `Shared`
- JSON-значения: `{ "firmware": "1.2.0", "model": "..." }`

## API Эндпоинты

| Метод | Путь | Описание | Auth |
|---|---|---|---|
| `POST` | `/api/devices` | Создать устройство | — |
| `GET` | `/api/devices` | Список устройств | JWT |
| `GET` | `/api/devices/{id}` | Получить устройство | JWT |
| `POST` | `/api/auth/device/login` | Аутентификация | — |
| `POST` | `/api/telemetry` | Отправить телеметрию | JWT |
| `GET` | `/api/devices/{id}/telemetry/latest` | Последние значения | JWT |
| `GET` | `/api/devices/{id}/telemetry` | История (query: key, from, to) | JWT |
| `POST` | `/api/attributes` | Отправить атрибуты | JWT |
| `GET` | `/api/devices/{id}/attributes` | Получить атрибуты | JWT |

## MQTT Топики

| Топик | Направление | Payload | Описание |
|---|---|---|---|
| `v1/devices/me/telemetry` | Device → Server | `{"temp": 25.5, ...}` | Отправка телеметрии |
| `v1/devices/me/attributes` | Device → Server | `{"firmware": "1.0", ...}` | Отправка атрибутов |

**Аутентификация MQTT:**
- Username = `accessToken` устройства
- Password = не используется
- ClientId = произвольный

## Настройка (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=myiot_db;Username=myiot;Password=myiot_secret",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "Secret": "your-secret-key-min-256-bits",
    "Issuer": "MyIOT.Api",
    "Audience": "MyIOT.Devices",
    "ExpiryMinutes": 1440
  },
  "MqttSettings": {
    "Port": 1883
  }
}
```

## Troubleshooting

### TimescaleDB не создаёт hypertable

```bash
# Подключиться к PostgreSQL
docker exec -it myiot-timescaledb psql -U myiot -d myiot_db

# Проверить hypertables
SELECT * FROM timescaledb_information.hypertables;

# Создать вручную (если нужно)
SELECT create_hypertable('telemetry', 'timestamp');
```

### Redis недоступен

```bash
# Проверка подключения
docker exec -it myiot-redis redis-cli ping
# Ответ: PONG

# Просмотр ключей
docker exec -it myiot-redis redis-cli keys "telemetry:*"
```

### MQTT-клиент не подключается

1. Проверить, что сервер запущен (`MQTT broker started on port 1883` в логах)
2. Убедиться, что `accessToken` корректный
3. Проверить логи: `docker-compose logs -f` или консоль ASP.NET Core

### Миграции не работают

```bash
# Удалить старые миграции
rm -rf src/MyIOT.Api/Migrations

# Пересоздать
dotnet ef migrations add InitialCreate --project src/MyIOT.Api

# Применить
dotnet ef database update --project src/MyIOT.Api
```

## Production Deployment

### 1. Docker-образ API (TODO)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 1883

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/MyIOT.Api/MyIOT.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyIOT.Api.dll"]
```

### 2. docker-compose.prod.yml
```yaml
services:
  timescaledb:
    restart: always
    environment:
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - ./data/timescaledb:/var/lib/postgresql/data

  redis:
    restart: always
    command: redis-server --requirepass ${REDIS_PASSWORD}

  myiot-api:
    build: .
    ports:
      - "80:80"
      - "1883:1883"
    environment:
      ConnectionStrings__DefaultConnection: "Host=timescaledb;..."
      ConnectionStrings__Redis: "redis:6379,password=${REDIS_PASSWORD}"
      JwtSettings__Secret: ${JWT_SECRET}
    depends_on:
      - timescaledb
      - redis
```

### 3. Настройки безопасности

- **JWT Secret**: Генерировать криптостойкий ключ ≥256 бит
- **PostgreSQL**: Изменить `myiot_secret` на безопасный пароль
- **Redis**: Включить аутентификацию (`requirepass`)
- **MQTT over TLS**: Настроить сертификаты для MQTTnet
- **Rate Limiting**: Добавить `AspNetCoreRateLimit` middleware

## Будущие улучшения

- [ ] **Rule Engine** — правила обработки телеметрии (alerts, aggregations)
- [ ] **WebSocket** — real-time уведомления для UI
- [ ] **Blazor WASM Frontend** — админ-панель (использует `MyIOT.Shared`)
- [ ] **Multi-tenancy** — поддержка нескольких организаций
- [ ] **Device provisioning** — автоматическая регистрация устройств
- [ ] **Dashboards** — визуализация данных (Grafana или встроенная)
- [ ] **Alarms** — настраиваемые пороги и уведомления
- [ ] **RPC** — удалённые команды устройствам через MQTT

## Разработка

```bash
# Формат кода
dotnet format

# Анализ кода
dotnet build /p:RunAnalyzers=true /p:EnforceCodeStyleInBuild=true

# Покрытие тестами (с coverlet)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Лицензия

MIT

## Контакты

GitHub: [@psxbox](https://github.com/psxbox)
