@echo off
echo ================================================
echo  Depo Rota Optimizasyonu - Build Script
echo ================================================
echo.

REM .NET SDK kontrolu
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo HATA: .NET 7 SDK bulunamadi!
    echo Lutfen https://dotnet.microsoft.com/download adresinden indirin.
    pause
    exit /b 1
)

echo [1/3] NuGet paketleri yukleniyor...
dotnet restore WarehouseSimulator\WarehouseSimulator.csproj
if %errorlevel% neq 0 (
    echo HATA: Restore basarisiz!
    pause
    exit /b 1
)

echo [2/3] Proje derleniyor...
dotnet build WarehouseSimulator\WarehouseSimulator.csproj -c Release
if %errorlevel% neq 0 (
    echo HATA: Build basarisiz!
    pause
    exit /b 1
)

echo [3/3] Uygulama baslatiliyor...
dotnet run --project WarehouseSimulator\WarehouseSimulator.csproj -c Release

pause
