# Для упрощения процесса разработки и тестирования можно использовать докер 🐳

### Пошаговый гайд:

1. Скачать [Docker desktop](https://www.docker.com/products/docker-desktop/) 😉
2. Запустить docker desktop 🤭
3. Проверить настройки в `dockerized.secrets.json` в корне проекта. Compose монтирует этот файл в оба backend-контейнера как `secrets.json`; отдельный корневой `secrets.json` создавать не нужно 🔐
4. Выполнить `docker compose -f compose.dev.yml up dev_cogni dev_chat_service` в корне проекта 👾
5. Подождать запуска ⌛

С vpn на 4 шаг может выполниться не с первого раза при первом запуске 😥

`dev_cogni` и `dev_chat_service` запускаются командой `dotnet run`, без `dotnet watch`. Исходники подключены в контейнеры как volumes, но изменения backend-кода автоматически не пересобираются. После таких изменений перезапустите сервисы:

```powershell
docker compose -f compose.dev.yml restart dev_cogni dev_chat_service
```

Фронтенд запускается в режиме Compose Watch отдельной командой ниже.

Запуск фронтенда для чатов: `docker compose -f .\compose.dev.yml watch dev_chat_frontend`

Немного больше команд:
`docker compose -f compose.dev.yml up dev_cogni dev_chat_service -d` - Запуск в фоновом режиме - так он не будет привязан к сессии консоли, в таком случае логи нужно смотреть с помощью `docker compose -f compose.dev.yml logs -f dev_chat_service` (именно эта команда смотрит логи только для `dev_chat_service`, также есть `dev_cogni`) 🫣

### Миграции и тестовые данные

В dev-конфигурации для `dev_cogni` задано `MIGRATE=true`. При запуске приложения EF Core автоматически применяет все ожидающие миграции к базе, используя строку подключения из `dockerized.secrets.json`. Перед запуском убедитесь, что PostgreSQL доступен по указанной там строке подключения.

Запуск приложения и автоматическое применение миграций:

```powershell
docker compose -f compose.dev.yml up dev_cogni dev_chat_service
```

Посмотреть логи миграций:

```powershell
docker compose -f compose.dev.yml logs -f dev_cogni
```

Чтобы создать новую миграцию, выполните из корня `CogniProject` (нужен установленный `dotnet-ef`):

```powershell
dotnet ef migrations add <MigrationName> --project Cogni/Cogni.csproj --startup-project Cogni/Cogni.csproj
```

В dev-окружении миграция применится при следующем запуске `dev_cogni`. Применить её вручную можно командой:

```powershell
dotnet ef database update --project Cogni/Cogni.csproj --startup-project Cogni/Cogni.csproj
```

После запуска `dev_cogni` наполните таблицы тестовыми данными:

```powershell
.\Cogni\seed-dev.ps1 -Password "NewDemoPassword"
```

Скрипт идемпотентный: повторный запуск не должен создавать дубликаты. Он создаёт восемь тестовых пользователей:

- `alex.demo@cogni.local`
- `maya.demo@cogni.local`
- `anna.demo@cogni.local`
- `ivan.demo@cogni.local`
- `olga.demo@cogni.local`
- `max.demo@cogni.local`
- `kate.demo@cogni.local`
- `dmitry.demo@cogni.local`

Для новых аккаунтов по умолчанию используется пароль `CogniDemo123!`. Задать другой пароль при запуске можно так:

```powershell
.\Cogni\seed-dev.ps1 -Password "NewDemoPassword"
```

Сид применяет миграции перед заполнением и создаёт данные во всех 17 таблицах. Фотографии представлены тестовыми URL; Uploadcare для наполнения не требуется. Существующим demo-аккаунтам скрипт пароль не меняет.
