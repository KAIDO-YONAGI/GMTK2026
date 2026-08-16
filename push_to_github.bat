@echo off
echo ========================================
echo Push code to GitHub repository
echo ========================================

cd /d "%~dp0"

echo Current branch:
git branch --show-current

echo.
echo Checking remote configuration...
git remote -v | findstr "github_gj" >nul
if errorlevel 1 (
    echo Adding GitHub remote...
    git remote add github_gj https://github.com/KAIDO-YONAGI/GMTK2026.git
)

echo.
echo Checking for uncommitted changes...
git status --porcelain
if not errorlevel 1 (
    for /f %%i in ('git status --porcelain') do (
        echo Found uncommitted changes, committing...
        git add -A
        git commit -m "auto commit"
        goto :push
    )
)

:push
echo.
echo Pushing to GitHub...
git push github_gj main

if errorlevel 1 (
    echo.
    echo ========================================
    echo Push FAILED! Please check network connection.
    echo ========================================
    pause
    exit /b 1
) else (
    echo.
    echo ========================================
    echo Push SUCCESS!
    echo ========================================
    pause
    exit /b 0
)
