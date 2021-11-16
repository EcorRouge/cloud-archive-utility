Dim fso
Dim ObjOutFile
 
'Creating File System Object
Set fso = CreateObject("Scripting.FileSystemObject")
 
'Create an output file
Set ObjOutFile = fso.CreateTextFile("utility-files.iss")

'Call the GetFile function to get all files
ListFolder "..\build\net5.0-windows\publish"
 
'Close the output file
ObjOutFile.Close
 
WScript.Echo("Completed")

Function ListFolder(FolderName)
	GetFiles FolderName, ""
End Function
 
Function GetFiles(FolderName, RelFolder)
    On Error Resume Next
     
    Dim ObjFolder
    Dim ObjSubFolders
    Dim ObjSubFolder
    Dim ObjFiles
    Dim ObjFile
 
    Set ObjFolder = fso.GetFolder(FolderName)
    Set ObjFiles = ObjFolder.Files

    'Write all files to output files
    For Each ObjFile In ObjFiles
        'Source: "..\wpf\ParagraphSnip\bin\Release\ParagraphSnip.exe.config"; DestDir: "{app}"; Flags: ignoreversion
	If Len(RelFolder)>0 Then
		ObjOutFile.WriteLine("Source: ""..\build\net5.0-windows\publish" & RelFolder & "\" & ObjFile.Name & """; DestDir: ""{app}" & RelFolder & """; Flags: ignoreversion")
	Else
		ObjOutFile.WriteLine("Source: ""..\build\net5.0-windows\publish" & RelFolder & "\" & ObjFile.Name & """; DestDir: ""{app}""; Flags: ignoreversion")
	End If
    Next

 
    'Getting all subfolders
    Set ObjSubFolders = ObjFolder.SubFolders
     
    For Each ObjFolder In ObjSubFolders
        'Getting all Files from subfolder
        GetFiles ObjFolder.Path, RelFolder & "\" & ObjFolder.Name
    Next
End Function