# MyIOT API Examples

Коллекция примеров HTTP-запросов для тестирования API.

## Переменные окружения

Установите эти переменные перед использованием:

```bash
export API_URL="http://localhost:5000"
export DEVICE_ID="your-device-id-here"
export ACCESS_TOKEN="your-access-token-here"
export JWT_TOKEN="your-jwt-token-here"
```

---

## 1. Device Management

### Создать устройство

```bash
curl -X POST $API_URL/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My IoT Device"
  }'
```

**Ответ:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "My IoT Device",
  "accessToken": "Abc123RandomToken456"
}
```

**Сохраните:** `accessToken` для MQTT и `id` для дальнейших запросов.

---

### Получить список устройств

```bash
curl -X GET $API_URL/api/devices \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

### Получить устройство по ID

```bash
curl -X GET $API_URL/api/devices/$DEVICE_ID \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## 2. Authentication

### Аутентификация (получить JWT)

```bash
curl -X POST $API_URL/api/auth/device/login \
  -H "Content-Type: application/json" \
  -d "{
    \"accessToken\": \"$ACCESS_TOKEN\"
  }"
```

**Ответ:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-02-08T12:00:00Z"
}
```

**Сохраните:** `token` для использования в заголовке `Authorization: Bearer <token>`.

---

## 3. Telemetry

### Отправить телеметрию

```bash
curl -X POST $API_URL/api/telemetry \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -d '{
    "values": {
      "temperature": 23.5,
      "humidity": 55.0,
      "pressure": 1013.25,
      "battery": 85.0
    }
  }'
```

**Ответ:**
```json
{
  "message": "Telemetry saved",
  "count": 4
}
```

---

### Получить последние значения

```bash
curl -X GET $API_URL/api/devices/$DEVICE_ID/telemetry/latest \
  -H "Authorization: Bearer $JWT_TOKEN"
```

**Ответ:**
```json
[
  {
    "key": "temperature",
    "value": 23.5,
    "timestamp": "2026-02-07T14:30:00Z"
  },
  {
    "key": "humidity",
    "value": 55.0,
    "timestamp": "2026-02-07T14:30:00Z"
  }
]
```

---

### Получить историю телеметрии

```bash
# За последний час
FROM_TIME=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)
TO_TIME=$(date -u +%Y-%m-%dT%H:%M:%SZ)

curl -X GET "$API_URL/api/devices/$DEVICE_ID/telemetry?key=temperature&from=$FROM_TIME&to=$TO_TIME" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

**Ответ:**
```json
{
  "key": "temperature",
  "dataPoints": [
    {
      "value": 22.0,
      "timestamp": "2026-02-07T13:30:00Z"
    },
    {
      "value": 23.5,
      "timestamp": "2026-02-07T14:00:00Z"
    },
    {
      "value": 24.2,
      "timestamp": "2026-02-07T14:30:00Z"
    }
  ]
}
```

---

## 4. Attributes

### Отправить атрибуты

```bash
curl -X POST $API_URL/api/attributes \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -d '{
    "values": {
      "firmware": "2.1.5",
      "model": "IoT-Sensor-Pro",
      "serial_number": "SN-2026-001234",
      "location": "Building A, Floor 3"
    },
    "scope": "Client"
  }'
```

**Scope опции:** `Client` (от устройства), `Server` (от платформы), `Shared` (общие).

---

### Получить атрибуты

```bash
# Все атрибуты
curl -X GET $API_URL/api/devices/$DEVICE_ID/attributes \
  -H "Authorization: Bearer $JWT_TOKEN"

# Только Client scope
curl -X GET "$API_URL/api/devices/$DEVICE_ID/attributes?scope=Client" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

**Ответ:**
```json
[
  {
    "key": "firmware",
    "value": "\"2.1.5\"",
    "scope": "Client",
    "updatedAt": "2026-02-07T14:00:00Z"
  },
  {
    "key": "model",
    "value": "\"IoT-Sensor-Pro\"",
    "scope": "Client",
    "updatedAt": "2026-02-07T14:00:00Z"
  }
]
```

---

## 5. MQTT Examples

### Mosquitto CLI (mosquitto_pub)

```bash
# Установка (Ubuntu/Debian)
sudo apt-get install mosquitto-clients

# Отправка телеметрии
mosquitto_pub -h localhost -p 1883 \
  -u "$ACCESS_TOKEN" \
  -t "v1/devices/me/telemetry" \
  -m '{"temperature": 25.3, "humidity": 60.0}'

# Отправка атрибутов
mosquitto_pub -h localhost -p 1883 \
  -u "$ACCESS_TOKEN" \
  -t "v1/devices/me/attributes" \
  -m '{"firmware": "2.0.0", "model": "SensorX"}'
```

---

### Python MQTT Client

```python
import paho.mqtt.client as mqtt
import json
import time

ACCESS_TOKEN = "your-access-token"

client = mqtt.Client(client_id="python-device-1", clean_session=True)
client.username_pw_set(ACCESS_TOKEN)

def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("Connected to MQTT broker")
        
        # Отправка телеметрии
        telemetry = {"temperature": 25.5, "humidity": 55.0}
        client.publish("v1/devices/me/telemetry", json.dumps(telemetry))
        
        # Отправка атрибутов
        attributes = {"firmware": "1.0.0", "location": "Lab"}
        client.publish("v1/devices/me/attributes", json.dumps(attributes))
        
        print("Data sent!")
    else:
        print(f"Connection failed with code {rc}")

client.on_connect = on_connect
client.connect("localhost", 1883, 60)

client.loop_start()
time.sleep(2)
client.loop_stop()
client.disconnect()
```

**Установка:**
```bash
pip install paho-mqtt
```

---

### Node.js MQTT Client

