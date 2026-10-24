[README.md](https://github.com/user-attachments/files/33904718/README.md)
# EstateFlow 🏠

**EstateFlow** е уеб приложение за управление и търсене на обяви за недвижими имоти, разработено като университетски проект по **„Софтуерни технологии 2“** към Факултета по математика и информатика на Пловдивския университет „Паисий Хилендарски“.

**Екип:** Team 15  
**Хранилище:** [ST2-2026-Team-15-EstateFlow](https://github.com/MitakaP/ST2-2026-Team-15-EstateFlow)  
**Технологии:** C#, ASP.NET Core 8 MVC, Razor Views, SQLite, JavaScript, Bootstrap, Google Gemini REST API, xUnit.

## Основни функционалности

- Списък с имоти с филтриране и странициране в потребителския интерфейс.
- Преглед на подробности за конкретен имот.
- Добавяне, редактиране и изтриване на имоти (CRUD).
- Валидация на въведените данни, включително проверка за уникален референтен номер.
- **Интелигентно търсене:** потребителят описва критериите си на български или английски, а Google Gemini ги преобразува в структурирана заявка. Приложението я валидира и извлича реалните резултати от SQLite.
- Сортиране по позволени полета, включително заявки само за сортиране без филтриращи условия.

## Развитие по седмици

### Седмица 1 — Insert, Select with Filter и Singleton

Реализирани са:

- **Insert** — добавяне на имот чрез `PropertiesController.Create` и `IDatabase.Add`.
- **Select** — извличане на всички имоти чрез `IDatabase.GetAll`, получаване на конкретен имот чрез `GetById` и визуализиране в Razor изгледи.
- **Select with Filter** — филтриране на списъка с имоти и клиентско странициране чрез `wwwroot/js/properties.js`.
- **Singleton Design Pattern** — един споделен екземпляр на `Database`, инициализиран чрез `Database.Initialize(connectionString)` и регистриран в Dependency Injection контейнера като `IDatabase`.

### Седмица 2 — Update и Delete

- **Update** — редактиране на съществуващ имот чрез `PropertiesController.Edit`, `IDatabase.Update` и `Views/Properties/Edit.cshtml`.
- **Delete** — изтриване с отделен екран за потвърждение чрез `PropertiesController.Delete`/`DeleteConfirmed`, `IDatabase.Delete` и `Views/Properties/Delete.cshtml`.
- **Валидация** — Data Annotations и проверка за уникалност на референтния номер; формите за запис и изтриване използват anti-forgery защита.

### Седмица 3 — Gemini AI и автоматизирани тестове

- Свързване с отдалечен езиков модел чрез `IGeminiPropertyService` и `GeminiPropertyService`.
- Преобразуване на естествен език в `PropertyQuery`, съставен от условия, логически оператор, сортиране и лимит.
- Поддръжка на числови и текстови филтри и устойчиво прочитане на JSON числови стойности чрез `FlexibleStringConverter`.
- Проверка на разрешените колони и оператори и превод до **параметризирана SQL SELECT заявка** чрез `PropertyQuerySqlTranslator`.
- Заявки само за сортиране (без `WHERE`, когато няма условия).
- xUnit тестове за преводача, JSON конвертора и Gemini услугата с симулирани HTTP отговори.

> **Тестов статус:** При последното локално изпълнение във Visual Studio всички тестове са преминали успешно (потвърдено от разработчика). Автоматизираните тестове на Gemini услугата не изискват реални API повиквания.

## MVC архитектура

Приложението следва **Model–View–Controller (MVC)**:

| Слой | Основни компоненти | Отговорност |
| --- | --- | --- |
| Models | `Property`, `PropertyQuery`, `PropertyQueryCondition` | Данни за имота и ограничен формат за ИИ заявки |
| Views | `Views/Properties/*.cshtml` | Потребителски интерфейс, форми и резултати |
| Controllers | `PropertiesController`, `HomeController` | HTTP действия, проверка на входа и координация на услугите |
| Data | `IDatabase`, `Database` | SQLite достъп и CRUD операции |
| Services | `GeminiPropertyService`, `PropertyQuerySqlTranslator` | ИИ комуникация и безопасен превод на заявки |

**Основни Razor изгледи:** `Index`, `Details`, `Create`, `Edit` и `Delete` — пет отделни изгледа за управление на имоти. Използва се и частичен изглед `_PropertyForm.cshtml` за формите.

### Поток на интелигентното търсене

```text
Потребител: „Покажи апартаменти в Пловдив до 150000 евро“
    |
    v
PropertiesController.AiSearch
    |
    v
IGeminiPropertyService / GeminiPropertyService
    |  Google Gemini REST API
    v
PropertyQuery (структуриран JSON, а не SQL)
    |
    v
PropertyQuerySqlTranslator (разрешени полета и оператори)
    |
    v
PropertySqlCommand (SQL + параметри)
    |
    v
IDatabase.Search -> SQLite -> резултати в интерфейса
```

ИИ **не изпълнява SQL** и няма директен достъп до базата данни. Gemini получава инструкции за позволените полета, но не получава редовете с имоти или личните контакти на брокерите. SQL се изгражда и изпълнява от приложението с параметри.

## Design Patterns

| Шаблон | Реализация | Приложение |
| --- | --- | --- |
| **Singleton** | `Database.Initialize(...)`, регистрация на `IDatabase` чрез `AddSingleton` | Един споделен обект за работа с базата в рамките на приложението |
| **Adapter / Facade** | `IGeminiPropertyService`, `GeminiPropertyService` | Скриване на HTTP комуникацията, структурата на Gemini API и обработката на отговора зад услуга |
| **Interpreter / Translator** | `PropertyQuery`, `PropertyQuerySqlTranslator`, `PropertySqlCommand` | Интерпретиране на ограничена заявка и превръщането ѝ в параметризиран SQL |
| **Result Object** | `PropertyAiResult` | Представяне на успешен ИИ резултат или очаквана грешка без използване на изключения за обичайния поток |
| **Options Pattern** | `GeminiOptions`, `IOptions<GeminiOptions>` | Типизирана конфигурация на Gemini API |

Интерфейсът `IDatabase` допълнително отделя контролерите от конкретната SQLite реализация и осигурява **repository-подобна абстракция**. Шаблоните са коментирани и в съответните C# класове.

## Структура на проекта

```text
EstateFlow.sln
EstateFlow/
├── Configuration/          # GeminiOptions
├── Controllers/            # PropertiesController, HomeController
├── Data/                   # IDatabase, Database
├── Enums/                  # PropertyType
├── Models/                 # Property, PropertyQuery и др.
├── Services/               # Gemini API, JSON converter, SQL translator
├── Validations/            # Валидация за уникалност
├── Views/Properties/       # Index, Details, Create, Edit, Delete
├── wwwroot/                # JavaScript, CSS, статични ресурси
├── appsettings.json        # Несекретни настройки
└── EstateFlow.csproj
EstateFlow.Tests/              # xUnit тестове
```

## Изисквания

- **.NET 8 SDK** (или съвместима среда с .NET 8).
- Visual Studio с ASP.NET and web development workload **или** друг редактор, поддържащ .NET.
- Интернет връзка и личен Gemini API ключ **само** за интелигентното търсене. Основните CRUD функции работят локално.

## Стартиране на проекта

1. Клонирайте хранилището:

   ```bash
   git clone https://github.com/MitakaP/ST2-2026-Team-15-EstateFlow.git
   cd ST2-2026-Team-15-EstateFlow
   ```

2. Възстановете зависимостите и компилирайте:

   ```bash
   dotnet restore
   dotnet build
   ```

3. При първо стартиране приложението подготвя локалната SQLite база според конфигурацията `ConnectionStrings:EstateFlowDb` (по подразбиране `App_Data/estateflow.db`).

4. Стартирайте:

   ```bash
   dotnet run --project EstateFlow/EstateFlow.csproj
   ```

   Или отворете `EstateFlow.sln` във Visual Studio и стартирайте проекта `EstateFlow`.

### Настройване на Gemini API ключ

Създайте собствен ключ в [Google AI Studio](https://aistudio.google.com/apikey). В директорията с `EstateFlow.csproj` задайте ключа чрез .NET User Secrets:

```bash
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project EstateFlow/EstateFlow.csproj
```

Във Visual Studio може да използвате **десен бутон върху проекта → Manage User Secrets**. Настройката на модела се намира в `EstateFlow/appsettings.json`, секция `Gemini:Model`; изберете идентификатор, достъпен за вашия API проект.

**Не записвайте и не публикувайте истинския API ключ** в `appsettings.json`, изходния код, логове или GitHub. `secrets.json` от User Secrets се намира извън папката на хранилището. При работа на друг компютър ключът се конфигурира наново.

## Примерни заявки към ИИ търсачката

- `Покажи всички имоти в Пловдив`
- `Намери апартаменти до 150000 евро`
- `Покажи имоти с площ над 80 кв.м.`
- `Намери имоти с поне 3 стаи`
- `Подреди всички имоти от най-евтин към най-скъп`

**Уточнения:** `Rooms` означава **общ брой стаи**, не брой спални. `Price` е в EUR, `Area` — в кв. м. ИИ търсенето поддържа най-много 12 филтриращи условия и до 100 резултата на заявка. Върнатите резултати зависят от наличните записи в базата.

## Тестове

От директорията на решението:

```bash
dotnet test EstateFlow.sln
```

Или във Visual Studio: **Test → Test Explorer → Run All Tests**.

Покритите сценарии включват:

- Текстови и числови условия и сортиране, включително без `WHERE`.
- Параметризация и отхвърляне на неразрешени колони/оператори.
- Екраниране на `LIKE` специалните символи.
- Преобразуване на JSON числа към низове чрез `FlexibleStringConverter`.
- Успешни и неуспешни Gemini HTTP отговори чрез тестови `HttpMessageHandler` обекти.

Преди предаване е препоръчителна и ръчна проверка на Create, Details, Edit, Delete, стандартното филтриране и Gemini търсенето с реален ключ.

## Сигурност и ограничения

- SQL заявки се създават **само** от валидирания C# преводач и използват параметри.
- Имената на колони и SQL операторите се избират от изрично разрешени стойности.
- Формите за промяна на данни и заявката за ИИ търсене използват anti-forgery защита.
- API ключът се съхранява локално чрез User Secrets или подходящи секретни настройки на средата.
- Публичният проект е **учебно приложение**. Няма реализирана система за потребителски роли и това не трябва да се приема за готово production решение.
- Достъпността, квотите и цената на Gemini моделите се определят от Google и може да се променят.

## Авторство и учебен контекст

Разработено за дисциплината **„Софтуерни технологии 2“**, **Team 15**, учебна 2026 година.

Частта за Gemini е разработена с оглед на лабораторното ръководство *„Интеграция на Google Gemini ИИ в приложението ContactsMVC“* (ФМИ, ПУ „Паисий Хилендарски“, 2026), като подходът `естествен език → JSONQuery → безопасен SQL SELECT` е адаптиран към предметната област на EstateFlow.
