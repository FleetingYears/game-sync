@echo off
chcp 65001 >nul
python -X utf8 "%~dp0export.py" %*
exit /b %errorlevel%
