@echo off

If [%1]==[] goto nomsg
git add .
git commit -m %1

:Ask
echo Would you like to push to remote server? (Y/N)
set INPUT=
set /P INPUT=Input: %=%
If /I "%INPUT%"=="y" goto yes 
If /I "%INPUT%"=="n" goto no
echo Incorrect input & goto Ask

:yes
git push
goto exit

:no
goto exit

:nomsg
echo Please insert commit message & goto exit

:exit
