"""Package the current root console application with placeholder credentials."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json

root = Path(__file__).resolve().parent.parent
output = root / "artifacts" / "CriticalCare.Console.Source.zip"
output.parent.mkdir(exist_ok=True)
with ZipFile(output, "w", ZIP_DEFLATED) as archive:
    for folder in ["ConsoleApp", "Controllers", "Services", "Interfaces", "Repositories", "Infrastructure",
                   "Models", "DTOs", "Data", "Migrations", "Tests/Console"]:
        for path in (root / folder).rglob("*"):
            if path.is_file() and not {"bin", "obj"}.intersection(path.parts) and path.suffix in {".cs", ".md", ".sql"}:
                archive.write(path, path.relative_to(root).as_posix())
    for name in ["Program.cs", "GlobalUsings.cs", "EquipmentManagementBackend.csproj",
                 "README.md", "AI_CONTEXT_README.md", "docs/TCS_TRANSFER_GUIDE.md", ".editorconfig", "global.json", ".config/dotnet-tools.json"]:
        archive.write(root / name, name)
    archive.writestr("appsettings.json", json.dumps({"ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=criticalcare_console;Username=postgres;Password=YOUR_PASSWORD"
    }}, indent=2))
with ZipFile(output) as archive:
    assert archive.testzip() is None
    assert "Program.cs" in archive.namelist()
    assert "Services/Backups/BackupsService.cs" in archive.namelist()
print(output)