```javascript
const mqtt = require('mqtt');

const ACCESS_TOKEN = 'your-access-token';
const client = mqtt.connect('mqtt://localhost:1883', {
  username: ACCESS_TOKEN,
  clientId: 'nodejs-device-1'
});

client.on('connect', () => {
  console.log('Connected to MQTT broker');
  
  // Отправка телеметрии
  const telemetry = { temperature: 25.5, humidity: 55.0 };
  client.publish('v1/devices/me/telemetry', JSON.stringify(telemetry));
  
  // Отправка атрибутов
  const attributes = { firmware: '1.0.0', location: 'Lab' };
  client.publish('v1/devices/me/attributes', JSON.stringify(attributes));
  
  console.log('Data sent!');
  
  setTimeout(() => {
    client.end();
  }, 1000);
});

client.on('error', (err) => {
  console.error('Connection error:', err);
});
```

**Установка:**
```bash
npm install mqtt
```

---

## 6. Stress Testing

### Массовая отправка телеметрии (bash loop)

```bash
for i in {1..100}; do
  TEMP=$(awk -v min=20 -v max=30 'BEGIN{srand(); print min+rand()*(max-min)}')
  HUM=$(awk -v min=40 -v max=80 'BEGIN{srand(); print min+rand()*(max-min)}')
  
  curl -s -X POST $API_URL/api/telemetry \
    -H "Authorization: Bearer $JWT_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"values\": {\"temperature\": $TEMP, \"humidity\": $HUM}}" \
    > /dev/null
  
  echo "Sent #$i: temp=$TEMP, hum=$HUM"
  sleep 0.5
done
```

---

### Apache Bench (HTTP load test)

```bash
# Установка
sudo apt-get install apache2-utils

# Тест POST /api/telemetry
ab -n 1000 -c 10 \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -p telemetry.json \
  $API_URL/api/telemetry
```

**telemetry.json:**
```json
{"values": {"temperature": 25.0, "humidity": 60.0}}
```

---

## 7. Database Queries

### Прямые SQL-запросы в PostgreSQL

```bash
# Подключиться к БД
docker exec -it myiot-timescaledb psql -U myiot -d myiot_db

# Или через psql напрямую
psql -h localhost -p 5432 -U myiot -d myiot_db
```

**Примеры запросов:**

```sql
-- Количество устройств
SELECT COUNT(*) FROM devices;

-- Последние 10 записей телеметрии
SELECT device_id, key, timestamp, value 
FROM telemetry 
ORDER BY timestamp DESC 
LIMIT 10;

-- Средняя температура за час
SELECT 
  time_bucket('1 hour', timestamp) AS hour,
  AVG(value) as avg_temperature
FROM telemetry
WHERE key = 'temperature'
  AND timestamp > NOW() - INTERVAL '24 hours'
GROUP BY hour
ORDER BY hour;

-- Атрибуты устройства
SELECT d.name, a.key, a.value, a.scope, a.updated_at
FROM devices d
LEFT JOIN device_attributes a ON d.id = a.device_id
WHERE d.name = 'DemoDevice';
```

---

## 8. Redis Cache Inspection

```bash
# Подключиться к Redis
docker exec -it myiot-redis redis-cli

# Или через redis-cli напрямую
redis-cli -h localhost -p 6379
```

**Команды Redis:**

```redis
# Все ключи телеметрии
KEYS telemetry:*

# Получить все поля для устройства
HGETALL telemetry:latest:550e8400-e29b-41d4-a716-446655440000

# Получить конкретное значение
HGET telemetry:latest:550e8400-e29b-41d4-a716-446655440000 temperature

# Удалить кэш устройства
DEL telemetry:latest:550e8400-e29b-41d4-a716-446655440000

# Статистика Redis
INFO stats
```

---

## Tips & Tricks

### Сохранение переменных после создания устройства

```bash
# Создать устройство и сохранить переменные
RESPONSE=$(curl -s -X POST $API_URL/api/devices \
  -H "Content-Type: application/json" \
  -d '{"name": "MyDevice"}')

export DEVICE_ID=$(echo $RESPONSE | jq -r '.id')
export ACCESS_TOKEN=$(echo $RESPONSE | jq -r '.accessToken')

# Получить JWT
TOKEN_RESPONSE=$(curl -s -X POST $API_URL/api/auth/device/login \
  -H "Content-Type: application/json" \
  -d "{\"accessToken\": \"$ACCESS_TOKEN\"}")

export JWT_TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.token')

# Сохранить в файл для повторного использования
cat > .env <<EOF
DEVICE_ID=$DEVICE_ID
ACCESS_TOKEN=$ACCESS_TOKEN
JWT_TOKEN=$JWT_TOKEN
EOF

# Загрузить при следующем запуске
source .env
```

**Требует:** `jq` (`sudo apt-get install jq`)

---

### Форматирование JSON-ответов

```bash
# С jq
curl -s $API_URL/api/devices/$DEVICE_ID/telemetry/latest \
  -H "Authorization: Bearer $JWT_TOKEN" | jq '.'

# Без jq (Python)
curl -s $API_URL/api/devices/$DEVICE_ID/telemetry/latest \
  -H "Authorization: Bearer $JWT_TOKEN" | python3 -m json.tool
```

---

### Мониторинг логов

```bash
# Логи API (если запущен через dotnet run)
# Ctrl+C для остановки

# Логи Docker-сервисов
docker-compose logs -f

# Только PostgreSQL
docker-compose logs -f timescaledb

# Только Redis
docker-compose logs -f redis
```

---

## Полезные ссылки

- Swagger UI: http://localhost:5000/swagger
- ReDoc (альтернативная документация): http://localhost:5000/redoc (если настроено)
- Health Check: `curl http://localhost:5000/health` (если настроено)
