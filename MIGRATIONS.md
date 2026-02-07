# Database Migration Guide

Инструкции по созданию и применению миграций для MyIOT.

## Создание начальной миграции

### 1. Установка EF Core CLI

```bash
# Проверка установки
dotnet ef --version

# Если не установлен
dotnet tool install --global dotnet-ef

# Обновление
dotnet tool update --global dotnet-ef
```

### 2. Создание миграции

```bash
# Из корня проекта
dotnet ef migrations add InitialCreate --project src/MyIOT.Api

# Или из директории API
cd src/MyIOT.Api
dotnet ef migrations add InitialCreate
```

**Результат:** Создаётся папка `Migrations/` с файлами:
- `{timestamp}_InitialCreate.cs` — основной файл миграции
- `{timestamp}_InitialCreate.Designer.cs` — метаданные
- `AppDbContextModelSnapshot.cs` — снимок модели

### 3. Просмотр SQL

```bash
# Сгенерировать SQL без применения
dotnet ef migrations script --project src/MyIOT.Api

# Только для конкретной миграции
dotnet ef migrations script 0 InitialCreate --project src/MyIOT.Api
```

### 4. Применение миграции

```bash
# Применить все pending-миграции
dotnet ef database update --project src/MyIOT.Api

# Применить до конкретной миграции
dotnet ef database update InitialCreate --project src/MyIOT.Api

# Откат до предыдущей миграции
dotnet ef database update PreviousMigrationName --project src/MyIOT.Api

# Откат всех миграций
dotnet ef database update 0 --project src/MyIOT.Api
```

---

## TimescaleDB Hypertable Setup

### Автоматическое создание (через Program.cs)

Hypertable создаётся автоматически при старте приложения:

```csharp
// src/MyIOT.Api/Program.cs (уже реализовано)
await db.Database.ExecuteSqlRawAsync(@"
    DO $$
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM timescaledb_information.hypertables
            WHERE hypertable_name = 'telemetry'
        ) THEN
            PERFORM create_hypertable('telemetry', 'timestamp');
        END IF;
    END $$;
");
```

### Ручное создание

Если нужно создать custom-миграцию:

```bash
# Создать пустую миграцию
dotnet ef migrations add CreateTelemetryHypertable --project src/MyIOT.Api
```

Отредактировать файл миграции:

```csharp
// Migrations/{timestamp}_CreateTelemetryHypertable.cs
using Microsoft.EntityFrameworkCore.Migrations;

public partial class CreateTelemetryHypertable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Проверка, что таблица telemetry существует
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.tables 
                    WHERE table_name = 'telemetry'
                ) THEN
                    -- Создать hypertable, если еще не создан
                    IF NOT EXISTS (
                        SELECT 1 FROM timescaledb_information.hypertables
                        WHERE hypertable_name = 'telemetry'
                    ) THEN
                        PERFORM create_hypertable('telemetry', 'timestamp', 
                            chunk_time_interval => INTERVAL '7 days');
                    END IF;
                END IF;
            END $$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Note: Нельзя напрямую откатить hypertable в обычную таблицу
        // Нужно вручную пересоздать таблицу
        migrationBuilder.Sql("-- Manual cleanup required for hypertable");
    }
}
```

### Продвинутая конфигурация TimescaleDB

```csharp
// Миграция с retention policy и compression
migrationBuilder.Sql(@"
    -- Создать hypertable с chunk = 1 день
    SELECT create_hypertable('telemetry', 'timestamp', 
        chunk_time_interval => INTERVAL '1 day',
        if_not_exists => TRUE);

    -- Compression policy: сжимать данные старше 7 дней
    SELECT add_compression_policy('telemetry', INTERVAL '7 days',
        if_not_exists => TRUE);

    -- Retention policy: удалять данные старше 1 года
    SELECT add_retention_policy('telemetry', INTERVAL '1 year',
        if_not_exists => TRUE);

    -- Индекс для ускорения запросов по device_id
    CREATE INDEX IF NOT EXISTS idx_telemetry_device_time 
        ON telemetry (device_id, timestamp DESC);

    -- Индекс для запросов по ключу
    CREATE INDEX IF NOT EXISTS idx_telemetry_key 
        ON telemetry (key);
");
```

---

## Проверка состояния БД

### Подключение к PostgreSQL

```bash
# Через Docker
docker exec -it myiot-timescaledb psql -U myiot -d myiot_db

# Напрямую (если PostgreSQL установлен локально)
psql -h localhost -p 5432 -U myiot -d myiot_db
```

### Проверка миграций

```sql
-- Таблица истории миграций EF Core
SELECT * FROM "__EFMigrationsHistory";

-- Все таблицы в БД
\dt

-- Структура таблицы
\d telemetry
\d devices
\d device_attributes
```

### Проверка TimescaleDB

