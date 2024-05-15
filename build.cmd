echo off
setlocal
set _DP0=%~dp0
set _ARTIFACT=%_DP0%artifacts
set _7ZEXE=C:\Program Files\7-Zip\7z.exe
set _MSBUILDEXE=%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MsBuild.exe

for /f tokens^=1^,2^ delims^=^" %%a in ('sed -n /AssemblyFileVersion/p %_DP0%Properties\AssemblyInfo.cs') do (set _VERSION=%%b)
echo Building MySqlUtils %_VERSION%
set _DEST=%_ARTIFACT%\%_VERSION%

rmdir /s /q %_DP0%artifacts
mkdir %_DEST%\bin
del %_DP0%*.zip

call "%_MSBUILDEXE%" MySqlUtils.csproj /p:Configuration=Release;ExcludeXmlAssemblyFiles=false /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /m
copy %_DP0%bin\MySqlUtils.dll %_DEST%\bin /y
copy %_DP0%extension.xml %_ARTIFACT% /y
copy %_DP0%applicationHost.xdt %_DEST% /y
copy %_DP0%default.ashx %_DEST% /y
copy %_DP0%default.htm %_DEST% /y
copy %_DP0%favicon.ico %_DEST% /y
copy %_DP0%web.config %_DEST% /y
call "%_7ZEXE%" a  %_DP0%MySqlUtils.%_VERSION%.zip %_ARTIFACT%\* -r
