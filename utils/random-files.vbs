Dim words
Dim fso
Dim ObjOutFile

words = Array("Lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua")

Set fso = CreateObject("Scripting.FileSystemObject")

For i = 0 to 1000
  WScript.Echo("Generating file #" & i)
  GenerateFile(i)
Next

Function CreateGuid()
  Dim TypeLib
  Set TypeLib = CreateObject("Scriptlet.TypeLib")
  CreateGuid = Mid(TypeLib.Guid, 2, 36)
End Function

Function Rand(Min, Max)
  Rand = Int((Max - Min + 1) * Rnd + Min)
End Function

Sub GenerateFile(FileNo)
  Dim fileName
  fileName = "data\" & CreateGuid() & ".txt"

  Set ObjOutFile = fso.CreateTextFile(fileName)
  
  For k = 0 to 10000
    ObjOutFile.Write(words(Rand(0, UBound(words))) & " ")
  Next
End Sub