```sql
-- Список hypertables
SELECT * FROM timescaledb_information.hypertables;

-- Chunks для hypertable
SELECT * FROM timescaledb_information.chunks
WHERE hypertable_name = 'telemetry';

-- Статистика по chunk'ам
SELECT 
    hypertable_name,
    chunk_name,
    range_start,
    range_end,
    pg_size_pretty(total_bytes) as total_size
FROM timescaledb_information.chunks
WHERE hypertable_name = 'telemetry'
ORDER BY range_start DESC;

-- Размер таблицы
SELECT 
    hypertable_name,
    pg_size_pretty(hypertable_size('telemetry')) as size_compressed,
    pg_size_pretty(hypertable_size('telemetry', true)) as size_uncompressed
FROM timescaledb_information.hypertables
WHERE hypertable_name = 'telemetry';

-- Проверка compression
SELECT * FROM timescaledb_information.compression_settings
WHERE hypertable_name = 'telemetry';
```

---

## Распространённые сценарии

### 1. Добавление нового поля в Device

```bash
# 1. Изменить модель
# src/MyIOT.Api/Models/Device.cs
public string? Description { get; set; }

# 2. Создать миграцию
dotnet ef migrations add AddDescriptionToDevice --project src/MyIOT.Api

# 3. Применить
dotnet ef database update --project src/MyIOT.Api
```

### 2. Изменение типа столбца

```bash
# 1. Изменить модель
# DeviceAttribute.Value: string → JsonDocument

# 2. Создать миграцию
dotnet ef migrations add ChangeAttributeValueType --project src/MyIOT.Api

# 3. Вручную отредактировать миграцию для data migration
```

Пример data migration:

```csharp
public partial class ChangeAttributeValueType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Создать временный столбец
        migrationBuilder.AddColumn<string>(
            name: "value_temp",
            table: "device_attributes");

        // 2. Скопировать данные
        migrationBuilder.Sql(
            "UPDATE device_attributes SET value_temp = value");

        // 3. Удалить старый столбец
        migrationBuilder.DropColumn(
            name: "value",
            table: "device_attributes");

        // 4. Создать новый столбец с нужным типом
        migrationBuilder.AddColumn<string>(
            name: "value",
            table: "device_attributes",
            type: "jsonb",
            nullable: false);

        // 5. Скопировать данные обратно
        migrationBuilder.Sql(
            "UPDATE device_attributes SET value = value_temp::jsonb");

        // 6. Удалить временный столбец
        migrationBuilder.DropColumn(
            name: "value_temp",
            table: "device_attributes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Обратная операция
        migrationBuilder.AlterColumn<string>(
            name: "value",
            table: "device_attributes",
            type: "text",
            nullable: false);
    }
}
```

### 3. Добавление индекса

```bash
# Создать миграцию
dotnet ef migrations add AddTelemetryKeyIndex --project src/MyIOT.Api
```

```csharp
public partial class AddTelemetryKeyIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_telemetry_device_key_time
            ON telemetry (device_id, key, timestamp DESC);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX IF EXISTS idx_telemetry_device_key_time;");
    }
}
```

**Важно:** `CONCURRENTLY` позволяет создать индекс без блокировки таблицы в production.

### 4. Seed данных

```bash
dotnet ef migrations add SeedDevices --project src/MyIOT.Api
```

```csharp
public partial class SeedDevices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "devices",
            columns: new[] { "id", "name", "access_token", "created_at" },
            values: new object[] 
            { 
                Guid.NewGuid(), 
                "Demo Device", 
                "demo-token-12345", 
                DateTime.UtcNow 
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DELETE FROM devices WHERE name = 'Demo Device';");
    }
}
```

---

## Откат миграций

### Удаление последней миграции

```bash
# Откатить в БД и удалить файлы миграции
dotnet ef migrations remove --project src/MyIOT.Api --force

# Только удалить файлы (если миграция НЕ была применена)
dotnet ef migrations remove --project src/MyIOT.Api
```

### Откат к конкретной миграции

```bash
# Список всех миграций
dotnet ef migrations list --project src/MyIOT.Api

# Откатить до определённой миграции
dotnet ef database update AddDescriptionToDevice --project src/MyIOT.Api

# Откатить ВСЕ миграции
dotnet ef database update 0 --project src/MyIOT.Api
```

---

## Production Deployment

### 1. Генерация SQL-скрипта

```bash
# Для всех миграций
dotnet ef migrations script --idempotent --project src/MyIOT.Api -o migration.sql

# От InitialCreate до последней
dotnet ef migrations script InitialCreate --project src/MyIOT.Api -o migration.sql
```

**`--idempotent`** — создаёт скрипт, который можно запустить несколько раз безопасно.

### 2. Применение через psql

```bash
# Применить SQL-скрипт
psql -h prod-server -U myiot -d myiot_db -f migration.sql

# Или через Docker
docker exec -i myiot-timescaledb psql -U myiot -d myiot_db < migration.sql
```

### 3. Разделение миграций

