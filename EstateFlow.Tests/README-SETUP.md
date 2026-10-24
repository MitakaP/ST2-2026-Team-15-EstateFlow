# EstateFlow Week 3 — xUnit test project

Copy this **EstateFlow.Tests** directory next to the existing **EstateFlow** directory (same level as EstateFlow.sln).

This test suite targets the *latest local code* discussed in the conversation, including the `Services/FlexibleStringConverter.cs` file and registration in `GeminiPropertyService`.
It deliberately does **not** overwrite your working application files.

In a terminal at the folder containing EstateFlow.sln:

```powershell
dotnet sln EstateFlow.sln add .\EstateFlow.Tests\EstateFlow.Tests.csproj
dotnet restore
dotnet test EstateFlow.sln
```

Or use Solution Explorer → Add → Existing Project → EstateFlow.Tests.csproj, then Test → Test Explorer → Run All.

The HTTP tests use a mocked HttpMessageHandler; no Gemini API key or network request is used.

If compilation says `FlexibleStringConverter` is missing, ensure the converter created during the successful JSON fix is present in `EstateFlow/Services/` and is public in the `EstateFlow.Services` namespace.

Known limitation documented by a passing test: current translator rejects empty conditions (sorting-only query). This is not proof that sorting-only AI search works.

`dotnet test` has not been executed in the authoring environment, which has no .NET SDK. Review compile/test output in Visual Studio; do not treat included tests as already verified.
