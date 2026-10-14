@echo off
dotnet run --project "%~dp0..\server\server.csproj"
exit /b %errorlevel%
