Set WshShell = CreateObject("WScript.Shell")
Set FSO = CreateObject("Scripting.FileSystemObject")

' Get the current directory of the .vbs file
currentDir = FSO.GetParentFolderName(WScript.ScriptFullName)

' Change the working directory to ensure correct execution
WshShell.CurrentDirectory = currentDir

' Build the full path to BioMatrix.exe
appPath = currentDir & "\BioMatrix.exe"

' Retrieve the arguments passed to the .vbs file
Set args = WScript.Arguments
argString = ""

' Concatenate all arguments into a single string
For i = 0 To args.Count - 1
    argString = argString & args(i) & " "
Next

' Trim any trailing space from the argument string
argString = Trim(argString)

' Run BioMatrix.exe with the passed arguments
WshShell.Run Chr(34) & appPath & Chr(34) & " " & argString, 0
