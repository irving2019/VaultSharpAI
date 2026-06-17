[Setup]
AppName=VaultSharpAI
AppVersion=1.0
DefaultDirName={autopf}\VaultSharpAI
DefaultGroupName=VaultSharpAI
OutputBaseFilename=VaultSharpAI_Windows_Setup
Compression=lzma2
SolidCompression=yes
; Используем иконку из папки проекта для самого инсталлятора
SetupIconFile=C:\Users\vovap\source\repos\VaultSharpAI\VaultSharpAI.Desktop\Assets\intellect.ico

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Главный исполняемый файл из папки publish (исходя из скриншота image_d98ac1.png)
Source: "C:\Users\vovap\source\repos\VaultSharpAI\VaultSharpAI.Desktop\bin\Release\net10.0\win-x64\publish\VaultSharpAI.Desktop.exe"; DestDir: "{app}"; Flags: ignoreversion
; Все остальные файлы сборки, которые лежат рядом в publish
Source: "C:\Users\vovap\source\repos\VaultSharpAI\VaultSharpAI.Desktop\bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Ярлык в меню Пуск с нашей иконкой
Name: "{group}\VaultSharpAI"; Filename: "{app}\VaultSharpAI.Desktop.exe"; IconFilename: "C:\Users\vovap\source\repos\VaultSharpAI\VaultSharpAI.Desktop\Assets\intellect.ico"
; Ярлык на Рабочем столе
Name: "{autodesktop}\VaultSharpAI"; Filename: "{app}\VaultSharpAI.Desktop.exe"; IconFilename: "C:\Users\vovap\source\repos\VaultSharpAI\VaultSharpAI.Desktop\Assets\intellect.ico"; Tasks: desktopicon

[Run]
; Предложение запустить программу после завершения установки
Filename: "{app}\VaultSharpAI.Desktop.exe"; Description: "{cm:LaunchProgram,VaultSharpAI}"; Flags: nowait postinstall skipifsilent