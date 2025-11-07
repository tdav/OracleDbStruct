# Dockerfile для сборки и запуска OracleDepsSol
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Копируем файлы проекта
COPY OracleDepsSol.sln .
COPY OracleDepsSol/ ./OracleDepsSol/

# Восстанавливаем зависимости и собираем проект
RUN dotnet restore
RUN dotnet build -c Release --no-restore

# Публикуем приложение
RUN dotnet publish OracleDepsSol/OracleDepsSol.csproj -c Release -o /app/publish --no-restore

# Создаем runtime образ
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .

# Точка входа
ENTRYPOINT ["dotnet", "OracleDepsSol.dll"]