**Bad:** Один огромный SQL-скрипт.  
**Good:** Разбить на части:

```bash
# Структура (DDL)
dotnet ef migrations script 0 20260207_Schema --project src/MyIOT.Api -o schema.sql

# Данные (DML)
dotnet ef migrations script 20260207_Schema 20260207_SeedData --project src/MyIOT.Api -o data.sql

# Индексы (отдельно, можно применять CONCURRENTLY)
dotnet ef migrations script 20260207_SeedData 20260207_Indexes --project src/MyIOT.Api -o indexes.sql
```

### 4. Backup перед миграцией

```bash
# Backup всей БД
docker exec myiot-timescaledb pg_dump -U myiot -d myiot_db -F c -f /tmp/backup.dump

# Копировать backup из контейнера
docker cp myiot-timescaledb:/tmp/backup.dump ./backup_$(date +%Y%m%d).dump

# Restore (если что-то пошло не так)
docker exec -i myiot-timescaledb pg_restore -U myiot -d myiot_db -c /tmp/backup.dump
```

---

## Troubleshooting

### Ошибка: Build failed

```bash
# Очистить bin/obj
dotnet clean --project src/MyIOT.Api
dotnet build --project src/MyIOT.Api
```

### Ошибка: DbContext не найден

```bash
# Убедиться, что в .csproj есть Design package
dotnet add src/MyIOT.Api package Microsoft.EntityFrameworkCore.Design
```

### Ошибка: Connection string не найдена

```bash
# Установить переменную окружения
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=myiot_db;Username=myiot;Password=myiot_secret"

# Или добавить в appsettings.Development.json
```

### Ошибка: TimescaleDB extension не найден

Подключиться к PostgreSQL и установить расширение:

```sql
-- Проверка доступности
SELECT * FROM pg_available_extensions WHERE name = 'timescaledb';

-- Установка
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Проверка версии
SELECT extversion FROM pg_extension WHERE extname = 'timescaledb';
```

### Ошибка: Миграция применена частично

Ручной rollback к последней успешной миграции:

```sql
-- Посмотреть историю
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

-- Удалить неудачную запись
DELETE FROM "__EFMigrationsHistory" 
WHERE "MigrationId" = '20260207120000_FailedMigration';

-- Откатить изменения вручную (если нужно)
DROP TABLE IF EXISTS new_table;
ALTER TABLE old_table DROP COLUMN IF EXISTS new_column;
```

---

## Best Practices

### 1. Именование миграций

✅ **Good:**
- `AddUserEmailIndex`
- `CreateTelemetryHypertable`
- `ChangeAttributeValueToJsonb`

❌ **Bad:**
- `Update1`
- `FixBug`
- `Changes`

### 2. Одна миграция = одна логическая единица

✅ Создать миграцию для каждого изменения:
```bash
dotnet ef migrations add AddDeviceDescription
dotnet ef migrations add AddDeviceLocation
```

❌ Накапливать много изменений:
```bash
# После 10 изменений в модели:
dotnet ef migrations add ManyChanges
```

### 3. Code review миграций

Всегда проверяйте сгенерированные файлы:
- Проверить `Up()` и `Down()`
- Убедиться, что data migration корректен
- Проверить индексы (особенно `CONCURRENTLY`)

### 4. Резервное копирование

```bash
# Backup перед каждым update
docker exec myiot-timescaledb pg_dump -U myiot -d myiot_db > backup_before_migration.sql
```

### 5. Тестирование миграций

```bash
# 1. Применить
dotnet ef database update --project src/MyIOT.Api

# 2. Экспорт данных
pg_dump -U myiot -d myiot_db --data-only > test_data.sql

# 3. Откат
dotnet ef database update PreviousMigration --project src/MyIOT.Api

# 4. Повторно применить
dotnet ef database update --project src/MyIOT.Api

# 5. Импорт данных
psql -U myiot -d myiot_db < test_data.sql

# 6. Проверка целостности
```

---

## CI/CD Integration

### GitHub Actions example

```yaml
name: Database Migration

on:
  push:
    branches: [main]

jobs:
  migrate:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Install EF Core CLI
        run: dotnet tool install --global dotnet-ef
      
      - name: Generate SQL script
        run: |
          dotnet ef migrations script --idempotent \
            --project src/MyIOT.Api \
            --output migration.sql
      
      - name: Apply migration to staging
        run: |
          psql ${{ secrets.STAGING_DB_URL }} < migration.sql
      
      - name: Run tests
        run: dotnet test
```

---

## Мониторинг миграций

### Логирование

```csharp
// src/MyIOT.Api/Program.cs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations: {Names}",
                pendingMigrations.Count(),
                string.Join(", ", pendingMigrations));
        }

        await db.Database.MigrateAsync();
        
        logger.LogInformation("All migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error applying migrations");
        throw;
    }
}
```

### Alerting

Настроить уведомления при ошибках миграций (Slack, Email, PagerDuty).
