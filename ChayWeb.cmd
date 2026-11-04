@echo off
cd /d "%~dp0"
echo Dang khoi dong Quan ly cua hang sua xe...
echo Neu Visual Studio dang chay bai, hay dung Debug truoc.
sqllocaldb start MSSQLLocalDB
dotnet run --project "QuanLyCuaHangSuaXe.Web.csproj" --launch-profile http
pause
