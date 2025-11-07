# Oracle DDL Dependency Analyzer

Консольная программа на C# .NET 8.0 для семантического анализа Oracle SQL DDL и поиска зависимостей между объектами базы данных.

## Возможности

- **Парсинг Oracle DDL** - полноценный анализ Oracle SQL DDL файлов с использованием ANTLR4
- **Анализ объектов БД** - поддержка всех типов объектов:
  - Таблицы (Tables)
  - Представления (Views)
  - Пакеты (Packages)
  - Функции (Functions)
  - Процедуры (Procedures)
  - Триггеры (Triggers)
- **Поиск зависимостей** - автоматическое обнаружение:
  - Foreign Key зависимостей между таблицами
  - Зависимостей представлений от таблиц (через SELECT)
  - DML операций в PL/SQL коде (INSERT, UPDATE, DELETE, SELECT)
- **Экспорт результатов** - вывод в различных форматах:
  - Консольный вывод (текст)
  - DOT формат для визуализации графов (Graphviz)
  - JSON формат для дальнейшей обработки

## Архитектура

Проект использует:
- **.NET 8.0** - целевая платформа
- **ANTLR4** - генератор парсеров для анализа SQL
- **Oracle PL/SQL Grammar** - официальная грамматика из [grammars-v4](https://github.com/antlr/grammars-v4)

### Структура проекта

```
OracleDbStruct/
├── OracleDepsSol/                    # Основной проект
│   ├── Grammar/                      # ANTLR4 грамматики
│   │   ├── PlSqlLexer.g4            # Лексер Oracle PL/SQL
│   │   └── PlSqlParser.g4           # Парсер Oracle PL/SQL
│   ├── CaseChangingCharStream.cs    # Утилита для регистронезависимого парсинга
│   ├── Model.cs                      # Модель данных (DbObjectId, DepEdge, DepGraph)
│   ├── OracleDependencyAnalyzer.cs  # Основной анализатор зависимостей
│   ├── PlSqlLexerBase.cs            # Базовый класс для лексера
│   ├── PlSqlParserBase.cs           # Базовый класс для парсера
│   └── Program.cs                    # Точка входа приложения
├── Dockerfile                        # Docker образ для сборки
├── test_schema.sql                   # Тестовый DDL файл
└── README.md                         # Этот файл
```

## Требования

### Для локальной сборки:
- .NET SDK 8.0 или выше
- Windows, Linux или macOS

### Для сборки через Docker:
- Docker 20.10 или выше

## Установка и сборка

### Вариант 1: Локальная сборка

```bash
# Клонируем репозиторий
git clone <repository-url>
cd OracleDbStruct

# Восстанавливаем зависимости
dotnet restore

# Собираем проект
dotnet build -c Release

# Запускаем
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- test_schema.sql
```

### Вариант 2: Сборка через Docker

```bash
# Собираем Docker образ
docker build -t oracle-deps-analyzer .

# Запускаем анализ (монтируем текущую директорию)
docker run -v $(pwd):/data oracle-deps-analyzer /data/test_schema.sql

# С экспортом в DOT и JSON
docker run -v $(pwd):/data oracle-deps-analyzer \
  /data/test_schema.sql --dot /data/output.dot --json /data/output.json
```

## Использование

### Базовое использование

```bash
# Анализ DDL файла
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- schema.sql

# Через Docker
docker run -v $(pwd):/data oracle-deps-analyzer /data/schema.sql
```

### Экспорт в DOT формат (Graphviz)

```bash
# Генерация DOT файла
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- \
  schema.sql --dot dependencies.dot

# Визуализация с помощью Graphviz
dot -Tpng dependencies.dot -o dependencies.png
```

### Экспорт в JSON

```bash
# Генерация JSON файла
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- \
  schema.sql --json dependencies.json
```

### Комбинированный экспорт

```bash
# Вывод в консоль + DOT + JSON одновременно
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- \
  schema.sql --dot output.dot --json output.json
```

## Формат вывода

### Консольный вывод

Программа выводит три секции:

1. **TABLES (discovered)** - список всех найденных таблиц
2. **FK Dependencies (TABLE -> TABLE)** - Foreign Key зависимости между таблицами
3. **All object->table edges** - все зависимости всех объектов от таблиц

Пример:
```
== TABLES (discovered) ==
  DEPARTMENTS
  EMPLOYEES
  PROJECTS
  EMPLOYEE_PROJECTS
  SALARY_HISTORY

== FK Dependencies (TABLE -> TABLE) ==
  EMPLOYEES -> DEPARTMENTS
  EMPLOYEES -> EMPLOYEES
  PROJECTS -> DEPARTMENTS
  EMPLOYEE_PROJECTS -> EMPLOYEES
  EMPLOYEE_PROJECTS -> PROJECTS
  SALARY_HISTORY -> EMPLOYEES

== All object->table edges ==
  Table:EMPLOYEES --[ForeignKey]--> DEPARTMENTS
  Table:EMPLOYEES --[ForeignKey]--> EMPLOYEES
  View:V_EMPLOYEE_DETAILS --[ViewQuery]--> EMPLOYEES
  View:V_EMPLOYEE_DETAILS --[ViewQuery]--> DEPARTMENTS
  ...
```

### DOT формат

DOT файл можно визуализировать с помощью Graphviz:

```bash
# Генерация PNG
dot -Tpng output.dot -o diagram.png

# Генерация SVG
dot -Tsvg output.dot -o diagram.svg

# Генерация PDF
dot -Tpdf output.dot -o diagram.pdf
```

### JSON формат

JSON содержит структурированные данные:

```json
{
  "tables": [
    "DEPARTMENTS",
    "EMPLOYEES",
    "PROJECTS"
  ],
  "edges": [
    {
      "from": "Table:EMPLOYEES",
      "to": "DEPARTMENTS",
      "kind": "ForeignKey"
    },
    {
      "from": "View:V_EMPLOYEE_DETAILS",
      "to": "EMPLOYEES",
      "kind": "ViewQuery"
    }
  ]
}
```

## Примеры использования

### Пример 1: Анализ тестовой схемы

```bash
# Используем включенный тестовый файл
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- test_schema.sql
```

Тестовая схема содержит:
- 5 таблиц с Foreign Key зависимостями
- 3 представления (Views)
- 2 функции
- 2 процедуры
- 1 пакет
- 2 триггера

### Пример 2: Создание визуализации зависимостей

```bash
# Генерируем DOT файл
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- \
  test_schema.sql --dot test_deps.dot

# Создаем PNG диаграмму (требуется Graphviz)
dot -Tpng test_deps.dot -o test_deps.png
```

### Пример 3: Программная обработка результатов

```bash
# Экспортируем в JSON
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- \
  schema.sql --json output.json

# Обрабатываем с помощью jq
cat output.json | jq '.edges[] | select(.kind == "ForeignKey")'
```

## Типы зависимостей

Программа распознает следующие типы зависимостей:

| Тип | Описание | Пример |
|-----|----------|--------|
| **ForeignKey** | Foreign Key ограничения | `EMPLOYEES.dept_id -> DEPARTMENTS.dept_id` |
| **ViewQuery** | Таблицы в SELECT представлений | `VIEW v_emp -> EMPLOYEES, DEPARTMENTS` |
| **DmlRead** | SELECT в PL/SQL коде | `PROCEDURE -> EMPLOYEES (SELECT)` |
| **DmlWrite** | INSERT/UPDATE/DELETE в PL/SQL | `PROCEDURE -> EMPLOYEES (UPDATE)` |

## Ограничения

- Программа анализирует только синтаксис DDL, не выполняя реального подключения к БД
- Динамический SQL (EXECUTE IMMEDIATE) не анализируется
- Сложные конструкции PL/SQL могут быть пропущены
- Анализ основан на токенах, а не на полном AST (для производительности)

## Разработка

### Добавление новых возможностей

Основные точки расширения:

1. **OracleDependencyAnalyzer.cs** - логика анализа
2. **Model.cs** - модель данных
3. **Program.cs** - CLI интерфейс

### Отладка

```bash
# Сборка в Debug режиме
dotnet build -c Debug

# Запуск с отладкой
dotnet run --project OracleDepsSol/OracleDepsSol.csproj -- schema.sql
```

## Лицензия

Грамматики ANTLR4 для Oracle PL/SQL используются по лицензии Apache License 2.0 из проекта [antlr/grammars-v4](https://github.com/antlr/grammars-v4).

## Контакты и поддержка

Для сообщений об ошибках и предложений используйте Issues в репозитории проекта.
