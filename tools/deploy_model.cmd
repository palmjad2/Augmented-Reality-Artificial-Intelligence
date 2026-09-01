@echo off
REM Copy a trained ML-Agents model into the asset the Editor's agent uses.
REM Usage:  tools\deploy_model.cmd <run-id>      e.g.  tools\deploy_model.cmd 002
REM Source: results\<run-id>\Prosthetic.onnx (written at each checkpoint and when training stops)
REM Target: Assets\Models\Prosthetic.onnx (referenced by ArmAnimation > Behavior Parameters > Model)
REM The previous model is kept as Assets\Models\Prosthetic.onnx.bak (gitignored) for quick rollback.
setlocal
if "%~1"=="" ( echo Usage: %~nx0 ^<run-id^> & exit /b 1 )
set "ROOT=%~dp0.."
set "SRC=%ROOT%\results\%~1\Prosthetic.onnx"
set "DST=%ROOT%\Assets\Models\Prosthetic.onnx"
if not exist "%SRC%" ( echo Not found: %SRC% & echo No .onnx yet? It is written at checkpoints and when you stop training with Ctrl+C. & exit /b 1 )
if exist "%DST%" copy /y "%DST%" "%DST%.bak" >nul
copy /y "%SRC%" "%DST%" >nul && echo Deployed %SRC% -^> Assets\Models\Prosthetic.onnx  (previous saved as Prosthetic.onnx.bak)
echo Unity will reimport it automatically; press Play in the Editor to watch the new policy.
endlocal
