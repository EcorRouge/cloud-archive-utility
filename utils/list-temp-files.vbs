Dim fso
Dim ObjOutFile
 
'Creating File System Object
Set fso = CreateObject("Scripting.FileSystemObject")
 
'Create an output file
Set ObjOutFile = fso.CreateTextFile("export.csv")

ObjOutFile.WriteLine("Collector|Drive|Name|Path|Size (bytes)|Modified Date|Classification|Type|Category|Age Bucket|Size Bucket|Original Path")

'Call the GetFile function to get all files
ListFolder "D:\книги"

'Close the output file
ObjOutFile.Close
 
WScript.Echo("Completed")

Function ListFolder(FolderName)
	GetFiles FolderName, ""
End Function
 
Function GetFiles(FolderName, RelFolder)
    On Error Resume Next
     
    Dim ObjFolder
    Dim ObjFiles
    Dim ObjFile
    Dim ChildFolder
    Dim ChildFolders
 
    Set ObjFolder = fso.GetFolder(FolderName)
    Set ObjFiles = ObjFolder.Files

    Set ChildFolders = ObjFolder.SubFolders

    'Write all files to output files
    For Each ObjFile In ObjFiles
   	   ObjOutFile.WriteLine("test|" & ObjFile.Drive.DriveLetter & ":|" & ObjFile.Name & "|" & ObjFile.Path & "|" & ObjFile.Size & "|Risk management|class|Technical Documents|7 - 8| < 10KB|" & ObjFile.Path)
    Next

    For Each ChildFolder In ChildFolders
       GetFiles ChildFolder.Path, RelFolder & "\" & ChildFolder.Name
    Next

End Function