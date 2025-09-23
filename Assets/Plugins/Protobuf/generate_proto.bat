@echo off
echo Generating C# code from .proto files...

setlocal enabledelayedexpansion

REM Set path to protoc.exe
set PROTOC_PATH=%~dp0protoc.exe

REM Set Unity project root directory (two levels up)
set PROJECT_ROOT=%~dp0..\..

REM Set Scripts directory path
set SCRIPTS_DIR=%PROJECT_ROOT%\Scripts

REM Search for all .proto files in Scripts directory and subdirectories
for /R "%SCRIPTS_DIR%" %%F in (*.proto) do (
    set PROTO_FILE=%%F
    echo Processing: !PROTO_FILE!
    
    REM Get file's relative path (relative to Scripts directory)
    set REL_PATH=%%~dpnF
    call set REL_PATH=%%REL_PATH:%SCRIPTS_DIR%=%%
    
    REM Set output directory to the same directory as the .proto file
    set OUTPUT_DIR=%%~dpF
    
    REM Change to output directory to avoid absolute path issues
    pushd "!OUTPUT_DIR!"
    
    REM Execute protoc command with relative path
    "%PROTOC_PATH%" --csharp_out=. "%%~nxF"
    
    if !errorlevel! equ 0 (
        echo Successfully generated: !OUTPUT_DIR!\%%~nF.cs
    ) else (
        echo Error: Failed to generate !OUTPUT_DIR!\%%~nF.cs
    )
    
    REM Return to original directory
    popd
    
    echo.
)

echo.
echo All .proto files processed.
pause
